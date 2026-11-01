using V2rayWatch.Server.Core;

namespace V2rayWatch.Server.Engine;

public interface IProbeEngine
{
    Task<DelayTestResult> TestAsync(
        ServerProfile profile,
        MonitorSettings settings,
        CancellationToken cancellationToken = default);
}
