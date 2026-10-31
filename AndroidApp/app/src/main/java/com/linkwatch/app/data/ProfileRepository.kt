package com.linkwatch.app.data

import android.content.Context
import android.content.SharedPreferences
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import java.util.UUID
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

data class BackupPayload(
    val formatVersion: Int = 1,
    val createdAtEpochMs: Long = System.currentTimeMillis(),
    val profiles: List<ProxyProfile>,
    val settings: MonitorSettings
)

data class BackupRestoreResult(
    val addedProfiles: Int,
    val skippedProfiles: Int
)

class ProfileRepository private constructor(context: Context) {
    private val appContext = context.applicationContext
    private val prefs: SharedPreferences = appContext.getSharedPreferences("linkwatch_secure_store", Context.MODE_PRIVATE)
    private val settingsPrefs: SharedPreferences = appContext.getSharedPreferences("linkwatch_settings", Context.MODE_PRIVATE)
    private val gson = Gson()
    private val alias = "linkwatch.profile.aes.v1"
    private val _profiles = MutableStateFlow(loadProfiles())
    val profiles: StateFlow<List<ProxyProfile>> = _profiles

    @Synchronized
    fun getProfiles(): List<ProxyProfile> = _profiles.value

    @Synchronized
    fun upsert(profile: ProxyProfile) {
        val next = _profiles.value.toMutableList()
        val index = next.indexOfFirst { it.id == profile.id }
        if (index >= 0) next[index] = profile else next.add(profile)
        persist(next)
    }

    @Synchronized
    fun addAll(profiles: List<ProxyProfile>): Int {
        if (profiles.isEmpty()) return 0
        val next = _profiles.value.toMutableList()
        val known = next.map { it.rawLink }.toHashSet()
        var added = 0
        profiles.forEach { profile ->
            if (known.add(profile.rawLink)) {
                next.add(profile)
                added++
            }
        }
        persist(next)
        return added
    }

    @Synchronized
    fun remove(id: String) {
        persist(_profiles.value.filterNot { it.id == id })
    }

    @Synchronized
    fun clear() {
        persist(emptyList())
    }

    fun settings(): MonitorSettings = MonitorSettings(
        intervalSeconds = settingsPrefs.getInt("interval", DEFAULT_CHECK_INTERVAL_SECONDS).coerceIn(20, 3600),
        failureThreshold = settingsPrefs.getInt("failure_threshold", DEFAULT_FAILURE_THRESHOLD).coerceIn(1, 5),
        primaryTestUrl = settingsPrefs.getString("test_url_primary", DEFAULT_PRIMARY_TEST_URL) ?: DEFAULT_PRIMARY_TEST_URL,
        fallbackTestUrl = settingsPrefs.getString("test_url_fallback", DEFAULT_FALLBACK_TEST_URL) ?: DEFAULT_FALLBACK_TEST_URL
    )

    fun isMonitoringEnabled(): Boolean = settingsPrefs.getBoolean("monitoring_enabled", false)

    fun setMonitoringEnabled(enabled: Boolean) {
        settingsPrefs.edit().putBoolean("monitoring_enabled", enabled).apply()
    }

    fun saveSettings(settings: MonitorSettings) {
        settingsPrefs.edit()
            .putInt("interval", settings.intervalSeconds.coerceIn(20, 3600))
            .putInt("failure_threshold", settings.failureThreshold.coerceIn(1, 5))
            .putString("test_url_primary", settings.primaryTestUrl.trim().ifBlank { DEFAULT_PRIMARY_TEST_URL })
            .putString("test_url_fallback", settings.fallbackTestUrl.trim().ifBlank { DEFAULT_FALLBACK_TEST_URL })
            .apply()
    }

    fun createEncryptedBackup(passphrase: CharArray): ByteArray {
        val payload = BackupPayload(
            profiles = getProfiles(),
            settings = settings()
        )
        val json = gson.toJson(payload).toByteArray(Charsets.UTF_8)
        return EncryptedBackupCodec.encrypt(json, passphrase)
    }

    @Synchronized
    fun restoreEncryptedBackup(backup: ByteArray, passphrase: CharArray): BackupRestoreResult {
        val plaintext = EncryptedBackupCodec.decrypt(backup, passphrase)
        val payload = try {
            gson.fromJson(plaintext.toString(Charsets.UTF_8), BackupPayload::class.java)
        } catch (_: Exception) {
            throw IllegalArgumentException("Backup data is invalid or unsupported")
        } finally {
            plaintext.fill(0)
        }

        require(payload != null && payload.formatVersion == 1) { "Backup data version is not supported" }
        require(payload.profiles.size <= 5_000) { "Backup contains too many profiles" }

        val backupSettings = payload.settings
        require(backupSettings != null) { "Backup settings are missing" }
        require(backupSettings.intervalSeconds in 20..3600) { "Backup monitoring interval is invalid" }
        require(backupSettings.failureThreshold in 1..5) { "Backup failure threshold is invalid" }
        require(backupSettings.primaryTestUrl.startsWith("https://", ignoreCase = true)) { "Backup primary test URL must use HTTPS" }
        require(backupSettings.fallbackTestUrl.isBlank() || backupSettings.fallbackTestUrl.startsWith("https://", ignoreCase = true)) {
            "Backup fallback test URL must use HTTPS"
        }

        val next = _profiles.value.toMutableList()
        val knownLinks = next.map { it.rawLink }.toHashSet()
        var added = 0
        var skipped = 0

        payload.profiles.forEach { profile ->
            require(profile.id.isNotBlank() && profile.name.isNotBlank() && profile.protocol.isNotBlank()) {
                "Backup contains an incomplete profile"
            }
            require(profile.rawLink.isNotBlank() && profile.configJson.isNotBlank()) {
                "Backup contains a profile without configuration data"
            }
            require(profile.port in 0..65535) { "Backup contains an invalid profile port" }
            require(profile.configJson.length <= 2 * 1024 * 1024) { "A profile configuration in the backup is too large" }

            if (!knownLinks.add(profile.rawLink)) {
                skipped++
            } else {
                val safeId = if (next.any { it.id == profile.id }) UUID.randomUUID().toString() else profile.id
                next.add(
                    profile.copy(
                        id = safeId,
                        state = LinkState.UNKNOWN,
                        delayMs = -1L,
                        lastCheckedAt = 0L,
                        consecutiveFailures = 0,
                        lastError = "",
                        uptimeHistory = profile.uptimeHistory.orEmpty().takeLast(UPTIME_HISTORY_LIMIT)
                    )
                )
                added++
            }
        }

        persist(next)
        saveSettings(backupSettings)
        return BackupRestoreResult(addedProfiles = added, skippedProfiles = skipped)
    }

    private fun loadProfiles(): List<ProxyProfile> {
        val encrypted = prefs.getString("profiles_ciphertext", null) ?: return emptyList()
        return try {
            val decoded = Base64.decode(encrypted, Base64.NO_WRAP)
            if (decoded.size <= 12) return emptyList()
            val iv = decoded.copyOfRange(0, 12)
            val ciphertext = decoded.copyOfRange(12, decoded.size)
            val cipher = Cipher.getInstance("AES/GCM/NoPadding")
            cipher.init(Cipher.DECRYPT_MODE, getOrCreateKey(), GCMParameterSpec(128, iv))
            val json = String(cipher.doFinal(ciphertext), Charsets.UTF_8)
            val type = object : TypeToken<List<ProxyProfile>>() {}.type
            (gson.fromJson<List<ProxyProfile>>(json, type) ?: emptyList()).map { profile ->
                // Gson sets newly-added fields to null when loading profiles saved by older app versions.
                profile.copy(uptimeHistory = profile.uptimeHistory.orEmpty().takeLast(UPTIME_HISTORY_LIMIT))
            }
        } catch (_: Exception) {
            // Keep the stored ciphertext intact. Never log decrypted config data.
            emptyList()
        }
    }

    private fun persist(profiles: List<ProxyProfile>) {
        val plaintext = gson.toJson(profiles).toByteArray(Charsets.UTF_8)
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.ENCRYPT_MODE, getOrCreateKey())
        val iv = cipher.iv
        val encrypted = cipher.doFinal(plaintext)
        val combined = iv + encrypted
        prefs.edit().putString("profiles_ciphertext", Base64.encodeToString(combined, Base64.NO_WRAP)).commit()
        _profiles.value = profiles
    }

    private fun getOrCreateKey(): SecretKey {
        val keyStore = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        val existing = keyStore.getKey(alias, null) as? SecretKey
        if (existing != null) return existing

        val generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore")
        generator.init(
            KeyGenParameterSpec.Builder(
                alias,
                KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT
            )
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setRandomizedEncryptionRequired(true)
                .setKeySize(256)
                .build()
        )
        return generator.generateKey()
    }

    companion object {
        @Volatile private var instance: ProfileRepository? = null

        fun get(context: Context): ProfileRepository = instance ?: synchronized(this) {
            instance ?: ProfileRepository(context).also { instance = it }
        }
    }
}
