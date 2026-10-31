package com.linkwatch.app.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Test

class ProfileBackupMergerTest {
    private fun profile(
        id: String,
        rawLink: String,
        state: LinkState = LinkState.DISCONNECTED,
        history: List<UptimeMark> = emptyList()
    ) = ProxyProfile(
        id = id,
        name = "Test $id",
        protocol = "VLESS",
        host = "example.com",
        port = 443,
        rawLink = rawLink,
        configJson = "{\"outbounds\":[]}",
        state = state,
        delayMs = 123,
        lastCheckedAt = 1234,
        consecutiveFailures = 3,
        lastError = "old error",
        uptimeHistory = history
    )

    @Test
    fun restoreSkipsDuplicateRawLinksAndPreservesExistingProfile() {
        val existing = profile("existing", "vless://same", state = LinkState.CONNECTED)
        val importedDuplicate = profile("backup-copy", "vless://same")

        val result = mergeBackedUpProfiles(listOf(existing), listOf(importedDuplicate))

        assertEquals(1, result.profiles.size)
        assertEquals(0, result.addedCount)
        assertEquals(1, result.skippedCount)
        assertEquals(existing, result.profiles.single())
    }

    @Test
    fun restoreResetsLiveStateButKeepsNewestTwentyHistoryMarks() {
        val history = (1..25).map {
            if (it % 2 == 0) UptimeMark.CONNECTED else UptimeMark.DISCONNECTED
        }
        val imported = profile("from-backup", "vless://new", history = history)

        val result = mergeBackedUpProfiles(emptyList(), listOf(imported))
        val restored = result.profiles.single()

        assertEquals(1, result.addedCount)
        assertEquals(0, result.skippedCount)
        assertEquals(LinkState.UNKNOWN, restored.state)
        assertEquals(-1L, restored.delayMs)
        assertEquals(0L, restored.lastCheckedAt)
        assertEquals(0, restored.consecutiveFailures)
        assertEquals("", restored.lastError)
        assertEquals(history.takeLast(UPTIME_HISTORY_LIMIT), restored.uptimeHistory)
    }

    @Test
    fun restoreCreatesANewIdWhenImportedIdConflictsWithDifferentProfile() {
        val existing = profile("shared-id", "vless://existing")
        val imported = profile("shared-id", "vless://new")

        val result = mergeBackedUpProfiles(listOf(existing), listOf(imported))

        assertEquals(2, result.profiles.size)
        assertEquals("shared-id", result.profiles.first().id)
        assertNotEquals("shared-id", result.profiles.last().id)
    }
}