package com.linkwatch.app.data

import java.util.UUID

data class ProfileMergeResult(
    val profiles: List<ProxyProfile>,
    val addedCount: Int,
    val skippedCount: Int
)

/**
 * Non-destructive restore semantics: preserve all existing profiles, append only
 * profiles with previously unseen raw links, and reset transient connection state.
 */
fun mergeBackedUpProfiles(
    existing: List<ProxyProfile>,
    restored: List<ProxyProfile>
): ProfileMergeResult {
    val merged = existing.toMutableList()
    val knownLinks = existing.map { it.rawLink }.toHashSet()
    var added = 0
    var skipped = 0

    restored.forEach { profile ->
        require(profile.id.isNotBlank() && profile.name.isNotBlank() && profile.protocol.isNotBlank()) {
            "Backup contains an incomplete profile"
        }
        require(profile.rawLink.isNotBlank() && profile.configJson.isNotBlank()) {
            "Backup contains a profile without configuration data"
        }
        require(profile.port in 0..65535) { "Backup contains an invalid profile port" }
        require(profile.configJson.length <= 2 * 1024 * 1024) { "A profile configuration in the backup is too large" }

        if (!knownLinks.add(profile.rawLink)) {
            skipped++
        } else {
            val safeId = if (merged.any { it.id == profile.id }) UUID.randomUUID().toString() else profile.id
            merged.add(
                profile.copy(
                    id = safeId,
                    state = LinkState.UNKNOWN,
                    delayMs = -1L,
                    lastCheckedAt = 0L,
                    consecutiveFailures = 0,
                    lastError = "",
                    uptimeHistory = profile.uptimeHistory.orEmpty().takeLast(UPTIME_HISTORY_LIMIT)
                )
            )
            added++
        }
    }

    return ProfileMergeResult(merged, added, skipped)
}