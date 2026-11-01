using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using V2rayWatch.Server.Core;
using V2rayWatch.Server.Engine;
using V2rayWatch.Server.Web;
var builder = WebApplication.CreateBuilder(args);
var configPath = Environment.GetEnvironmentVariable("V2RAYWATCH_CONFIG") ?? "/etc/v2raywatch/server.json";
builder.Configuration.AddJsonFile(configPath, optional: true, reloadOnChange: false);
var server = builder.Configuration.GetSection("Server").Get<ServerOptions>() ?? new();
server.Validate();
var password = Environment.GetEnvironmentVariable("V2RAYWATCH_ADMIN_PASSWORD");
if (string.IsNullOrWhiteSpace(password) || password.Length < 20) throw new InvalidOperationException("V2RAYWATCH_ADMIN_PASSWORD is missing or too short.");
var keyFile = Environment.GetEnvironmentVariable("V2RAYWATCH_PROFILE_KEY_FILE") ?? "/etc/v2raywatch/profile.key";
var salt = RandomNumberGenerator.GetBytes(16);
var expectedHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA256, 32);
builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Parse(server.ListenAddress), server.Port));
builder.Services.AddSingleton(server);
builder.Services.AddSingleton(new ProfileSecretProtector(keyFile));
builder.Services.AddSingleton<SqliteProfileRepository>();
builder.Services.AddSingleton<IProbeEngine, XrayDelayTester>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "V2rayWatch.Server.Auth"; o.LoginPath = "/login";
    o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; o.Cookie.Path = server.BasePath;
    o.ExpireTimeSpan = TimeSpan.FromHours(12); o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c =>
    {
        if (c.Request.Path.StartsWithSegments("/api")) { c.Response.StatusCode = 401; return Task.CompletedTask; }
        c.Response.Redirect(c.RedirectUri); return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("login", c => RateLimitPartition.GetFixedWindowLimiter(
        c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(5), QueueLimit = 0, AutoReplenishment = true }));
});
var app = builder.Build();
var repository = app.Services.GetRequiredService<SqliteProfileRepository>();
await repository.InitializeAsync();
app.Use(async (ctx, next) =>
{
    if (string.Equals(ctx.Request.Path.Value, server.BasePath, StringComparison.Ordinal))
    { ctx.Response.Redirect(server.BasePath + "/"); return; }
    if (!ctx.Request.Path.StartsWithSegments(server.BasePath, StringComparison.Ordinal))
    { ctx.Response.StatusCode = StatusCodes.Status404NotFound; return; }
    await next();
});
app.UsePathBase(server.BasePath);
app.Use(async (ctx, next) =>
{
    ctx.Response.OnStarting(() =>
    {
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["X-Frame-Options"] = "DENY";
        ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
        ctx.Response.Headers["Cache-Control"] = "no-store";
        return Task.CompletedTask;
    });
    await next();
});
app.UseRouting(); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/login", async (IWebHostEnvironment env) =>
    Results.Content(await File.ReadAllTextAsync(Path.Combine(env.ContentRootPath, "wwwroot", "login.html")), "text/html; charset=utf-8")).AllowAnonymous();
app.MapPost("/api/v1/login", async (LoginRequest request, HttpContext ctx) =>
{
    if (string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024) return Results.Unauthorized();
    var candidate = Rfc2898DeriveBytes.Pbkdf2(request.Password, salt, 210_000, HashAlgorithmName.SHA256, 32);
    if (!CryptographicOperations.FixedTimeEquals(expectedHash, candidate)) return Results.Unauthorized();
    var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, "Administrator") }, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return Results.Ok(new { authenticated = true });
}).AllowAnonymous().RequireRateLimiting("login");
app.MapPost("/api/v1/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { authenticated = false });
}).RequireAuthorization();
app.MapGet("/", async (IWebHostEnvironment env) =>
    Results.Content(await File.ReadAllTextAsync(Path.Combine(env.ContentRootPath, "wwwroot", "index.html")), "text/html; charset=utf-8")).RequireAuthorization();
app.MapGet("/api/v1/health", async (SqliteProfileRepository profiles) =>
{
    var all = await profiles.ListAsync();
    var corePath = Environment.GetEnvironmentVariable("V2RAYWATCH_XRAY_PATH") ?? "/opt/v2raywatch/xray";
    return Results.Ok(new { status = "online", service = "V2rayWatch Server",
        version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.2.0",
        profileCount = all.Count, profileCapacity = SqliteProfileRepository.MaximumProfiles,
        probeEngine = File.Exists(corePath) ? "ready" : "missing", storage = "sqlite-aes-256-gcm" });
}).RequireAuthorization();
app.MapGet("/api/v1/profiles", async (SqliteProfileRepository profiles) =>
    Results.Ok((await profiles.ListAsync()).Select(ToProfileView))).RequireAuthorization();
app.MapPost("/api/v1/profiles/import", async (ImportRequest request, SqliteProfileRepository profiles) =>
{
    if (string.IsNullOrWhiteSpace(request.Text)) return Results.BadRequest(new { error = "Paste one or more supported links or an Xray JSON config." });
    if (request.Text.Length > 2 * 1024 * 1024) return Results.BadRequest(new { error = "Import text exceeds the 2 MiB limit." });
    IReadOnlyList<ServerProfile> parsed;
    try { parsed = ServerProfileParser.ParseMany(request.Text); }
    catch (FormatException ex) { return Results.BadRequest(new { error = ex.Message }); }
    var (imported, ignored, total) = await profiles.ImportAsync(parsed);
    return Results.Ok(new { imported, ignored, total, limit = SqliteProfileRepository.MaximumProfiles });
}).RequireAuthorization();
app.MapDelete("/api/v1/profiles/{id:guid}", async (Guid id, SqliteProfileRepository profiles) =>
    await profiles.DeleteAsync(id) ? Results.NoContent() : Results.NotFound()).RequireAuthorization();
app.MapPost("/api/v1/profiles/{id:guid}/test", async (Guid id, SqliteProfileRepository profiles, IProbeEngine probe, HttpContext context) =>
{
    var profile = await profiles.GetAsync(id, context.RequestAborted);
    if (profile is null) return Results.NotFound();
    var result = await probe.TestAsync(profile, new MonitorSettings(), context.RequestAborted);
    profile.LastCheckedUtc = DateTimeOffset.UtcNow; profile.DelayMs = result.DelayMs;
    profile.Status = result.Success ? "Connected" : "Disconnected";
    profile.ConsecutiveFailures = result.Success ? 0 : profile.ConsecutiveFailures + 1;
    await profiles.UpdateAsync(profile, context.RequestAborted);
    return Results.Ok(new { profileId = profile.Id, success = result.Success, delayMs = result.DelayMs,
        status = profile.Status, error = result.Error, checkedAtUtc = profile.LastCheckedUtc });
}).RequireAuthorization();
app.Run();
static ProfileView ToProfileView(ServerProfile p) => new(p.Id, p.Name, p.Protocol, p.Host, p.Port, p.Status, p.DelayMs, p.LastCheckedUtc, p.ImportWarning);
public sealed record LoginRequest(string Password);
public sealed record ImportRequest(string? Text);
public sealed record ProfileView(Guid Id, string Name, string Protocol, string Host, int Port, string Status, long? DelayMs, DateTimeOffset? LastCheckedUtc, string ImportWarning);
public partial class Program { }
