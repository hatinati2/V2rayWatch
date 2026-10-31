package com.linkwatch.app.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class LinkCheckReducerTest {
    private val settings = MonitorSettings(failureThreshold = 2)
    private val profile = ProxyProfile(
        name = "Test",
        protocol = "vless",
        host = "example.com",
        port = 443,
        rawLink = "vless://test@example.com:443",
        configJson = "{}"
    )

    @Test
    fun firstFailureIsUnstableAndDoesNotNotifyDisconnect() {
        val transition = reduceLinkCheck(
            profile,
            DelayResult(success = false, error = "timeout"),
            settings,
            checkedAtEpochMs = 100L
        )

        assertEquals(LinkState.UNSTABLE, transition.profile.state)
        assertEquals(1, transition.profile.consecutiveFailures)
        assertEquals(-1L, transition.profile.delayMs)
        assertEquals("timeout", transition.profile.lastError)
        assertEquals(listOf(UptimeMark.DISCONNECTED), transition.profile.uptimeHistory)
        assertFalse(transition.becameDisconnected)
        assertFalse(transition.recovered)
    }

    @Test
    fun reachingFailureThresholdTransitionsToDisconnectedOnce() {
        val first = reduceLinkCheck(
            profile,
            DelayResult(success = false, error = "timeout"),
            settings,
            100L
        ).profile
        val second = reduceLinkCheck(
            first,
            DelayResult(success = false, error = "timeout"),
            settings,
            200L
        )

        assertEquals(LinkState.DISCONNECTED, second.profile.state)
        assertEquals(2, second.profile.consecutiveFailures)
        assertTrue(second.becameDisconnected)
        assertFalse(second.recovered)
    }

    @Test
    fun repeatedFailureWhileDisconnectedDoesNotCreateAnotherTransition() {
        val disconnected = profile.copy(
            state = LinkState.DISCONNECTED,
            consecutiveFailures = 2
        )
        val transition = reduceLinkCheck(
            disconnected,
            DelayResult(success = false, error = "still offline"),
            settings,
            300L
        )

        assertEquals(LinkState.DISCONNECTED, transition.profile.state)
        assertFalse(transition.becameDisconnected)
    }

    @Test
    fun successResetsFailuresAndMarksRecovery() {
        val disconnected = profile.copy(
            state = LinkState.DISCONNECTED,
            consecutiveFailures = 4,
            lastError = "timeout"
        )
        val transition = reduceLinkCheck(
            disconnected,
            DelayResult(success = true, delayMs = 123L),
            settings,
            400L
        )

        assertEquals(LinkState.CONNECTED, transition.profile.state)
        assertEquals(0, transition.profile.consecutiveFailures)
        assertEquals(123L, transition.profile.delayMs)
        assertEquals("", transition.profile.lastError)
        assertEquals(listOf(UptimeMark.CONNECTED), transition.profile.uptimeHistory)
        assertFalse(transition.becameDisconnected)
        assertTrue(transition.recovered)
    }

    @Test
    fun historyRemainsBoundedToTwentyEntries() {
        val full = profile.copy(
            uptimeHistory = List(UPTIME_HISTORY_LIMIT) { UptimeMark.CONNECTED }
        )
        val transition = reduceLinkCheck(
            full,
            DelayResult(success = false, error = "timeout"),
            settings,
            500L
        )

        assertEquals(UPTIME_HISTORY_LIMIT, transition.profile.uptimeHistory.size)
        assertEquals(UptimeMark.DISCONNECTED, transition.profile.uptimeHistory.last())
        assertEquals(UPTIME_HISTORY_LIMIT - 1, transition.profile.uptimeHistory.count { it == UptimeMark.CONNECTED })
    }
}
