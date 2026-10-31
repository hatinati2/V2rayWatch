package com.linkwatch.app.monitor

import android.app.Service
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import androidx.core.app.ServiceCompat
import com.linkwatch.app.LinkWatchApplication
import com.linkwatch.app.core.XrayDelayTester
import com.linkwatch.app.data.LinkState
import com.linkwatch.app.data.ProxyProfile
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlin.math.max
import java.util.concurrent.atomic.AtomicInteger

class MonitorService : Service() {
    private val serviceScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private lateinit var tester: XrayDelayTester
    private var loopJob: kotlinx.coroutines.Job? = null

    override fun onCreate() {
        super.onCreate()
        tester = XrayDelayTester(applicationContext)
        NotificationHelper.createChannels(this)
        promoteToForeground(0, 0)
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_STOP -> {
                stopMonitoring()
                return START_NOT_STICKY
            }
            else -> startMonitoring()
        }
        return START_STICKY
    }

    private fun promoteToForeground(checked: Int, total: Int) {
        val notification = NotificationHelper.ongoing(this, checked, total)
        val type = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE
        } else 0
        ServiceCompat.startForeground(this, NotificationHelper.ONGOING_ID, notification, type)
    }

    private fun startMonitoring() {
        if (loopJob?.isActive == true) return
        loopJob = serviceScope.launch {
            while (isActive) {
                val repository = (application as LinkWatchApplication).profiles
                val profiles = repository.getProfiles()
                if (profiles.isEmpty()) {
                    delay(5_000L)
                    continue
                }

                val settings = repository.settings()
                val roundStartedAt = System.currentTimeMillis()
                val completed = AtomicInteger(0)
                withContext(Dispatchers.Main.immediate) {
                    promoteToForeground(completed.get(), profiles.size)
                }
                try {
                    profiles.map { original ->
                        async {
                            val current = repository.getProfiles().firstOrNull { it.id == original.id }
                            if (current != null) {
                                repository.upsert(current.copy(state = LinkState.CHECKING))
                                val result = tester.test(current, settings)
                                val previousFailures = current.consecutiveFailures
                                val nextFailures = if (result.success) 0 else previousFailures + 1
                                val nextState = when {
                                    result.success -> LinkState.CONNECTED
                                    nextFailures >= settings.failureThreshold -> LinkState.DISCONNECTED
                                    else -> LinkState.UNSTABLE
                                }
                                val updated = current.copy(
                                    state = nextState,
                                    delayMs = result.delayMs,
                                    lastCheckedAt = System.currentTimeMillis(),
                                    consecutiveFailures = nextFailures,
                                    lastError = if (result.success) "" else result.error
                                )
                                // A profile deleted during its probe must not be re-created by this completion.
                                if (repository.getProfiles().any { it.id == current.id }) {
                                    repository.upsert(updated)
                                    if (nextState == LinkState.DISCONNECTED && current.state != LinkState.DISCONNECTED) {
                                        NotificationHelper.notifyDisconnected(this@MonitorService, current.name, result.error)
                                    } else if (result.success && current.state == LinkState.DISCONNECTED) {
                                        NotificationHelper.notifyRecovered(this@MonitorService, current.name, result.delayMs)
                                    }
                                }
                            }
                            val count = completed.incrementAndGet()
                            withContext(Dispatchers.Main.immediate) {
                                promoteToForeground(count, profiles.size)
                            }
                        }
                    }.awaitAll()
                } catch (e: CancellationException) {
                    throw e
                } catch (_: Exception) {
                    // Individual profile failures are recorded by XrayDelayTester; keep monitoring other links.
                }

                val targetMs = max(settings.intervalSeconds * 1000L, 20_000L)
                val elapsedMs = System.currentTimeMillis() - roundStartedAt
                delay(max(targetMs - elapsedMs, 1_000L))
            }
        }
    }

    private fun stopMonitoring() {
        loopJob?.cancel()
        loopJob = null
        stopForeground(STOP_FOREGROUND_REMOVE)
        stopSelf()
    }

    override fun onDestroy() {
        loopJob?.cancel()
        serviceScope.cancel()
        super.onDestroy()
    }

    override fun onBind(intent: Intent?): IBinder? = null

    companion object {
        const val ACTION_START = "com.linkwatch.app.action.START_MONITOR"
        const val ACTION_STOP = "com.linkwatch.app.action.STOP_MONITOR"
    }
}
