package com.linkwatch.app.monitor

import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import com.linkwatch.app.AppLanguage
import com.linkwatch.app.MainActivity
import com.linkwatch.app.uiText
import com.linkwatch.app.uiError
import com.linkwatch.app.data.formatOutageDuration

object NotificationHelper {
    const val CHANNEL_MONITOR = "linkwatch_monitor"
    const val CHANNEL_ALERTS = "linkwatch_alerts"
    const val ONGOING_ID = 4101
    private const val ALERT_ID = 4102

    private fun lang(context: Context) = AppLanguage.load(context)

    fun createChannels(context: Context) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val language = lang(context)
        val manager = context.getSystemService(NotificationManager::class.java)
        manager.createNotificationChannel(
            NotificationChannel(
                CHANNEL_MONITOR,
                uiText(language, "وضعیت پایش", "Monitoring status"),
                NotificationManager.IMPORTANCE_LOW
            ).apply {
                description = uiText(language, "اعلان دائمی هنگام فعال بودن پایش لینک‌ها", "Persistent notification while link monitoring is active")
                setShowBadge(false)
            }
        )
        manager.createNotificationChannel(
            NotificationChannel(
                CHANNEL_ALERTS,
                uiText(language, "هشدار قطع و وصل لینک", "Connection alerts"),
                NotificationManager.IMPORTANCE_HIGH
            ).apply {
                description = uiText(language, "هشدار هنگام قطع شدن یا بازیابی اتصال واقعی پروکسی", "Alerts when a proxy connection goes down or recovers")
                enableVibration(true)
                setShowBadge(true)
            }
        )
    }

    fun ongoing(context: Context, checked: Int, total: Int) = NotificationCompat.Builder(context, CHANNEL_MONITOR)
        .setSmallIcon(android.R.drawable.stat_notify_sync)
        .setContentTitle(uiText(lang(context), "V2rayWatch در حال پایش است", "V2rayWatch is monitoring"))
        .setContentText(uiText(lang(context), "$checked از $total لینک بررسی شده‌اند", "$checked of $total links checked"))
        .setContentIntent(openAppIntent(context))
        .setOngoing(true)
        .setOnlyAlertOnce(true)
        .setCategory(NotificationCompat.CATEGORY_SERVICE)
        .setPriority(NotificationCompat.PRIORITY_LOW)
        .build()

    fun notifyDisconnected(context: Context, profileId: String, profileName: String, reason: String) {
        val language = lang(context)
        val fallback = uiText(language, "تست اتصال واقعی ناموفق بود", "Real connection test failed")
        val notification = NotificationCompat.Builder(context, CHANNEL_ALERTS)
            .setSmallIcon(android.R.drawable.stat_notify_error)
            .setContentTitle(uiText(language, "لینک قطع شده است: $profileName", "Link is down: $profileName"))
            .setContentText(if (reason.isBlank()) fallback else uiError(language, reason))
            .setStyle(NotificationCompat.BigTextStyle().bigText("$profileName\n${if (reason.isBlank()) fallback else uiError(language, reason)}"))
            .setContentIntent(openAppIntent(context))
            .setAutoCancel(true)
            .setCategory(NotificationCompat.CATEGORY_ERROR)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .build()
        runCatching { NotificationManagerCompat.from(context).notify(profileId, ALERT_ID, notification) }
    }

    fun notifyRecovered(context: Context, profileId: String, profileName: String, delayMs: Long, outageDurationMs: Long?) {
        val language = lang(context)
        val delayText = uiText(language, "تاخیر واقعی: ${delayMs} ms", "Real Delay: ${delayMs} ms")
        val outage = outageDurationMs?.let { formatOutageDuration(it, language.isEnglish) }
        val content = outage?.let { uiText(language, "مدت قطعی: $it", "Outage duration: $it") } ?: delayText
        val expanded = if (outage == null) content else "$content\\n$delayText"
        val notification = NotificationCompat.Builder(context, CHANNEL_ALERTS)
            .setSmallIcon(android.R.drawable.stat_sys_download_done)
            .setContentTitle(uiText(lang(context), "اتصال دوباره برقرار شد: $profileName", "Connection restored: $profileName"))
            .setContentText(content)
            .setStyle(NotificationCompat.BigTextStyle().bigText(expanded))
            .setContentIntent(openAppIntent(context))
            .setAutoCancel(true)
            .setCategory(NotificationCompat.CATEGORY_STATUS)
            .setPriority(NotificationCompat.PRIORITY_DEFAULT)
            .build()
        runCatching { NotificationManagerCompat.from(context).notify(profileId, ALERT_ID, notification) }
    }

    private fun openAppIntent(context: Context): PendingIntent {
        val intent = Intent(context, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP
        }
        return PendingIntent.getActivity(
            context,
            0,
            intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )
    }
}
