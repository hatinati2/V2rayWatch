package com.linkwatch.app.data

import kotlin.math.roundToLong

enum class ConnectionHistoryRange(val durationMs: Long, val bucketCount: Int) {
    LAST_24_HOURS(24L * 60L * 60L * 1000L, 24),
    LAST_7_DAYS(7L * 24L * 60L * 60L * 1000L, 7);

    val bucketDurationMs: Long get() = durationMs / bucketCount
}

data class ConnectionHistorySample(
    val sessionId: String,
    val checkedAtEpochMs: Long,
    val success: Boolean,
    val delayMs: Long?
)

data class ConnectionHistoryBucket(
    val startEpochMs: Long,
    val totalChecks: Int,
    val successfulChecks: Int,
    val failedChecks: Int
)

data class ConnectionHistorySummary(
    val range: ConnectionHistoryRange,
    val startEpochMs: Long,
    val endEpochMs: Long,
    val totalChecks: Int,
    val successCount: Int,
    val failureCount: Int,
    val successRatePercent: Double?,
    val averageDelayMs: Long?,
    val minimumDelayMs: Long?,
    val maximumDelayMs: Long?,
    val longestFailureRunCount: Int,
    val longestObservedFailureSpanMs: Long,
    val buckets: List<ConnectionHistoryBucket>
)

/**
 * Summarizes only probes that were actually recorded while monitoring was active.
 * Failure runs are isolated by monitoring session; separate starts are never joined.
 */
fun analyzeConnectionHistory(
    samples: List<ConnectionHistorySample>,
    range: ConnectionHistoryRange,
    nowEpochMs: Long
): ConnectionHistorySummary {
    val start = nowEpochMs - range.durationMs
    val relevant = samples
        .asSequence()
        .filter { it.checkedAtEpochMs in start..nowEpochMs }
        .sortedWith(compareBy<ConnectionHistorySample> { it.checkedAtEpochMs }.thenBy { it.sessionId })
        .toList()

    val bucketTotals = IntArray(range.bucketCount)
    val bucketSuccess = IntArray(range.bucketCount)
    val bucketFailure = IntArray(range.bucketCount)
    val successfulDelays = relevant.filter { it.success && (it.delayMs ?: -1L) >= 0L }
        .mapNotNull { it.delayMs }

    relevant.forEach { sample ->
        val index = ((sample.checkedAtEpochMs - start) / range.bucketDurationMs)
            .toInt().coerceIn(0, range.bucketCount - 1)
        bucketTotals[index]++
        if (sample.success) bucketSuccess[index]++ else bucketFailure[index]++
    }

    var currentSession: String? = null
    var currentFailureCount = 0
    var currentFailureStart = 0L
    var currentFailureEnd = 0L
    var longestFailureCount = 0
    var longestFailureSpan = 0L

    fun finishFailureRun() {
        if (currentFailureCount > longestFailureCount ||
            (currentFailureCount == longestFailureCount && currentFailureCount > 0 &&
                currentFailureEnd - currentFailureStart > longestFailureSpan)
        ) {
            longestFailureCount = currentFailureCount
            longestFailureSpan = (currentFailureEnd - currentFailureStart).coerceAtLeast(0L)
        }
        currentFailureCount = 0
        currentFailureStart = 0L
        currentFailureEnd = 0L
    }

    relevant.forEach { sample ->
        if (currentSession != sample.sessionId) {
            finishFailureRun()
            currentSession = sample.sessionId
        }
        if (sample.success) {
            finishFailureRun()
        } else {
            if (currentFailureCount == 0) currentFailureStart = sample.checkedAtEpochMs
            currentFailureCount++
            currentFailureEnd = sample.checkedAtEpochMs
        }
    }
    finishFailureRun()

    return ConnectionHistorySummary(
        range = range,
        startEpochMs = start,
        endEpochMs = nowEpochMs,
        totalChecks = relevant.size,
        successCount = relevant.count { it.success },
        failureCount = relevant.count { !it.success },
        successRatePercent = if (relevant.isEmpty()) null else
            relevant.count { it.success }.toDouble() * 100.0 / relevant.size,
        averageDelayMs = successfulDelays.takeIf { it.isNotEmpty() }?.average()?.roundToLong(),
        minimumDelayMs = successfulDelays.minOrNull(),
        maximumDelayMs = successfulDelays.maxOrNull(),
        longestFailureRunCount = longestFailureCount,
        longestObservedFailureSpanMs = longestFailureSpan,
        buckets = List(range.bucketCount) { index ->
            ConnectionHistoryBucket(
                startEpochMs = start + index * range.bucketDurationMs,
                totalChecks = bucketTotals[index],
                successfulChecks = bucketSuccess[index],
                failedChecks = bucketFailure[index]
            )
        }
    )
}
