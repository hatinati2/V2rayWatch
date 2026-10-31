package com.linkwatch.app.data

data class NotificationEvents(val disconnected: Boolean, val recovered: Boolean)

fun notificationEvents(transition: LinkCheckTransition, settings: MonitorSettings) =
    NotificationEvents(
        disconnected = transition.becameDisconnected && settings.notifyOnDisconnect,
        recovered = transition.recovered && settings.notifyOnRecovery
    )

fun formatOutageDuration(durationMs: Long, isEnglish: Boolean): String {
    val seconds = durationMs.coerceAtLeast(0L) / 1_000L
    val days = seconds / 86_400L
    val hours = (seconds % 86_400L) / 3_600L
    val minutes = (seconds % 3_600L) / 60L
    val remainder = seconds % 60L
    return when {
        days > 0 -> if (isEnglish) "${days}d ${hours}h" else "${days} روز و ${hours} ساعت"
        hours > 0 -> if (isEnglish) "${hours}h ${minutes}m" else "${hours} ساعت و ${minutes} دقیقه"
        minutes > 0 -> if (isEnglish) "${minutes}m ${remainder}s" else "${minutes} دقیقه و ${remainder} ثانیه"
        else -> if (isEnglish) "${remainder}s" else "${remainder} ثانیه"
    }
}
