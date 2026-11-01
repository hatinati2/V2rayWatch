package com.linkwatch.app.monitor

import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.media.AudioAttributes
import android.net.Uri
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import com.linkwatch.app.AppLanguage
import com.linkwatch.app.MainActivity
import com.linkwatch.app.R
import com.linkwatch.app.uiText
import com.linkwatch.app.uiError
import com.linkwatch.app.data.formatOutageDuration

object NotificationHelper {
    const val CHANNEL_MONITOR = "linkwatch_monitor"
    private const val CHANNEL_DISCONNECTED = "linkwatch_alert_disconnected_v2"
    private const val CHANNEL_RECOVERED = "linkwatch_alert_recovered_v2"
    const val ONGOING_ID = 4101
    private const val ALERT_ID = 4102

    private val disconnectVibration = longArrayOf(
        0L, 900L, 260L, 900L, 260L, 900L, 420L, 1_100L
    )
    private val recoveryVibration = longArrayOf(
        0L, 420L, 220L, 420L, 220L, 620L
    )

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

        val soundAttributes = AudioAttributes.Builder()
            .setUsage(AudioAttributes.USAGE_NOTIFICATION_EVENT)
            .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
            .build()

        manager.createNotificationChannel(
            NotificationChannel(
                CHANNEL_DISCONNECTED,
                uiText(language, "هشدار صوتی قطع اتصال", "Disconnect warning sound"),
                NotificationManager.IMPORTANCE_HIGH
            ).apply {
                description = uiText(
                    language,
                    "آهنگ هشدار اختصاصی و ویبره طولانی هنگام قطع شدن لینک",
                    "Distinct warning melody and extended vibration when a link disconnects"
                )
                setSound(
                    Uri.parse("android.resource://${context.packageName}/${R.raw.disconnect_warning}"),
                    soundAttributes
                )
                enableVibration(true)
                setVibrationPattern(disconnectVibration)
                setShowBadge(true)
            }
        )

        manager.createNotificationChannel(
            NotificationChannel(
                CHANNEL_RECOVERED,
                uiText(language, "آهنگ آرام اتصال مجدد", "Gentle reconnection melody"),
                NotificationManager.IMPORTANCE_DEFAULT
            ).apply {
                description = uiText(
                    language,
                    "ملودی آرام و ویبره ملایم هنگام برقراری دوباره اتصال",
                    "Gentle melody and vibration when the connection is restored"
                )
                setSound(
                    Uri.parse("android.resource://${context.packageName}/${R.raw.connection_restored}"),
                    soundAttributes
                )
                enableVibration(true)
                setVibrationPattern(recoveryVibration)
                setShowBadge(true)
            }
        )
    }

    fun ongoing(context: Context, checked: Int, total: Int) = NotificationCompat.Builder(context, CHANNEL_MONITOR)
        .setSmallIcon(android.R.drawable.stat_notify_sync)
        .setContentTitle(uiText(lang(context), "V2rayWatch در حال پایش است", "V2rayWatch is monitoring"))
        .setContentText(uiText(lang(context), "$checked از $total لینک بررسی شده‌اند", "$checked of $total links checked"))
        .setSubText(uiText(
            lang(context),
            "بررسی با صفحهٔ خاموش فعال است؛ مصرف باتری بیشتر می‌شود",
            "Screen-off checks are active; battery use may increase"
        ))
        .setContentIntent(openAppIntent(context))
        .setOngoing(true)
        .setOnlyAlertOnce(true)
        .setCategory(NotificationCompat.CATEGORY_SERVICE)
        .setPriority(NotificationCompat.PRIORITY_LOW)
        .build()

    fun notifyDisconnected(context: Context, profileId: String, profileName: String, reason: String) {
        val language = lang(context)
        val fallback = uiText(language, "تست اتصال واقعی ناموفق بود", "Real connection test failed")
        val notification = NotificationCompat.Builder(context, CHANNEL_DISCONNECTED)
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
        val expanded = if (outage == null) content else "$content\n$delayText"
        val notification = NotificationCompat.Builder(context, CHANNEL_RECOVERED)
            .setSmallIcon(android.R.drawable.stat_sys_download_done)
            .setContentTitle(uiText(language, "اتصال دوباره برقرار شد: $profileName", "Connection restored: $profileName"))
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
