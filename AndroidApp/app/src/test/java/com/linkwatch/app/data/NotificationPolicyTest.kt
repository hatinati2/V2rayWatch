package com.linkwatch.app.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Test

class NotificationPolicyTest {
    private val profile = ProxyProfile(
        name = "Test", protocol = "vless", host = "example.com", port = 443,
        rawLink = "vless://test@example.com:443", configJson = "{}"
    )

    @Test
    fun transientFailureDoesNotCreateDisconnectAlert() {
        val settings = MonitorSettings(failureThreshold = 2)
        val transition = reduceLinkCheck(profile, DelayResult(false, error = "timeout"), settings, 1_000L)
        val events = notificationEvents(transition, settings)
        assertFalse(events.disconnected)
        assertFalse(events.recovered)
    }

    @Test
    fun disconnectAndRecoveryAlertsCanBeDisabledIndependently() {
        val down = LinkCheckTransition(profile, becameDisconnected = true, recovered = false)
        assertFalse(notificationEvents(down, MonitorSettings(notifyOnDisconnect = false)).disconnected)
        val recovered = LinkCheckTransition(profile, becameDisconnected = false, recovered = true)
        assertFalse(notificationEvents(recovered, MonitorSettings(notifyOnRecovery = false)).recovered)
    }

    @Test
    fun outageDurationFormattingIsLocalizedAndClamped() {
        assertEquals("45s", formatOutageDuration(45_999L, true))
        assertEquals("2m 5s", formatOutageDuration(125_000L, true))
        assertEquals("1h 1m", formatOutageDuration(3_661_000L, true))
        assertEquals("1 دقیقه و 5 ثانیه", formatOutageDuration(65_000L, false))
        assertEquals("0s", formatOutageDuration(-1L, true))
    }
}
