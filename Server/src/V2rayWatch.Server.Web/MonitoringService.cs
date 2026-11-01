using System.Collections.Concurrent;
using System.Diagnostics;
using V2rayWatch.Server.Core;
using V2rayWatch.Server.Engine;

namespace V2rayWatch.Server.Web;

/// <summary>
/// Server-side monitor. The enabled flag is stored in SQLite, so an explicitly
/// enabled monitor resumes after a systemd restart or machine reboot.
/// </summary>
public sealed class MonitoringService : BackgroundService
{
    private readonly SqliteProfileRepository _profiles;
    private readonly SqliteServerStateRepository _state;
    private readonly IProbeEngine _probe;
    private readonly GmailNotificationService _notifications;
    private readonly ILogger<MonitoringService> _logger;

    public MonitoringService(
        SqliteProfileRepository profiles,
        SqliteServerStateRepository state,
        IProbeEngine probe,
        GmailNotificationService notifications,
        ILogger<MonitoringService> logger)
    {
        _profiles = profiles;
        _state = state;
        _probe = probe;
        _notifications = notifications;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Guid? sessionId = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var appSettings = await _state.GetSettingsAsync(stoppingToken).ConfigureAwait(false);
                if (!appSettings.MonitoringEnabled)
                {
                    sessionId = null;
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                sessionId ??= Guid.NewGuid();
                var settings = appSettings.Settings;
                var cycle = Stopwatch.StartNew();
                var profiles = await _profiles.ListAsync(stoppingToken).ConfigureAwait(false);
                if (profiles.Count > 0)
                {
                    await Parallel.ForEachAsync(
                        profiles,
                        new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = stoppingToken },
                        async (profile, token) =>
                        {
                            try
                            {
                                var previousStatus = profile.Status;
                                var result = await _probe.TestAsync(profile, settings, token).ConfigureAwait(false);
                                var checkedAt = DateTimeOffset.UtcNow;
                                profile.LastCheckedUtc = checkedAt;
                                if (result.Success)
                                {
                                    profile.DelayMs = result.DelayMs;
                                    profile.ConsecutiveFailures = 0;
                                    profile.Status = "Connected";
                                }
                                else
                                {
                                    profile.DelayMs = null;
                                    profile.ConsecutiveFailures++;
                                    profile.Status = profile.ConsecutiveFailures < settings.ConsecutiveFailureThreshold
                                        ? "Unstable" : "Disconnected";
                                }

                                profile.RecordRecentCheck(result.Success);
                                await _profiles.UpdateAsync(profile, token).ConfigureAwait(false);
                                await _state.RecordHistoryAsync(
                                    profile.Id, sessionId!.Value, checkedAt, result.Success, result.DelayMs, token)
                                    .ConfigureAwait(false);

                                if (profile.Status != previousStatus)
                                {
                                    _logger.LogInformation("Profile {ProfileId} changed from {Previous} to {Current}",
                                        profile.Id, previousStatus, profile.Status);
                                    await _notifications.NotifyStatusChangeAsync(
                                        profile, previousStatus, profile.Status, settings, token).ConfigureAwait(false);
                                }
                            }
                            catch (OperationCanceledException) when (token.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Automatic check failed for profile {ProfileId}", profile.Id);
                            }
                        }).ConfigureAwait(false);
                }

                var delay = TimeSpan.FromSeconds(Math.Clamp(settings.IntervalSeconds, 1, 9999)) - cycle.Elapsed;
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background monitor cycle failed");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
