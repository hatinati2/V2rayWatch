package com.linkwatch.app.ui

import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.Backup
import androidx.compose.material.icons.outlined.ContentPaste
import androidx.compose.material.icons.outlined.DeleteOutline
import androidx.compose.material.icons.outlined.History
import androidx.compose.material.icons.outlined.NotificationsActive
import androidx.compose.material.icons.outlined.Refresh
import androidx.compose.material.icons.outlined.QrCodeScanner
import androidx.compose.material.icons.outlined.Settings
import androidx.compose.material.icons.outlined.Speed
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.AssistChip
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.Divider
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.graphics.Color
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import com.linkwatch.app.AppLanguage
import com.linkwatch.app.uiText
import com.linkwatch.app.uiError
import com.linkwatch.app.data.ConnectionHistoryRange
import com.linkwatch.app.data.EncryptedBackupCodec
import com.linkwatch.app.data.LinkState
import com.linkwatch.app.data.MonitorSettings
import com.linkwatch.app.data.ProxyProfile
import com.linkwatch.app.data.UPTIME_HISTORY_LIMIT
import com.linkwatch.app.data.uptimeHistorySlots
import com.linkwatch.app.data.UptimeMark
import com.journeyapps.barcodescanner.ScanContract
import com.journeyapps.barcodescanner.ScanOptions
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.ByteArrayOutputStream
import java.io.IOException
import java.text.DateFormat
import java.util.Date

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LinkWatchScreen(
    viewModel: MainViewModel,
    sharedText: String? = null,
    language: AppLanguage,
    onLanguageChange: (AppLanguage) -> Unit
) {
    val state by viewModel.uiState.collectAsState()
    LaunchedEffect(language) { viewModel.setLanguage(language) }
    val snackbarHost = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    val clipboard = LocalClipboardManager.current
    val context = LocalContext.current
    val notificationPermission = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
        if (granted) viewModel.setMonitoring(true)
        else scope.launch { snackbarHost.showSnackbar(uiText(language, "برای دریافت هشدار قطع و وصل، اجازه اعلان لازم است", "Notification permission is required for connection alerts")) }
    }
    var showImport by remember { mutableStateOf(false) }
    var showSettings by remember { mutableStateOf(false) }
    var showBackup by remember { mutableStateOf(false) }
    var pendingExportPassphrase by remember { mutableStateOf("") }
    var pendingImportPassphrase by remember { mutableStateOf("") }
    var importText by remember { mutableStateOf("") }
    var historyProfile by remember { mutableStateOf<ProxyProfile?>(null) }
    var historyRange by remember { mutableStateOf(ConnectionHistoryRange.LAST_24_HOURS) }
    val historyState by viewModel.connectionHistory.collectAsState()

    LaunchedEffect(historyProfile?.id, historyRange, state.monitoring) {
        val selected = historyProfile
        if (selected == null) {
            viewModel.clearConnectionHistorySelection()
        } else {
            viewModel.refreshConnectionHistory(selected.id, historyRange)
            if (state.monitoring) {
                while (true) {
                    delay(5_000L)
                    viewModel.refreshConnectionHistory(selected.id, historyRange)
                }
            }
        }
    }
    val exportBackupLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.CreateDocument("application/vnd.v2raywatch.backup")
    ) { uri ->
        val passphrase = pendingExportPassphrase
        pendingExportPassphrase = ""
        if (uri != null) {
            scope.launch {
                try {
                    val encrypted = viewModel.createEncryptedBackup(passphrase.toCharArray())
                    try {
                        withContext(Dispatchers.IO) {
                            val output = context.contentResolver.openOutputStream(uri, "w")
                                ?: throw IOException("Could not open the selected backup file")
                            output.use { it.write(encrypted) }
                        }
                    } finally {
                        encrypted.fill(0)
                    }
                    snackbarHost.showSnackbar(uiText(language, "پشتیبان رمزگذاری‌شده ذخیره شد", "Encrypted backup saved"))
                } catch (error: Exception) {
                    snackbarHost.showSnackbar(
                        uiText(language, "ساخت پشتیبان ناموفق بود: ${error.message ?: "خطا"}", "Backup failed: ${error.message ?: "Error"}")
                    )
                }
            }
        }
    }
    val importBackupLauncher = rememberLauncherForActivityResult(ActivityResultContracts.OpenDocument()) { uri ->
        val passphrase = pendingImportPassphrase
        pendingImportPassphrase = ""
        if (uri != null) {
            scope.launch {
                try {
                    val encrypted = withContext(Dispatchers.IO) {
                        val input = context.contentResolver.openInputStream(uri)
                            ?: throw IOException("Could not open the selected backup file")
                        input.use { stream ->
                            val output = ByteArrayOutputStream()
                            val buffer = ByteArray(8192)
                            while (true) {
                                val count = stream.read(buffer)
                                if (count < 0) break
                                if (output.size() + count > EncryptedBackupCodec.MAX_BACKUP_BYTES) {
                                    throw IOException("Backup file is too large")
                                }
                                output.write(buffer, 0, count)
                            }
                            output.toByteArray()
                        }
                    }
                    viewModel.restoreEncryptedBackup(encrypted, passphrase.toCharArray())
                    snackbarHost.showSnackbar(uiText(language, "بازیابی پشتیبان انجام شد", "Backup restored"))
                } catch (error: Exception) {
                    snackbarHost.showSnackbar(
                        uiText(language, "بازیابی ناموفق بود: ${error.message ?: "خطا"}", "Restore failed: ${error.message ?: "Error"}")
                    )
                }
            }
        }
    }

    val qrScanner = rememberLauncherForActivityResult(ScanContract()) { result ->
        val scanned = result.contents?.trim().orEmpty()
        if (scanned.isNotBlank()) {
            importText = scanned
            showImport = true
        }
    }

    LaunchedEffect(sharedText) {
        if (!sharedText.isNullOrBlank()) {
            importText = sharedText
            showImport = true
        }
    }
    LaunchedEffect(state.message) {
        state.message?.let {
            snackbarHost.showSnackbar(it)
            viewModel.consumeMessage()
        }
    }

    Scaffold(
        containerColor = MaterialTheme.colorScheme.background,
        topBar = {
            TopAppBar(
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.background
                ),
                title = {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Box(
                            Modifier.size(38.dp).clip(RoundedCornerShape(13.dp))
                                .background(MaterialTheme.colorScheme.primaryContainer),
                            contentAlignment = Alignment.Center
                        ) {
                            Icon(Icons.Outlined.Speed, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
                        }
                        Spacer(Modifier.width(10.dp))
                        Column {
                            Text("V2rayWatch", fontWeight = FontWeight.Bold)
                            Text(uiText(language, "پایش واقعی کانفیگ‌های پروکسی", "Real proxy config monitoring"), style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    }
                },
                actions = {
                    TextButton(onClick = { onLanguageChange(language.toggle()) }) {
                        Text(if (language.isEnglish) "FA" else "EN", fontWeight = FontWeight.Bold)
                    }
                    IconButton(onClick = { showSettings = true }) {
                        Icon(Icons.Outlined.Settings, contentDescription = uiText(language, "تنظیمات", "Settings"))
                    }
                    IconButton(onClick = { showBackup = true }) {
                        Icon(Icons.Outlined.Backup, contentDescription = uiText(language, "پشتیبان‌گیری و بازیابی", "Backup and restore"))
                    }

                    IconButton(onClick = { showImport = true }) {
                        Icon(Icons.Outlined.Add, contentDescription = uiText(language, "افزودن لینک", "Add link"))
                    }
                }
            )
        },
        snackbarHost = { SnackbarHost(snackbarHost) },
        floatingActionButton = {
            FloatingActionButton(onClick = { showImport = true }) {
                Icon(Icons.Outlined.Add, contentDescription = uiText(language, "افزودن کانفیگ", "Add config"))
            }
        }
    ) { padding ->
        LazyColumn(
            modifier = Modifier.fillMaxSize().padding(padding),
            contentPadding = androidx.compose.foundation.layout.PaddingValues(start = 16.dp, end = 16.dp, top = 8.dp, bottom = 96.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            item {
                MonitoringCard(
                    language = language,
                    monitoring = state.monitoring,
                    interval = state.settings.intervalSeconds,
                    total = state.profiles.size,
                    connected = state.profiles.count { it.state == LinkState.CONNECTED },
                    disconnected = state.profiles.count { it.state == LinkState.DISCONNECTED },
                    onToggle = { enabled ->
                        if (enabled && Build.VERSION.SDK_INT >= 33 &&
                            ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED
                        ) {
                            notificationPermission.launch(Manifest.permission.POST_NOTIFICATIONS)
                        } else {
                            viewModel.setMonitoring(enabled)
                        }
                    },
                    onSettings = { showSettings = true }
                )
            }
            item {
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceBetween, modifier = Modifier.fillMaxWidth()) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(uiText(language, "کانفیگ‌های من", "My Configs"), style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                        Text(uiText(language, "تست واقعی از مسیر خود پروکسی، نه Ping پورت", "Real test through the proxy, not a port ping"), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    AssistChip(
                        onClick = { showImport = true },
                        label = { Text(uiText(language, "افزودن لینک", "Add link")) },
                        leadingIcon = { Icon(Icons.Outlined.Add, contentDescription = null, modifier = Modifier.size(16.dp)) }
                    )
                }
            }
            if (state.profiles.isEmpty()) {
                item {
                    EmptyState(
                        language = language,
                        onImport = { showImport = true },
                        onPaste = {
                            val pasted = clipboard.getText()?.text.orEmpty()
                            if (pasted.isNotBlank()) {
                                importText = pasted
                                showImport = true
                            } else scope.launch { snackbarHost.showSnackbar(uiText(language, "متنی در کلیپ‌بورد نیست", "Clipboard is empty")) }
                        }
                    )
                }
            } else {
                item {
                    Row(
                        horizontalArrangement = Arrangement.spacedBy(14.dp),
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier.fillMaxWidth().padding(start = 4.dp, bottom = 2.dp)
                    ) {
                        UptimeLegend(language, Color(0xFF16A34A), "متصل", "Up")
                        UptimeLegend(language, Color(0xFFDC2626), "قطع", "Down")
                        UptimeLegend(language, Color(0xFFCBD5E1), "بدون سابقه", "No data")
                    }
                }
                items(state.profiles, key = { it.id }) { profile ->
                    ProfileCard(
                        language = language,
                        profile = profile,
                        onTest = { viewModel.testNow(profile) },
                        onHistory = {
                            historyRange = ConnectionHistoryRange.LAST_24_HOURS
                            historyProfile = profile
                        },
                        onDelete = { viewModel.deleteProfile(profile) }
                    )
                }
            }
            item {
                Text(
                    uiText(language, "هنگام فعال‌بودن پایش، اعلان دائمی سرویس روشن می‌ماند. محدودیت‌های باتری بعضی گوشی‌ها ممکن است فاصلهٔ واقعی بررسی‌ها را بیشتر کند.", "While monitoring is active, the service keeps a persistent notification. Battery restrictions on some phones may increase the actual interval between checks."),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(top = 4.dp, bottom = 8.dp)
                )
            }
        }
    }

    if (showImport) {
        AlertDialog(
            onDismissRequest = { showImport = false },
            title = { Text(uiText(language, "افزودن لینک یا Subscription", "Add Link or Subscription")) },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(uiText(language, "یک لینک، چند لینک در خط‌های جدا، یا آدرس Subscription را وارد کن.", "Enter one link, multiple links on separate lines, or a subscription URL."), style = MaterialTheme.typography.bodySmall)
                    OutlinedTextField(
                        value = importText,
                        onValueChange = { importText = it },
                        modifier = Modifier.fillMaxWidth(),
                        minLines = 5,
                        maxLines = 9,
                        placeholder = { Text("vless://…\nvmess://…\nhttps://example.com/sub…") }
                    )
                    OutlinedButton(
                        onClick = {
                            val options = ScanOptions().apply {
                                setDesiredBarcodeFormats(ScanOptions.QR_CODE)
                                setPrompt(uiText(language, "کد QR لینک یا کانفیگ را اسکن کنید", "Scan a config or link QR code"))
                                setBeepEnabled(false)
                                setOrientationLocked(false)
                            }
                            qrScanner.launch(options)
                        },
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Icon(Icons.Outlined.QrCodeScanner, contentDescription = null)
                        Spacer(Modifier.width(8.dp))
                        Text(uiText(language, "اسکن QR کد", "Scan QR Code"))
                    }
                    OutlinedButton(
                        onClick = {
                            val pasted = clipboard.getText()?.text.orEmpty()
                            if (pasted.isNotBlank()) importText = if (importText.isBlank()) pasted else importText + "\n" + pasted
                        },
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Icon(Icons.Outlined.ContentPaste, contentDescription = null)
                        Spacer(Modifier.width(8.dp))
                        Text(uiText(language, "چسباندن از کلیپ‌بورد", "Paste from Clipboard"))
                    }
                    if (state.busy) Text(uiText(language, "در حال دریافت و پردازش لینک‌ها…", "Fetching and processing links…"), color = MaterialTheme.colorScheme.primary)
                }
            },
            confirmButton = {
                Button(onClick = {
                    viewModel.importLinks(importText)
                    showImport = false
                    importText = ""
                }, enabled = importText.isNotBlank() && !state.busy) { Text(uiText(language, "افزودن", "Add")) }
            },
            dismissButton = { TextButton(onClick = { showImport = false }) { Text(uiText(language, "انصراف", "Cancel")) } }
        )
    }

    if (showBackup) {
        BackupDialog(
            language = language,
            onDismiss = { showBackup = false },
            onExport = { passphrase ->
                pendingExportPassphrase = passphrase
                showBackup = false
                exportBackupLauncher.launch("V2rayWatch-backup.v2vwbackup")
            },
            onImport = { passphrase ->
                pendingImportPassphrase = passphrase
                showBackup = false
                importBackupLauncher.launch(arrayOf("application/vnd.v2raywatch.backup", "application/octet-stream", "*/*"))
            }
        )
    }

    if (showSettings) {
        SettingsDialog(
            language = language,
            current = state.settings,
            onDismiss = { showSettings = false },
            onSave = { viewModel.saveSettings(it); showSettings = false }
        )
    }

    if (historyProfile != null) {
        val selectedProfile = historyProfile!!
        val selectedHistory = historyState?.takeIf {
            it.profileId == selectedProfile.id && it.range == historyRange
        }
        ConnectionHistoryDialog(
            language = language,
            profile = selectedProfile,
            range = historyRange,
            summary = selectedHistory?.summary,
            loading = selectedHistory?.loading ?: true,
            onRangeChange = { historyRange = it },
            onDismiss = {
                historyProfile = null
                viewModel.clearConnectionHistorySelection()
            }
        )
    }

}

@Composable
private fun MonitoringCard(
    language: AppLanguage,
    monitoring: Boolean,
    interval: Int,
    total: Int,
    connected: Int,
    disconnected: Int,
    onToggle: (Boolean) -> Unit,
    onSettings: () -> Unit
) {
    Card(
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer),
        shape = RoundedCornerShape(24.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(Modifier.size(42.dp).clip(CircleShape).background(MaterialTheme.colorScheme.primary.copy(alpha = 0.12f)), contentAlignment = Alignment.Center) {
                    Icon(Icons.Outlined.Speed, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
                }
                Spacer(Modifier.width(10.dp))
                Column(Modifier.weight(1f)) {
                    Text(uiText(language, "پایش مداوم", "Continuous Monitoring"), fontWeight = FontWeight.Bold, style = MaterialTheme.typography.titleMedium)
                    Text(if (monitoring) uiText(language, "در حال بررسی کانفیگ‌ها", "Checking configurations") else uiText(language, "پایش متوقف است", "Monitoring is stopped"), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                Switch(checked = monitoring, onCheckedChange = onToggle)
            }
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                StatPill(uiText(language, "همه", "Total"), total.toString(), Modifier.weight(1f))
                StatPill(uiText(language, "سالم", "Online"), connected.toString(), Modifier.weight(1f), Color(0xFF16794A))
                StatPill(uiText(language, "قطع", "Offline"), disconnected.toString(), Modifier.weight(1f), MaterialTheme.colorScheme.error)
            }
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceBetween, modifier = Modifier.fillMaxWidth()) {
                Text(uiText(language, "فاصله تست", "Check interval"), style = MaterialTheme.typography.bodyMedium)
                Text(uiText(language, "هر $interval ثانیه", "Every $interval sec"), fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.primary)
            }
            if (monitoring) {
                Text(
                    uiText(
                        language,
                        "برای ادامهٔ بررسی با صفحهٔ خاموش، پردازنده در زمان پایش بیدار نگه داشته می‌شود؛ مصرف باتری بیشتر خواهد بود.",
                        "The CPU stays awake while monitoring so checks can continue with the screen off. This uses more battery."
                    ),
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            TextButton(onClick = onSettings, modifier = Modifier.align(Alignment.End)) {
                Icon(Icons.Outlined.Settings, contentDescription = null, modifier = Modifier.size(16.dp))
                Spacer(Modifier.width(4.dp))
                Text(uiText(language, "تنظیمات پایش", "Monitoring Settings"))
            }
        }
    }
}

@Composable
private fun StatPill(label: String, value: String, modifier: Modifier = Modifier, tint: Color = MaterialTheme.colorScheme.onSurface) {
    Column(modifier.clip(RoundedCornerShape(14.dp)).background(MaterialTheme.colorScheme.surface.copy(alpha = 0.7f)).padding(8.dp)) {
        Text(value, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold, color = tint)
        Text(label, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

@Composable
private fun ProfileCard(language: AppLanguage, profile: ProxyProfile, onTest: () -> Unit, onHistory: () -> Unit, onDelete: () -> Unit) {
    val statusColor = when (profile.state) {
        LinkState.CONNECTED -> Color(0xFF16834F)
        LinkState.DISCONNECTED -> MaterialTheme.colorScheme.error
        LinkState.UNSTABLE -> Color(0xFFB7791F)
        LinkState.CHECKING -> MaterialTheme.colorScheme.primary
        LinkState.UNSUPPORTED -> MaterialTheme.colorScheme.outline
        LinkState.UNKNOWN -> MaterialTheme.colorScheme.onSurfaceVariant
    }
    val statusText = when (profile.state) {
        LinkState.CONNECTED -> uiText(language, "متصل", "Connected")
        LinkState.DISCONNECTED -> uiText(language, "قطع", "Disconnected")
        LinkState.UNSTABLE -> uiText(language, "ناپایدار", "Unstable")
        LinkState.CHECKING -> uiText(language, "در حال تست", "Testing")
        LinkState.UNSUPPORTED -> uiText(language, "پشتیبانی‌نشده", "Unsupported")
        LinkState.UNKNOWN -> uiText(language, "تست نشده", "Not tested")
    }
    Card(
        shape = RoundedCornerShape(20.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(Modifier.fillMaxWidth().padding(10.dp), verticalArrangement = Arrangement.spacedBy(5.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(Modifier.size(10.dp).clip(CircleShape).background(statusColor))
                Spacer(Modifier.width(8.dp))
                Column(Modifier.weight(1f)) {
                    Text(profile.name, style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold, maxLines = 1, overflow = TextOverflow.Ellipsis)
                    Text("${profile.protocol}  •  ${profile.host}:${profile.port}", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1, overflow = TextOverflow.Ellipsis)
                }
                Column(horizontalAlignment = Alignment.End) {
                    Text(statusText, color = statusColor, fontWeight = FontWeight.SemiBold, style = MaterialTheme.typography.labelMedium)
                    Text(if (profile.delayMs >= 0) "${profile.delayMs} ms" else "— ms", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                }
            }
            if (profile.lastError.isNotBlank() && profile.state != LinkState.CONNECTED) {
                Text(uiError(language, profile.lastError), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.error, maxLines = 2, overflow = TextOverflow.Ellipsis)
            }
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    if (profile.lastCheckedAt > 0) uiText(language, "آخرین تست: ${DateFormat.getTimeInstance(DateFormat.SHORT).format(Date(profile.lastCheckedAt))}", "Last check: ${DateFormat.getTimeInstance(DateFormat.SHORT, if (language.isEnglish) java.util.Locale.US else java.util.Locale("fa", "IR")).format(Date(profile.lastCheckedAt))}") else uiText(language, "هنوز تست نشده", "Not checked yet"),
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.weight(1f)
                )
                IconButton(
                    onClick = onTest,
                    modifier = Modifier.size(32.dp)
                ) {
                    Icon(Icons.Outlined.Refresh, contentDescription = uiText(language, "تست الآن", "Test Now"), modifier = Modifier.size(18.dp))
                }
                IconButton(onClick = onHistory, modifier = Modifier.size(32.dp)) {
                    Icon(Icons.Outlined.History, contentDescription = uiText(language, "تاریخچهٔ بلندمدت", "Long-term history"), tint = MaterialTheme.colorScheme.primary)
                }
                IconButton(onClick = onDelete, modifier = Modifier.size(32.dp)) {
                    Icon(Icons.Outlined.DeleteOutline, contentDescription = uiText(language, "حذف", "Delete"), tint = MaterialTheme.colorScheme.error)
                }
            }
            UptimeHistoryBar(language = language, history = profile.uptimeHistory)
        }
    }
}

@Composable
private fun UptimeHistoryBar(language: AppLanguage, history: List<UptimeMark>) {
    val recentCount = history.takeLast(UPTIME_HISTORY_LIMIT).size
    val cells = uptimeHistorySlots(history)

    Column(verticalArrangement = Arrangement.spacedBy(3.dp), modifier = Modifier.fillMaxWidth()) {
        Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.fillMaxWidth()) {
            Text(
                uiText(language, "۵۰ بررسی خودکار اخیر", "Last 50 automatic checks"),
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                fontWeight = FontWeight.SemiBold,
                modifier = Modifier.weight(1f)
            )
            Text(
                "$recentCount/$UPTIME_HISTORY_LIMIT",
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
        Row(horizontalArrangement = Arrangement.spacedBy(2.dp), modifier = Modifier.fillMaxWidth()) {
            cells.forEachIndexed { index, mark ->
                val cellColor = when (mark) {
                    UptimeMark.CONNECTED -> Color(0xFF16A34A)
                    UptimeMark.DISCONNECTED -> Color(0xFFDC2626)
                    UptimeMark.UNKNOWN -> Color(0xFFCBD5E1)
                }
                val description = when (mark) {
                    UptimeMark.CONNECTED -> uiText(language, "بررسی موفق", "Check succeeded")
                    UptimeMark.DISCONNECTED -> uiText(language, "بررسی ناموفق", "Check failed")
                    UptimeMark.UNKNOWN -> uiText(language, "بدون سابقه", "No history")
                }
                Box(
                    modifier = Modifier
                        .weight(1f)
                        .height(8.dp)
                        .clip(RoundedCornerShape(3.dp))
                        .background(cellColor)
                        .semantics { contentDescription = "${index + 1}: $description" }
                )
            }
        }
    }
}

@Composable
private fun UptimeLegend(language: AppLanguage, color: Color, persian: String, english: String) {
    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(4.dp)) {
        Box(Modifier.size(7.dp).clip(RoundedCornerShape(2.dp)).background(color))
        Text(uiText(language, persian, english), style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

@Composable
private fun EmptyState(language: AppLanguage, onImport: () -> Unit, onPaste: () -> Unit) {
    Card(shape = RoundedCornerShape(20.dp), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)) {
        Column(Modifier.fillMaxWidth().padding(22.dp), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Icon(Icons.Outlined.NotificationsActive, contentDescription = null, modifier = Modifier.size(42.dp), tint = MaterialTheme.colorScheme.primary)
            Text(uiText(language, "هنوز کانفیگی اضافه نکردی", "No configs added yet"), fontWeight = FontWeight.Bold)
            Text(uiText(language, "لینک VLESS، VMess، Trojan یا Shadowsocks و حتی Subscription را اضافه کن.", "Add a VLESS, VMess, Trojan, Shadowsocks link, or a subscription."), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Button(onClick = onImport) { Text(uiText(language, "افزودن لینک", "Add Link")) }
            OutlinedButton(onClick = onPaste) { Text(uiText(language, "چسباندن از کلیپ‌بورد", "Paste from Clipboard")) }
        }
    }
}

private enum class BackupMode { EXPORT, IMPORT }

@Composable
private fun BackupDialog(
    language: AppLanguage,
    onDismiss: () -> Unit,
    onExport: (String) -> Unit,
    onImport: (String) -> Unit
) {
    var mode by remember { mutableStateOf(BackupMode.EXPORT) }
    var passphrase by remember { mutableStateOf("") }
    var confirmation by remember { mutableStateOf("") }
    val exportMode = mode == BackupMode.EXPORT
    val validPassword = passphrase.length >= EncryptedBackupCodec.MIN_PASSPHRASE_LENGTH
    val valid = validPassword && (!exportMode || passphrase == confirmation)

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(uiText(language, "پشتیبان امن", "Secure backup")) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Text(
                    uiText(
                        language,
                        "فایل پشتیبان شامل کانفیگ‌ها، تنظیمات و تا ۵۰ نتیجهٔ اخیر است و با رمز عبور خودت رمزگذاری می‌شود. تاریخچهٔ بلندمدت جداگانه و فقط روی همین دستگاه نگهداری می‌شود.",
                        "The backup contains profiles, settings, and up to 50 recent checks. Long-term history is stored separately on this device and is not included in the portable backup."
                    ),
                    style = MaterialTheme.typography.bodySmall
                )
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    FilterChip(
                        selected = exportMode,
                        onClick = { mode = BackupMode.EXPORT; confirmation = "" },
                        label = { Text(uiText(language, "ساخت پشتیبان", "Create backup")) }
                    )
                    FilterChip(
                        selected = !exportMode,
                        onClick = { mode = BackupMode.IMPORT; confirmation = "" },
                        label = { Text(uiText(language, "بازیابی", "Restore")) }
                    )
                }
                OutlinedTextField(
                    value = passphrase,
                    onValueChange = { passphrase = it.take(EncryptedBackupCodec.MAX_PASSPHRASE_LENGTH) },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                    visualTransformation = PasswordVisualTransformation(),
                    label = { Text(uiText(language, "رمز پشتیبان (حداقل ۱۲ کاراکتر)", "Backup passphrase (12+ characters)")) }
                )
                if (exportMode) {
                    OutlinedTextField(
                        value = confirmation,
                        onValueChange = { confirmation = it.take(EncryptedBackupCodec.MAX_PASSPHRASE_LENGTH) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        visualTransformation = PasswordVisualTransformation(),
                        label = { Text(uiText(language, "تکرار رمز پشتیبان", "Confirm passphrase")) }
                    )
                } else {
                    Text(
                        uiText(
                            language,
                            "بازیابی، لینک‌های جدید را اضافه می‌کند و موارد تکراری را رد می‌کند؛ لینک‌های فعلی حذف نمی‌شوند. پایش خودکار روشن نخواهد شد.",
                            "Restore merges new profiles and skips duplicates. Existing profiles are never deleted, and monitoring is not started automatically."
                        ),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                if (passphrase.isNotEmpty() && !validPassword) {
                    Text(
                        uiText(language, "رمز باید حداقل ۱۲ کاراکتر باشد.", "Use at least 12 characters."),
                        color = MaterialTheme.colorScheme.error,
                        style = MaterialTheme.typography.labelSmall
                    )
                }
                if (exportMode && confirmation.isNotEmpty() && passphrase != confirmation) {
                    Text(
                        uiText(language, "دو رمز یکسان نیستند.", "The passphrases do not match."),
                        color = MaterialTheme.colorScheme.error,
                        style = MaterialTheme.typography.labelSmall
                    )
                }
            }
        },
        confirmButton = {
            Button(
                enabled = valid,
                onClick = {
                    if (exportMode) onExport(passphrase) else onImport(passphrase)
                    passphrase = ""
                    confirmation = ""
                }
            ) {
                Text(uiText(language, if (exportMode) "ذخیره فایل" else "انتخاب فایل", if (exportMode) "Save file" else "Choose file"))
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text(uiText(language, "انصراف", "Cancel")) }
        }
    )
}

@Composable
private fun SettingsDialog(language: AppLanguage, current: MonitorSettings, onDismiss: () -> Unit, onSave: (MonitorSettings) -> Unit) {
    var interval by remember(current.intervalSeconds) { mutableIntStateOf(current.intervalSeconds) }
    var threshold by remember(current.failureThreshold) { mutableIntStateOf(current.failureThreshold) }
    var primary by remember(current.primaryTestUrl) { mutableStateOf(current.primaryTestUrl) }
    var fallback by remember(current.fallbackTestUrl) { mutableStateOf(current.fallbackTestUrl) }
    var notifyOnDisconnect by remember(current.notifyOnDisconnect) { mutableStateOf(current.notifyOnDisconnect) }
    var notifyOnRecovery by remember(current.notifyOnRecovery) { mutableStateOf(current.notifyOnRecovery) }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(uiText(language, "تنظیمات پایش", "Monitoring Settings")) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Text(uiText(language, "فاصله بررسی لینک‌ها", "Link check interval"), fontWeight = FontWeight.SemiBold)
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    listOf(20, 30, 60, 120).forEach { seconds ->
                        FilterChip(selected = interval == seconds, onClick = { interval = seconds }, label = { Text("${seconds}s") })
                    }
                }
                Divider()
                Text(uiText(language, "هشدار پس از چند خطای متوالی؟", "Alert after how many consecutive failures?"), fontWeight = FontWeight.SemiBold)
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    (1..4).forEach { count ->
                        FilterChip(selected = threshold == count, onClick = { threshold = count }, label = { Text(count.toString()) })
                    }
                }
                Divider()
                Text(uiText(language, "اعلان‌های اتصال", "Connection notifications"), fontWeight = FontWeight.SemiBold)
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Checkbox(checked = notifyOnDisconnect, onCheckedChange = { notifyOnDisconnect = it })
                    Text(uiText(language, "اعلان هنگام قطع‌شدن لینک", "Notify when a link goes down"))
                }
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Checkbox(checked = notifyOnRecovery, onCheckedChange = { notifyOnRecovery = it })
                    Text(uiText(language, "اعلان هنگام بازیابی اتصال", "Notify when a connection recovers"))
                }
                Divider()
                OutlinedTextField(value = primary, onValueChange = { primary = it }, label = { Text(uiText(language, "URL تست اصلی", "Primary test URL")) }, singleLine = true, keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri))
                OutlinedTextField(value = fallback, onValueChange = { fallback = it }, label = { Text(uiText(language, "URL تست جایگزین", "Fallback test URL")) }, singleLine = true, keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri))
                Text(uiText(language, "این URL باید پاسخ HTTP 200 یا 204 بدهد و از داخل پروکسی قابل دسترسی باشد.", "This URL must return HTTP 200 or 204 and be reachable through the proxy."), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        },
        confirmButton = {
            Button(onClick = {
                onSave(MonitorSettings(intervalSeconds = interval, failureThreshold = threshold, primaryTestUrl = primary.trim(), fallbackTestUrl = fallback.trim(), notifyOnDisconnect = notifyOnDisconnect, notifyOnRecovery = notifyOnRecovery))
            }) { Text(uiText(language, "ذخیره", "Save")) }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text(uiText(language, "انصراف", "Cancel")) } }
    )
}
