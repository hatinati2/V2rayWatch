using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var configPath = Environment.GetEnvironmentVariable("V2RAYWATCH_CONFIG") ?? "/etc/v2raywatch/server.json";
builder.Configuration.AddJsonFile(configPath, optional: true, reloadOnChange: false);
var server = builder.Configuration.GetSection("Server").Get<V2rayWatch.Server.Web.ServerOptions>() ?? new();
server.Validate();

var password = Environment.GetEnvironmentVariable("V2RAYWATCH_ADMIN_PASSWORD");
if (string.IsNullOrWhiteSpace(password) || password.Length < 20)
    throw new InvalidOperationException("V2RAYWATCH_ADMIN_PASSWORD is missing or too short.");

var salt = RandomNumberGenerator.GetBytes(16);
var expectedHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA256, 32);
builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Parse(server.ListenAddress), server.Port));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "V2rayWatch.Server.Auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.Cookie.Path = server.BasePath;
    o.ExpireTimeSpan = TimeSpan.FromHours(12);
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c =>
    {
        if (c.Request.Path.StartsWithSegments("/api")) { c.Response.StatusCode = 401; return Task.CompletedTask; }
        c.Response.Redirect(c.RedirectUri);
        return Task.CompletedTask;
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
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/login", async (IWebHostEnvironment env) =>
    Results.Content(await File.ReadAllTextAsync(Path.Combine(env.ContentRootPath, "wwwroot", "login.html")), "text/html; charset=utf-8"))
    .AllowAnonymous();

app.MapPost("/api/v1/login", async (LoginRequest request, HttpContext ctx) =>
{
    if (string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024) return Results.Unauthorized();
    var candidate = Rfc2898DeriveBytes.Pbkdf2(request.Password, salt, 210_000, HashAlgorithmName.SHA256, 32);
    if (!CryptographicOperations.FixedTimeEquals(expectedHash, candidate)) return Results.Unauthorized();
    var identity = new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, "Administrator") },
        CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return Results.Ok(new { authenticated = true });
}).AllowAnonymous().RequireRateLimiting("login");

app.MapPost("/api/v1/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { authenticated = false });
}).RequireAuthorization();

app.MapGet("/", async (IWebHostEnvironment env) =>
    Results.Content(await File.ReadAllTextAsync(Path.Combine(env.ContentRootPath, "wwwroot", "index.html")), "text/html; charset=utf-8"))
    .RequireAuthorization();

app.MapGet("/api/v1/health", () => Results.Ok(new
{
    status = "online",
    service = "V2rayWatch Server",
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0",
    profileCapacityTarget = 100,
    probeEngine = "not-configured",
    storage = "not-configured"
})).RequireAuthorization();

app.Run();
public sealed record LoginRequest(string Password);
public partial class Program;
