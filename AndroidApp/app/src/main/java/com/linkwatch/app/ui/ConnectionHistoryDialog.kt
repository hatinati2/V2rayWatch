package com.linkwatch.app.ui

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Divider
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import com.linkwatch.app.AppLanguage
import com.linkwatch.app.data.ConnectionHistoryBucket
import com.linkwatch.app.data.ConnectionHistoryRange
import com.linkwatch.app.data.ConnectionHistorySummary
import com.linkwatch.app.data.ProxyProfile
import com.linkwatch.app.uiText
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import kotlin.math.max

@Composable
fun ConnectionHistoryDialog(
    language: AppLanguage,
    profile: ProxyProfile,
    range: ConnectionHistoryRange,
    summary: ConnectionHistorySummary?,
    loading: Boolean,
    onRangeChange: (ConnectionHistoryRange) -> Unit,
    onDismiss: () -> Unit
) {
    Dialog(
        onDismissRequest = onDismiss,
        properties = DialogProperties(usePlatformDefaultWidth = false)
    ) {
        Surface(
            modifier = Modifier.fillMaxWidth().widthIn(max = 560.dp).padding(horizontal = 14.dp),
            shape = RoundedCornerShape(26.dp),
            color = MaterialTheme.colorScheme.surface,
            tonalElevation = 8.dp
        ) {
            Column(
                modifier = Modifier.fillMaxWidth().verticalScroll(rememberScrollState()).padding(18.dp),
                verticalArrangement = Arrangement.spacedBy(14.dp)
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Column(Modifier.weight(1f)) {
                        Text(
                            uiText(language, "تاریخچهٔ اتصال", "Connection history"),
                            style = MaterialTheme.typography.titleLarge,
                            fontWeight = FontWeight.Bold
                        )
                        Text(
                            profile.name,
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            maxLines = 1
                        )
                    }
                    IconButton(onClick = onDismiss) {
                        Icon(Icons.Outlined.Close, contentDescription = uiText(language, "بستن", "Close"))
                    }
                }

                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    FilterChip(
                        selected = range == ConnectionHistoryRange.LAST_24_HOURS,
                        onClick = { onRangeChange(ConnectionHistoryRange.LAST_24_HOURS) },
                        label = { Text(uiText(language, "۲۴ ساعت", "24 hours")) }
                    )
                    FilterChip(
                        selected = range == ConnectionHistoryRange.LAST_7_DAYS,
                        onClick = { onRangeChange(ConnectionHistoryRange.LAST_7_DAYS) },
                        label = { Text(uiText(language, "۷ روز", "7 days")) }
                    )
                }

                when {
                    summary == null && loading -> {
                        Column(
                            modifier = Modifier.fillMaxWidth().padding(vertical = 28.dp),
                            horizontalAlignment = Alignment.CenterHorizontally,
                            verticalArrangement = Arrangement.spacedBy(10.dp)
                        ) {
                            CircularProgressIndicator()
                            Text(uiText(language, "در حال خواندن تاریخچه…", "Loading history…"))
                        }
                    }
                    summary == null || summary.totalChecks == 0 -> {
                        EmptyHistory(language, loading)
                    }
                    else -> {
                        HistoryMetricGrid(language, summary)
                        Divider(color = MaterialTheme.colorScheme.outlineVariant)
                        Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            Text(
                                uiText(language, "نمودار بررسی‌ها", "Check activity"),
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.SemiBold
                            )
                            ConnectionHistoryChart(summary.buckets, range, language)
                            HistoryAxisLabels(summary.buckets, range, language)
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween
                            ) {
                                Text(
                                    formatBucketDate(summary.startEpochMs, range, language),
                                    style = MaterialTheme.typography.labelSmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                                Text(
                                    formatBucketDate(summary.endEpochMs, range, language),
                                    style = MaterialTheme.typography.labelSmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                            Row(horizontalArrangement = Arrangement.spacedBy(14.dp)) {
                                HistoryLegend(language, Color(0xFF16A34A), "موفق", "Success")
                                HistoryLegend(language, Color(0xFFDC2626), "ناموفق", "Failed")
                            }
                        }
                        Divider(color = MaterialTheme.colorScheme.outlineVariant)
                        val average = summary.averageDelayMs?.let { "$it ms" } ?: "—"
                        val minMax = if (summary.minimumDelayMs != null && summary.maximumDelayMs != null) {
                            "${summary.minimumDelayMs} – ${summary.maximumDelayMs} ms"
                        } else "—"
                        HistoryDetailRow(
                            uiText(language, "میانگین تأخیر موفق", "Average successful delay"),
                            average
                        )
                        HistoryDetailRow(
                            uiText(language, "کمترین تا بیشترین تأخیر", "Minimum to maximum delay"),
                            minMax
                        )
                        val failureText = if (summary.longestFailureRunCount == 0) {
                            uiText(language, "خطای متوالی ثبت نشده", "No consecutive failures recorded")
                        } else {
                            val span = formatDuration(summary.longestObservedFailureSpanMs)
                            uiText(
                                language,
                                "${summary.longestFailureRunCount} بررسی ناموفق متوالی؛ فاصلهٔ مشاهده‌شده: $span",
                                "${summary.longestFailureRunCount} consecutive failed checks; observed span: $span"
                            )
                        }
                        Text(
                            failureText,
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }

                Text(
                    uiText(
                        language,
                        "فقط بررسی‌های انجام‌شده هنگام پایشِ شروع‌شده با انتخاب کاربر ثبت می‌شوند. زمان توقف پایش داده محسوب نمی‌شود و تست دستی وارد این آمار نیست.",
                        "Only checks made while monitoring is manually active are recorded. Unmonitored time is not treated as downtime, and one-off manual tests are excluded."
                    ),
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
    }
}

@Composable
private fun HistoryMetricGrid(language: AppLanguage, summary: ConnectionHistorySummary) {
    val rate = summary.successRatePercent?.let { String.format(Locale.US, "%.1f%%", it) } ?: "—"
    Row(horizontalArrangement = Arrangement.spacedBy(9.dp)) {
        HistoryMetric(
            uiText(language, "کل بررسی‌ها", "Total checks"),
            summary.totalChecks.toString(),
            Modifier.weight(1f)
        )
        HistoryMetric(
            uiText(language, "موفقیت", "Success rate"),
            rate,
            Modifier.weight(1f),
            MaterialTheme.colorScheme.primary
        )
    }
    Row(horizontalArrangement = Arrangement.spacedBy(9.dp)) {
        HistoryMetric(
            uiText(language, "موفق", "Successful"),
            summary.successCount.toString(),
            Modifier.weight(1f),
            Color(0xFF16834F)
        )
        HistoryMetric(
            uiText(language, "ناموفق", "Failed"),
            summary.failureCount.toString(),
            Modifier.weight(1f),
            MaterialTheme.colorScheme.error
        )
    }
}

@Composable
private fun HistoryMetric(
    label: String,
    value: String,
    modifier: Modifier = Modifier,
    tint: Color = MaterialTheme.colorScheme.onSurface
) {
    Card(
        modifier = modifier,
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)
    ) {
        Column(Modifier.padding(horizontal = 13.dp, vertical = 12.dp), verticalArrangement = Arrangement.spacedBy(3.dp)) {
            Text(label, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Text(value, style = MaterialTheme.typography.titleLarge, color = tint, fontWeight = FontWeight.Bold)
        }
    }
}

@Composable
private fun ConnectionHistoryChart(
    buckets: List<ConnectionHistoryBucket>,
    range: ConnectionHistoryRange,
    language: AppLanguage
) {
    val successColor = Color(0xFF16A34A)
    val failureColor = Color(0xFFDC2626)
    val trackColor = MaterialTheme.colorScheme.surfaceVariant
    val maxCount = max(1, buckets.maxOfOrNull { it.totalChecks } ?: 0)

    Canvas(Modifier.fillMaxWidth().height(150.dp)) {
        val count = buckets.size.coerceAtLeast(1)
        val gap = if (count > 10) 3.dp.toPx() else 8.dp.toPx()
        val barWidth = ((size.width - gap * (count - 1)) / count).coerceAtLeast(1f)
        val baseline = size.height - 4.dp.toPx()
        val maxHeight = size.height - 10.dp.toPx()

        buckets.forEachIndexed { index, bucket ->
            val x = index * (barWidth + gap)
            val totalHeight = if (bucket.totalChecks == 0) 3.dp.toPx()
                else (maxHeight * bucket.totalChecks / maxCount).coerceAtLeast(5.dp.toPx())
            val totalTop = baseline - totalHeight
            drawRoundRect(
                color = trackColor,
                topLeft = Offset(x, totalTop),
                size = Size(barWidth, totalHeight),
                cornerRadius = CornerRadius(barWidth / 2f)
            )
            if (bucket.totalChecks > 0) {
                val failedHeight = totalHeight * bucket.failedChecks / bucket.totalChecks
                val successHeight = totalHeight - failedHeight
                if (successHeight > 0f) {
                    drawRoundRect(
                        color = successColor,
                        topLeft = Offset(x, baseline - successHeight),
                        size = Size(barWidth, successHeight),
                        cornerRadius = CornerRadius(barWidth / 2f)
                    )
                }
                if (failedHeight > 0f) {
                    drawRoundRect(
                        color = failureColor,
                        topLeft = Offset(x, totalTop),
                        size = Size(barWidth, failedHeight),
                        cornerRadius = CornerRadius(barWidth / 2f)
                    )
                }
            }
        }
    }
}

@Composable
private fun HistoryAxisLabels(
    buckets: List<ConnectionHistoryBucket>,
    range: ConnectionHistoryRange,
    language: AppLanguage
) {
    val locale = if (language.isEnglish) Locale.US else Locale("fa", "IR")
    Row(modifier = Modifier.fillMaxWidth()) {
        buckets.forEachIndexed { index, bucket ->
            val label = when (range) {
                ConnectionHistoryRange.LAST_24_HOURS ->
                    if (index % 4 == 0 || index == buckets.lastIndex) {
                        SimpleDateFormat("HH", locale).format(Date(bucket.startEpochMs))
                    } else ""
                ConnectionHistoryRange.LAST_7_DAYS ->
                    SimpleDateFormat("EEE", locale).format(Date(bucket.startEpochMs))
            }
            Text(
                label,
                modifier = Modifier.weight(1f),
                style = MaterialTheme.typography.labelSmall.copy(fontSize = 9.sp),
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
                maxLines = 1
            )
        }
    }
}

@Composable
private fun HistoryLegend(language: AppLanguage, color: Color, persian: String, english: String) {
    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(5.dp)) {
        Spacer(
            Modifier.width(8.dp).height(8.dp)
                .clip(RoundedCornerShape(3.dp))
                .background(color)
        )
        Text(uiText(language, persian, english), style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

@Composable
private fun HistoryDetailRow(label: String, value: String) {
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
        Text(label, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.weight(1f))
        Text(value, style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.SemiBold, textAlign = TextAlign.End)
    }
}

@Composable
private fun EmptyHistory(language: AppLanguage, loading: Boolean) {
    Column(
        Modifier.fillMaxWidth().padding(vertical = 26.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        Text(
            uiText(language, "هنوز داده‌ای برای این بازه ثبت نشده", "No data recorded for this period yet"),
            fontWeight = FontWeight.SemiBold,
            textAlign = TextAlign.Center
        )
        Text(
            uiText(
                language,
                "پایش را به‌صورت دستی شروع کن. از این لحظه بررسی‌های خودکار وارد تاریخچه می‌شوند.",
                "Start monitoring manually. Automatic checks from this point onward will appear here."
            ),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center
        )
        if (loading) CircularProgressIndicator()
    }
}

private fun formatBucketDate(epochMs: Long, range: ConnectionHistoryRange, language: AppLanguage): String {
    val locale = if (language.isEnglish) Locale.US else Locale("fa", "IR")
    val pattern = if (range == ConnectionHistoryRange.LAST_24_HOURS) "HH:mm" else "MMM d"
    return SimpleDateFormat(pattern, locale).format(Date(epochMs))
}

private fun formatDuration(durationMs: Long): String {
    val totalSeconds = (durationMs / 1000L).coerceAtLeast(0L)
    val hours = totalSeconds / 3600L
    val minutes = (totalSeconds % 3600L) / 60L
    val seconds = totalSeconds % 60L
    return when {
        hours > 0L -> "${hours}h ${minutes}m"
        minutes > 0L -> "${minutes}m ${seconds}s"
        else -> "${seconds}s"
    }
}
