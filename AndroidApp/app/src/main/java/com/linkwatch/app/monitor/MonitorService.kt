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
import com.linkwatch.app.data.reduceLinkCheck
import com.linkwatch.app.data.UptimeMark
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
import kotlinx.coroutines.supervisorScope
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

                // One bad profile must not cancel sibling checks or terminate the monitor loop.
                try {
                    supervisorScope {
                        profiles.map { original ->
                            async {
                                try {
                                    val current = repository.getProfiles().firstOrNull { it.id == original.id }
                                    if (current != null) {
                                        repository.upsert(current.copy(state = LinkState.CHECKING))
                                        val result = tester.test(current, settings)

                                        // Re-read after the probe so edits made during the request are preserved.
                                        // The pre-check state is retained only for transition/notification decisions.
                                        val latest = repository.getProfiles().firstOrNull { it.id == current.id }
                                        if (latest != null) {
                                            val transition = reduceLinkCheck(
                                                previous = latest.copy(state = current.state),
                                                result = result,
                                                settings = settings,
                                                checkedAtEpochMs = System.currentTimeMillis()
                                            )
                                            repository.upsert(transition.profile)

                                            if (transition.becameDisconnected) {
                                                NotificationHelper.notifyDisconnected(
                                                    this@MonitorService,
                                                    transition.profile.name,
                                                    result.error
                                                )
                                            } else if (transition.recovered) {
                                                NotificationHelper.notifyRecovered(
                                                    this@MonitorService,
                                                    transition.profile.name,
                                                    result.delayMs
                                                )
                                            }
                                        }
                                    }
                                } catch (cancelled: CancellationException) {
                                    throw cancelled
                                } catch (_: Exception) {
                                    // Isolate unexpected storage/profile errors to this profile.
                                    // Its next scheduled check can retry without stopping other links.
                                } finally {
                                    val count = completed.incrementAndGet()
                                    try {
                                        withContext(Dispatchers.Main.immediate) {
                                            promoteToForeground(count, profiles.size)
                                        }
                                    } catch (cancelled: CancellationException) {
                                        throw cancelled
                                    } catch (_: Exception) {
                                        // A progress-notification refresh must not abort the monitor round.
                                    }
                                }
                            }
                        }.awaitAll()
                    }
                } catch (cancelled: CancellationException) {
                    throw cancelled
                } catch (_: Exception) {
                    // Keep the service loop alive if a round-level operation unexpectedly fails.
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
