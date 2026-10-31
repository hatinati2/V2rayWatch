package com.linkwatch.app.data

import java.util.UUID

const val DEFAULT_CHECK_INTERVAL_SECONDS = 20
const val DEFAULT_FAILURE_THRESHOLD = 2
const val DEFAULT_PRIMARY_TEST_URL = "https://www.google.com/generate_204"
const val DEFAULT_FALLBACK_TEST_URL = "https://www.gstatic.com/generate_204"
const val UPTIME_HISTORY_LIMIT = 20

enum class UptimeMark {
    UNKNOWN,
    CONNECTED,
    DISCONNECTED
}

fun appendUptimeMark(history: List<UptimeMark>, mark: UptimeMark): List<UptimeMark> =
    (history.takeLast(UPTIME_HISTORY_LIMIT) + mark).takeLast(UPTIME_HISTORY_LIMIT)

fun uptimeHistorySlots(history: List<UptimeMark>): List<UptimeMark> {
    val recent = history.takeLast(UPTIME_HISTORY_LIMIT)
    return List((UPTIME_HISTORY_LIMIT - recent.size).coerceAtLeast(0)) { UptimeMark.UNKNOWN } + recent
}

enum class LinkState {
    UNKNOWN,
    CHECKING,
    CONNECTED,
    UNSTABLE,
    DISCONNECTED,
    UNSUPPORTED
}

data class ProxyProfile(
    val id: String = UUID.randomUUID().toString(),
    val name: String,
    val protocol: String,
    val host: String,
    val port: Int,
    val rawLink: String,
    val configJson: String,
    val state: LinkState = LinkState.UNKNOWN,
    val delayMs: Long = -1L,
    val lastCheckedAt: Long = 0L,
    val consecutiveFailures: Int = 0,
    val lastError: String = "",
    val uptimeHistory: List<UptimeMark> = emptyList()
)

data class DelayResult(
    val success: Boolean,
    val delayMs: Long = -1L,
    val error: String = ""
)

data class MonitorSettings(
    val intervalSeconds: Int = DEFAULT_CHECK_INTERVAL_SECONDS,
    val failureThreshold: Int = DEFAULT_FAILURE_THRESHOLD,
    val primaryTestUrl: String = DEFAULT_PRIMARY_TEST_URL,
    val fallbackTestUrl: String = DEFAULT_FALLBACK_TEST_URL
)
