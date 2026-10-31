package com.linkwatch.app.core

import android.content.Context
import android.provider.Settings
import android.util.Base64
import go.Seq
import libv2ray.CoreCallbackHandler
import libv2ray.CoreController
import libv2ray.Libv2ray
import com.linkwatch.app.data.DelayResult
import com.linkwatch.app.data.MonitorSettings
import com.linkwatch.app.data.ProxyProfile
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.sync.Semaphore
import kotlinx.coroutines.sync.withPermit
import kotlinx.coroutines.withContext
import java.io.File

/**
 * Runs the same kind of real outbound delay check used by v2rayNG:
 * Xray creates a temporary core instance from this profile's JSON, then performs
 * an HTTP request through that outbound. This is not a TCP ping or a port check.
 */
class XrayDelayTester(context: Context) {
    private val appContext = context.applicationContext

    suspend fun test(profile: ProxyProfile, settings: MonitorSettings): DelayResult = testSemaphore.withPermit {
        withContext(Dispatchers.IO) {
            var controller: CoreController? = null
            var started = false
            try {
                initializeCoreOnce()
                val activeController = Libv2ray.newCoreController(NoopCoreCallback)
                controller = activeController
                activeController.startLoop(profile.configJson, 0)
                started = true

                val primary = runCatching { activeController.measureDelay(settings.primaryTestUrl) }.getOrElse { -1L }
                if (primary >= 0L) return@withContext DelayResult(success = true, delayMs = primary)

                val fallback = settings.fallbackTestUrl.trim()
                if (fallback.isNotEmpty() && fallback != settings.primaryTestUrl) {
                    val secondary = runCatching { activeController.measureDelay(fallback) }.getOrElse { -1L }
                    if (secondary >= 0L) return@withContext DelayResult(success = true, delayMs = secondary)
                }
                DelayResult(success = false, error = "تست اتصال واقعی ناموفق بود یا مهلت پاسخ تمام شد")
            } catch (e: Exception) {
                DelayResult(success = false, error = safeError(e))
            } finally {
                if (started) runCatching { controller?.stopLoop() }
            }
        }
    }

    private object NoopCoreCallback : CoreCallbackHandler {
        override fun startup(): Long = 0L
        override fun shutdown(): Long = 0L
        override fun onEmitStatus(code: Long, message: String?): Long = 0L
    }

    private fun safeError(error: Throwable): String {
        val message = error.message.orEmpty()
            .replace(Regex("(?i)(password|uuid|id|token|privatekey)\\s*[:=]\\s*[^, ]+"), "$1=[redacted]")
            .take(140)
        return message.ifBlank { error.javaClass.simpleName }
    }

    private fun initializeCoreOnce() {
        if (initialized) return
        synchronized(initLock) {
            if (initialized) return
            Seq.setContext(appContext)
            val assetsDir = File(appContext.filesDir, "xray-assets").apply { mkdirs() }
            // Xray's XUDP base key is not an arbitrary UUID. Match the format used by
            // v2rayNG: URL-safe Base64 (without padding) of the 32-byte Android ID buffer.
            val androidId = Settings.Secure.getString(appContext.contentResolver, Settings.Secure.ANDROID_ID).orEmpty()
            require(androidId.isNotBlank()) { "شناسه امن دستگاه برای مقداردهی Xray در دسترس نیست" }
            val xudpBaseKey = Base64.encodeToString(
                androidId.toByteArray(Charsets.UTF_8).copyOf(32),
                Base64.NO_PADDING or Base64.URL_SAFE
            )
            Libv2ray.initCoreEnv(assetsDir.absolutePath, xudpBaseKey)
            coreVersion = Libv2ray.checkVersionX()
            initialized = true
        }
    }

    companion object {
        private val initLock = Any()
        private val testSemaphore = Semaphore(2)
        @Volatile private var initialized = false
        @Volatile var coreVersion: String = "Xray core not initialized"
            private set
    }
}
