package com.linkwatch.app.data

data class LinkCheckTransition(
    val profile: ProxyProfile,
    val becameDisconnected: Boolean,
    val recovered: Boolean,
    val outageDurationMs: Long? = null
)

/**
 * A CHECKING state is transient. If the process died during a probe, show the
 * profile as UNKNOWN on the next launch instead of leaving it stuck forever.
 */
fun recoverInterruptedCheck(profile: ProxyProfile): ProxyProfile =
    if (profile.state == LinkState.CHECKING) profile.copy(state = LinkState.UNKNOWN) else profile

/**
 * Applies one completed probe to the latest profile snapshot.
 *
 * Keeping this reducer pure makes failure-threshold and uptime-history behavior
 * deterministic and independently unit-testable.
 */
fun reduceLinkCheck(
    previous: ProxyProfile,
    result: DelayResult,
    settings: MonitorSettings,
    checkedAtEpochMs: Long
): LinkCheckTransition {
    val failures = if (result.success) 0 else previous.consecutiveFailures + 1
    val nextState = when {
        result.success -> LinkState.CONNECTED
        failures >= settings.failureThreshold -> LinkState.DISCONNECTED
        else -> LinkState.UNSTABLE
    }
    val becameDisconnected = !result.success && nextState == LinkState.DISCONNECTED && previous.state != LinkState.DISCONNECTED
    val recovered = result.success && previous.state == LinkState.DISCONNECTED
    val disconnectedSince = when {
        nextState == LinkState.DISCONNECTED -> previous.disconnectedSinceEpochMs ?: previous.lastCheckedAt.takeIf { it > 0L } ?: checkedAtEpochMs
        result.success -> null
        else -> previous.disconnectedSinceEpochMs
    }
    val outageStart = previous.disconnectedSinceEpochMs ?: previous.lastCheckedAt.takeIf { it > 0L }
    val outageDuration = if (recovered && outageStart != null) (checkedAtEpochMs - outageStart).coerceAtLeast(0L) else null
    val updated = previous.copy(
        state = nextState,
        disconnectedSinceEpochMs = disconnectedSince,
        delayMs = if (result.success) result.delayMs else -1L,
        lastCheckedAt = checkedAtEpochMs,
        consecutiveFailures = failures,
        lastError = if (result.success) "" else result.error,
        uptimeHistory = appendUptimeMark(
            previous.uptimeHistory.orEmpty(),
            if (result.success) UptimeMark.CONNECTED else UptimeMark.DISCONNECTED
        )
    )
    return LinkCheckTransition(
        profile = updated,
        becameDisconnected = becameDisconnected,
        recovered = recovered,
        outageDurationMs = outageDuration
    )
}
