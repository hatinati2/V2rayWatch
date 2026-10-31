package com.linkwatch.app.ui

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.linkwatch.app.AppLanguage
import com.linkwatch.app.LinkWatchApplication
import com.linkwatch.app.uiError
import com.linkwatch.app.core.ProxyLinkParser
import com.linkwatch.app.core.XrayDelayTester
import com.linkwatch.app.data.BackupRestoreResult
import com.linkwatch.app.data.LinkState
import com.linkwatch.app.data.MonitorSettings
import com.linkwatch.app.data.ProfileRepository
import com.linkwatch.app.data.ProxyProfile
import com.linkwatch.app.monitor.MonitorController
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext


data class LinkWatchUiState(
    val profiles: List<ProxyProfile> = emptyList(),
    val settings: MonitorSettings = MonitorSettings(),
    val monitoring: Boolean = false,
    val busy: Boolean = false,
    val message: String? = null
)

class MainViewModel(application: Application) : AndroidViewModel(application) {
    @Volatile private var language: AppLanguage = AppLanguage.load(application)
    private fun message(fa: String, en: String) = if (language.isEnglish) en else fa

    fun setLanguage(value: AppLanguage) { language = value }

    private val repository: ProfileRepository = (application as LinkWatchApplication).profiles
    private val tester = XrayDelayTester(application)
    private val settingsFlow = MutableStateFlow(repository.settings())
    private val monitoringFlow = MutableStateFlow(repository.isMonitoringEnabled())
    private val busyFlow = MutableStateFlow(false)
    private val messageFlow = MutableStateFlow<String?>(null)

    val uiState = combine(
        repository.profiles,
        settingsFlow,
        monitoringFlow,
        busyFlow,
        messageFlow
    ) { profiles, settings, monitoring, busy, message ->
        LinkWatchUiState(profiles, settings, monitoring, busy, message)
    }.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), LinkWatchUiState())

    suspend fun createEncryptedBackup(passphrase: CharArray): ByteArray =
        withContext(Dispatchers.IO) {
            try {
                repository.createEncryptedBackup(passphrase)
            } finally {
                passphrase.fill('\u0000')
            }
        }

    suspend fun restoreEncryptedBackup(backup: ByteArray, passphrase: CharArray): BackupRestoreResult =
        withContext(Dispatchers.IO) {
            try {
                repository.restoreEncryptedBackup(backup, passphrase).also {
                    settingsFlow.value = repository.settings()
                    messageFlow.value = message(
                        "پشتیبان بازیابی شد: ${it.addedProfiles} لینک اضافه، ${it.skippedProfiles} لینک تکراری",
                        "Backup restored: ${it.addedProfiles} added, ${it.skippedProfiles} duplicates skipped"
                    )
                }
            } finally {
                passphrase.fill('\u0000')
                backup.fill(0)
            }
        }

    fun importLinks(text: String) {
        if (busyFlow.value) return
        viewModelScope.launch {
            busyFlow.value = true
            try {
                val parsed = ProxyLinkParser.importInput(text)
                val added = withContext(Dispatchers.IO) { repository.addAll(parsed) }
                messageFlow.value = if (added > 0) message("$added کانفیگ اضافه شد", "$added config(s) added") else message("این لینک‌ها قبلاً اضافه شده‌اند", "These links have already been added")
            } catch (e: Exception) {
                messageFlow.value = e.message?.take(180)?.let { uiError(language, it) } ?: message("ورود لینک ناموفق بود", "Could not import the link")
            } finally {
                busyFlow.value = false
            }
        }
    }

    fun deleteProfile(profile: ProxyProfile) {
        viewModelScope.launch(Dispatchers.IO) {
            repository.remove(profile.id)
            messageFlow.value = message("${profile.name} حذف شد", "${profile.name} deleted")
        }
    }

    fun testNow(profile: ProxyProfile) {
        viewModelScope.launch {
            val current = repository.getProfiles().firstOrNull { it.id == profile.id } ?: return@launch
            withContext(Dispatchers.IO) { repository.upsert(current.copy(state = LinkState.CHECKING)) }
            val result = tester.test(current, repository.settings())
            val threshold = repository.settings().failureThreshold
            val failures = if (result.success) 0 else current.consecutiveFailures + 1
            val nextState = when {
                result.success -> LinkState.CONNECTED
                failures >= threshold -> LinkState.DISCONNECTED
                else -> LinkState.UNSTABLE
            }
            withContext(Dispatchers.IO) { repository.upsert(current.copy(
                state = nextState,
                delayMs = result.delayMs,
                lastCheckedAt = System.currentTimeMillis(),
                consecutiveFailures = failures,
                lastError = if (result.success) "" else uiError(language, result.error)
            )) }
            messageFlow.value = if (result.success) "${current.name}: ${result.delayMs} ms" else message("تست ${current.name} ناموفق بود", "Test failed for ${current.name}")
        }
    }

    fun saveSettings(settings: MonitorSettings) {
        val primaryValid = settings.primaryTestUrl.startsWith("https://", ignoreCase = true)
        val fallbackValid = settings.fallbackTestUrl.isBlank() || settings.fallbackTestUrl.startsWith("https://", ignoreCase = true)
        if (!primaryValid || !fallbackValid) {
            messageFlow.value = message("برای حفظ امنیت، URL تست باید HTTPS باشد", "For security, test URLs must use HTTPS")
            return
        }
        repository.saveSettings(settings)
        settingsFlow.value = settings
        messageFlow.value = message("تنظیمات ذخیره شد", "Settings saved")
    }

    fun setMonitoring(enabled: Boolean) {
        repository.setMonitoringEnabled(enabled)
        monitoringFlow.value = enabled
        if (enabled) {
            MonitorController.start(getApplication())
            messageFlow.value = message("پایش مداوم شروع شد", "Monitoring started")
        } else {
            MonitorController.stop(getApplication())
            messageFlow.value = message("پایش متوقف شد", "Monitoring stopped")
        }
    }

    fun consumeMessage() {
        messageFlow.value = null
    }
}
