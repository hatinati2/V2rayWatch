package com.linkwatch.app.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class ConnectionHistoryTest {
    private val now = 2_000_000_000_000L

    @Test
    fun emptyHistoryReturnsEmptyBucketsWithoutInventingOutages() {
        val summary = analyzeConnectionHistory(emptyList(), ConnectionHistoryRange.LAST_24_HOURS, now)

        assertEquals(24, summary.buckets.size)
        assertEquals(0, summary.totalChecks)
        assertNull(summary.successRatePercent)
        assertEquals(0, summary.longestFailureRunCount)
        assertEquals(0L, summary.longestObservedFailureSpanMs)
    }

    @Test
    fun twentyFourHourSummaryCountsSamplesAndSuccessfulDelaysOnly() {
        val samples = listOf(
            ConnectionHistorySample("session-a", now - 23L * 60L * 60L * 1000L, true, 100L),
            ConnectionHistorySample("session-a", now - 22L * 60L * 60L * 1000L, false, null),
            ConnectionHistorySample("session-a", now - 21L * 60L * 60L * 1000L, true, 300L),
            ConnectionHistorySample("session-a", now - 25L * 60L * 60L * 1000L, false, null),
            ConnectionHistorySample("session-a", now + 1_000L, false, null)
        )

        val summary = analyzeConnectionHistory(samples, ConnectionHistoryRange.LAST_24_HOURS, now)

        assertEquals(3, summary.totalChecks)
        assertEquals(2, summary.successCount)
        assertEquals(1, summary.failureCount)
        assertEquals(2.0 / 3.0 * 100.0, summary.successRatePercent!!, 0.001)
        assertEquals(200L, summary.averageDelayMs)
        assertEquals(100L, summary.minimumDelayMs)
        assertEquals(300L, summary.maximumDelayMs)
        assertEquals(3, summary.buckets.size.coerceAtMost(24))
        assertEquals(3, summary.buckets.sumOf { it.totalChecks })
    }

    @Test
    fun sevenDaySummaryUsesSevenDailyBuckets() {
        val samples = (0..6).map { day ->
            ConnectionHistorySample(
                sessionId = "session-a",
                checkedAtEpochMs = now - (6L - day) * 24L * 60L * 60L * 1000L + 1_000L,
                success = day % 2 == 0,
                delayMs = if (day % 2 == 0) 120L + day else null
            )
        }

        val summary = analyzeConnectionHistory(samples, ConnectionHistoryRange.LAST_7_DAYS, now)

        assertEquals(7, summary.buckets.size)
        assertEquals(7, summary.totalChecks)
        assertEquals(4, summary.successCount)
        assertEquals(3, summary.failureCount)
        assertTrue(summary.buckets.all { it.totalChecks == 1 })
    }

    @Test
    fun failureRunsDoNotCrossSuccessOrMonitoringSessionBoundaries() {
        val samples = listOf(
            ConnectionHistorySample("session-a", now - 60_000L, false, null),
            ConnectionHistorySample("session-a", now - 40_000L, false, null),
            ConnectionHistorySample("session-a", now - 20_000L, true, 150L),
            ConnectionHistorySample("session-a", now - 10_000L, false, null),
            ConnectionHistorySample("session-b", now - 5_000L, false, null),
            ConnectionHistorySample("session-b", now, true, 130L)
        )

        val summary = analyzeConnectionHistory(samples, ConnectionHistoryRange.LAST_24_HOURS, now)

        assertEquals(2, summary.longestFailureRunCount)
        assertEquals(20_000L, summary.longestObservedFailureSpanMs)
    }

    @Test
    fun singleFailureHasZeroMeasuredSpanAndIsNotAnEstimatedDowntime() {
        val summary = analyzeConnectionHistory(
            listOf(ConnectionHistorySample("session-a", now - 1_000L, false, null)),
            ConnectionHistoryRange.LAST_7_DAYS,
            now
        )

        assertEquals(1, summary.longestFailureRunCount)
        assertEquals(0L, summary.longestObservedFailureSpanMs)
        assertEquals(1, summary.totalChecks)
    }
}
