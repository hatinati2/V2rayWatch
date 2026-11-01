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
if (string.IsNullOrWhiteSpace(password) || password.Length < 20)
    throw new InvalidOperationException("V2RAYWATCH_ADMIN_PASSWORD is missing or too short.");

var keyFile = Environment.GetEnvironmentVariable("V2RAYWATCH_PROFILE_KEY_FILE") ?? "/etc/v2raywatch/profile.key";
builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Parse(server.ListenAddress), server.Port));
builder.Services.AddSingleton(server);
builder.Services.AddSingleton(new ProfileSecretProtector(keyFile));
builder.Services.AddSingleton<SqliteProfileRepository>();
builder.Services.AddSingleton<SqliteServerStateRepository>();
builder.Services.AddSingleton<SubscriptionClient>();
builder.Services.AddSingleton<IProbeEngine, XrayDelayTester>();
builder.Services.AddHostedService<MonitoringService>();

var salt = RandomNumberGenerator.GetBytes(16);
var expectedHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA256, 32);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "V2rayWatch.Server.Auth";
    o.LoginPath = "/login";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.Cookie.Path = server.BasePath;
    o.ExpireTimeSpan = TimeSpan.FromHours(12);
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c =>
    {
        if (c.Request.Path.StartsWithSegments("/api"))
        {
            c.Response.StatusCode = 401;
            return Task.CompletedTask;
        }
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
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

var app = builder.Build();
var profiles = app.Services.GetRequiredService<SqliteProfileRepository>();
var state = app.Services.GetRequiredService<SqliteServerStateRepository>();
await profiles.InitializeAsync();
await state.InitializeAsync();
(await state.GetSettingsAsync()).Validate();

app.Use(async (ctx, next) =>
{
    if (string.Equals(ctx.Request.Path.Value, server.BasePath, StringComparison.Ordinal))
    {
        ctx.Response.Redirect(server.BasePath + "/");
        return;
    }
    if (!ctx.Request.Path.StartsWithSegments(server.BasePath, StringComparison.Ordinal))
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
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

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/login", async (IWebHostEnvironment env) =>
    Results.Content(
        await File.ReadAllTextAsync(Path.Combine(env.ContentRootPath, "wwwroot", "login.html")),
        "text/html; charset=utf-8")).AllowAnonymous();

app.MapPost("/api/v1/login", async (LoginRequest request, HttpContext ctx) =>
{
    if (string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024)
        return Results.Unauthorized();

    var candidate = Rfc2898DeriveBytes.Pbkdf2(
        request.Password, salt, 210_000, HashAlgorithmName.SHA256, 32);
    if (!CryptographicOperations.FixedTimeEquals(expectedHash, candidate))
        return Results.Unauthorized();

    var identity = new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, "Administrator") },
        CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity));
    return Results.Ok(new { authenticated = true });
}).AllowAnonymous().RequireRateLimiting("login");

app.MapPost("/api/v1/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { authenticated = false });
}).RequireAuthorization();

app.MapGet("/", async (IWebHostEnvironment env) =>
    Results.Content(
        await File.ReadAllTextAsync(Path.Combine(env.ContentRootPath, "wwwroot", "index.html")),
        "text/html; charset=utf-8")).RequireAuthorization();

app.MapGet("/api/v1/health", async (
    SqliteProfileRepository profileStore,
    SqliteServerStateRepository stateStore) =>
{
    var all = await profileStore.ListAsync();
    var appSettings = await stateStore.GetSettingsAsync();
    var subscriptionItems = await stateStore.ListSubscriptionsAsync();
    var corePath = Environment.GetEnvironmentVariable("V2RAYWATCH_XRAY_PATH") ?? "/opt/v2raywatch/xray";
    return Results.Ok(new
    {
        status = "online",
        service = "V2rayWatch Server",
        version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.4.0",
        profileCount = all.Count,
        profileCapacity = SqliteProfileRepository.MaximumProfiles,
        connectedCount = all.Count(p => p.Status == "Connected"),
        disconnectedCount = all.Count(p => p.Status == "Disconnected"),
        unstableCount = all.Count(p => p.Status == "Unstable"),
        subscriptionCount = subscriptionItems.Count,
        monitoringEnabled = appSettings.MonitoringEnabled,
        probeEngine = File.Exists(corePath) ? "ready" : "missing",
        storage = "sqlite-aes-256-gcm"
    });
}).RequireAuthorization();

app.MapGet("/api/v1/profiles", async (SqliteProfileRepository profileStore) =>
    Results.Ok((await profileStore.ListAsync()).Select(ToProfileView)))
    .RequireAuthorization();

app.MapPost("/api/v1/profiles/import", async (
    ImportRequest request,
    SqliteProfileRepository profileStore) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
        return Results.BadRequest(new { error = "یک یا چند لینک یا پیکربندی Xray وارد کن." });
    if (request.Text.Length > 2 * 1024 * 1024)
        return Results.BadRequest(new { error = "حجم متن ورودی بیش از ۲ مگابایت است." });

    IReadOnlyList<ServerProfile> parsed;
    try { parsed = ServerProfileParser.ParseMany(request.Text); }
    catch (FormatException ex) { return Results.BadRequest(new { error = ex.Message }); }

    var (imported, ignored, total) = await profileStore.ImportAsync(parsed);
    return Results.Ok(new { imported, ignored, total, limit = SqliteProfileRepository.MaximumProfiles });
}).RequireAuthorization();

app.MapDelete("/api/v1/profiles/{id:guid}", async (
    Guid id,
    SqliteProfileRepository profileStore,
    SqliteServerStateRepository stateStore) =>
{
    var removed = await profileStore.DeleteAsync(id);
    if (removed) await stateStore.DeleteProfileHistoryAsync(id);
    return removed ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

app.MapDelete("/api/v1/profiles", async (
    SqliteProfileRepository profileStore,
    SqliteServerStateRepository stateStore) =>
{
    var all = await profileStore.ListAsync();
    foreach (var profile in all)
    {
        await profileStore.DeleteAsync(profile.Id);
        await stateStore.DeleteProfileHistoryAsync(profile.Id);
    }
    return Results.Ok(new { deleted = all.Count });
}).RequireAuthorization();

app.MapPost("/api/v1/profiles/{id:guid}/test", async (
    Guid id,
    SqliteProfileRepository profileStore,
    SqliteServerStateRepository stateStore,
    IProbeEngine probe,
    HttpContext context) =>
{
    var profile = await profileStore.GetAsync(id, context.RequestAborted);
    if (profile is null) return Results.NotFound();

    var settings = (await stateStore.GetSettingsAsync(context.RequestAborted)).Settings;
    var result = await probe.TestAsync(profile, settings, context.RequestAborted);
    profile.LastCheckedUtc = DateTimeOffset.UtcNow;
    profile.DelayMs = result.Success ? result.DelayMs : null;
    profile.Status = result.Success ? "Connected" : "Disconnected";
    profile.ConsecutiveFailures = result.Success ? 0 : profile.ConsecutiveFailures + 1;
    await profileStore.UpdateAsync(profile, context.RequestAborted);

    return Results.Ok(new
    {
        profileId = profile.Id,
        success = result.Success,
        delayMs = result.DelayMs,
        status = profile.Status,
        error = result.Error,
        checkedAtUtc = profile.LastCheckedUtc
    });
}).RequireAuthorization();

app.MapGet("/api/v1/profiles/{id:guid}/history", async (
    Guid id,
    string? period,
    SqliteProfileRepository profileStore,
    SqliteServerStateRepository stateStore,
    CancellationToken cancellationToken) =>
{
    if (await profileStore.GetAsync(id, cancellationToken) is null)
        return Results.NotFound();
    var sevenDays = string.Equals(period, "7d", StringComparison.OrdinalIgnoreCase);
    var from = DateTimeOffset.UtcNow.AddDays(sevenDays ? -7 : -1);
    var samples = await stateStore.GetHistoryAsync(id, from, cancellationToken);
    return Results.Ok(new { period = sevenDays ? "7d" : "24h", samples });
}).RequireAuthorization();

app.MapGet("/api/v1/settings", async (SqliteServerStateRepository stateStore) =>
    Results.Ok(await stateStore.GetSettingsAsync())).RequireAuthorization();

app.MapPut("/api/v1/settings", async (
    MonitorSettings request,
    SqliteServerStateRepository stateStore,
    CancellationToken cancellationToken) =>
{
    var current = await stateStore.GetSettingsAsync(cancellationToken);
    current.Settings = request;
    try { current.Validate(); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
    await stateStore.SaveSettingsAsync(current, cancellationToken);
    return Results.Ok(current);
}).RequireAuthorization();

app.MapGet("/api/v1/monitoring", async (SqliteServerStateRepository stateStore) =>
{
    var current = await stateStore.GetSettingsAsync();
    return Results.Ok(new { enabled = current.MonitoringEnabled });
}).RequireAuthorization();

app.MapPut("/api/v1/monitoring", async (
    MonitoringRequest request,
    SqliteServerStateRepository stateStore,
    CancellationToken cancellationToken) =>
{
    var current = await stateStore.GetSettingsAsync(cancellationToken);
    current.MonitoringEnabled = request.Enabled;
    current.Validate();
    await stateStore.SaveSettingsAsync(current, cancellationToken);
    return Results.Ok(new { enabled = current.MonitoringEnabled });
}).RequireAuthorization();

app.MapGet("/api/v1/subscriptions", async (SqliteServerStateRepository stateStore) =>
    Results.Ok(await stateStore.ListSubscriptionsAsync())).RequireAuthorization();

app.MapPost("/api/v1/subscriptions", async (
    SubscriptionRequest request,
    SubscriptionClient client,
    SqliteServerStateRepository stateStore,
    SqliteProfileRepository profileStore,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Url) || request.Url.Length > 4096)
        return Results.BadRequest(new { error = "آدرس Subscription را وارد کن." });

    var sources = await stateStore.ListSubscriptionsAsync(cancellationToken);
    var existing = sources.FirstOrDefault(s =>
        string.Equals(s.Url, request.Url.Trim(), StringComparison.OrdinalIgnoreCase));
    try
    {
        var body = await client.FetchTextAsync(request.Url, cancellationToken);
        var parsed = ServerProfileParser.ParseMany(body);
        var source = existing ?? new SubscriptionSource
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = string.IsNullOrWhiteSpace(request.Name) ? new Uri(request.Url).Host : request.Name.Trim(),
            Url = request.Url.Trim()
        };
        if (!string.IsNullOrWhiteSpace(request.Name)) source.Name = request.Name.Trim();
        var result = await stateStore.SaveSubscriptionAsync(
            source, parsed, profileStore, replaceExisting: existing is not null, cancellationToken);
        return Results.Ok(new
        {
            sourceId = source.Id,
            source.Name,
            imported = result.Imported,
            ignored = result.Ignored,
            profileCount = result.Owned,
            total = (await profileStore.ListAsync(cancellationToken)).Count
        });
    }
    catch (Exception ex) when (ex is InvalidOperationException or FormatException or HttpRequestException or IOException)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPost("/api/v1/subscriptions/{id}/refresh", async (
    string id,
    SubscriptionClient client,
    SqliteServerStateRepository stateStore,
    SqliteProfileRepository profileStore,
    CancellationToken cancellationToken) =>
{
    var source = await stateStore.GetSubscriptionAsync(id, cancellationToken);
    if (source is null) return Results.NotFound();
    try
    {
        var body = await client.FetchTextAsync(source.Url, cancellationToken);
        var parsed = ServerProfileParser.ParseMany(body);
        var result = await stateStore.SaveSubscriptionAsync(
            source, parsed, profileStore, replaceExisting: true, cancellationToken);
        return Results.Ok(new { imported = result.Imported, ignored = result.Ignored, profileCount = result.Owned });
    }
    catch (Exception ex) when (ex is InvalidOperationException or FormatException or HttpRequestException or IOException)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapDelete("/api/v1/subscriptions/{id}", async (
    string id,
    SqliteServerStateRepository stateStore,
    SqliteProfileRepository profileStore,
    CancellationToken cancellationToken) =>
{
    var removed = await stateStore.DeleteSubscriptionAsync(id, profileStore, cancellationToken);
    return removed ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

app.Run();

static ProfileView ToProfileView(ServerProfile p) => new(
    p.Id, p.Name, p.Protocol, p.Host, p.Port, p.Status, p.DelayMs, p.LastCheckedUtc,
    p.ImportWarning, p.SourceSubscriptionId, p.UptimeHistory ?? [], p.ConsecutiveFailures);

public sealed record LoginRequest(string Password);
public sealed record ImportRequest(string? Text);
public sealed record SubscriptionRequest(string? Name, string? Url);
public sealed record MonitoringRequest(bool Enabled);
public sealed record ProfileView(
    Guid Id,
    string Name,
    string Protocol,
    string Host,
    int Port,
    string Status,
    long? DelayMs,
    DateTimeOffset? LastCheckedUtc,
    string ImportWarning,
    string SourceSubscriptionId,
    IReadOnlyList<string> UptimeHistory,
    int ConsecutiveFailures);

public partial class Program { }
