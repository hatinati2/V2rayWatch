using V2rayWatch.Server.Core;

namespace V2rayWatch.Server.Engine;

/// <summary>Contract for an isolated real-traffic probe. The Linux Xray adapter follows in a later milestone.</summary>
public interface IProbeEngine
{
    Task<ProbeResult> CheckAsync(ServerProfile profile, CancellationToken cancellationToken = default);
}
