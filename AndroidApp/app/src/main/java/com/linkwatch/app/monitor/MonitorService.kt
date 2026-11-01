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
import com.linkwatch.app.data.ProfileRepository
import com.linkwatch.app.data.reduceLinkCheck
import com.linkwatch.app.data.notificationEvents
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
import java.util.UUID

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
        // Each explicit monitoring run has its own ID; failure sequences never span sessions.
        val monitoringSessionId = UUID.randomUUID().toString()
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
                                var checkedProfileId: String? = null
                                var stateBeforeCheck: LinkState? = null
                                try {
                                    val current = repository.getProfiles().firstOrNull { it.id == original.id }
                                    if (current != null) {
                                        checkedProfileId = current.id
                                        stateBeforeCheck = current.state.takeUnless { it == LinkState.CHECKING }
                                            ?: LinkState.UNKNOWN
                                        repository.upsert(current.copy(state = LinkState.CHECKING))
                                        val result = tester.test(current, settings)

                                        // Re-read after the probe so edits made during the request are preserved.
                                        // Keep the original state only for transition/notification decisions.
                                        val latest = repository.getProfiles().firstOrNull { it.id == current.id }
                                        if (latest != null) {
                                            val transition = reduceLinkCheck(
                                                previous = latest.copy(state = stateBeforeCheck ?: LinkState.UNKNOWN),
                                                result = result,
                                                settings = settings,
                                                checkedAtEpochMs = System.currentTimeMillis()
                                            )
                                            repository.upsert(transition.profile)
                                            // Only service-driven checks enter long-term history; manual tests are excluded.
                                            repository.recordMonitoringCheck(
                                                profileId = transition.profile.id,
                                                sessionId = monitoringSessionId,
                                                checkedAtEpochMs = transition.profile.lastCheckedAt,
                                                success = result.success,
                                                delayMs = result.delayMs.takeIf { result.success && it >= 0L }
                                            )

                                            val events = notificationEvents(transition, settings)
                                            if (events.disconnected) {
                                                NotificationHelper.notifyDisconnected(this@MonitorService, transition.profile.id, transition.profile.name, result.error)
                                            } else if (events.recovered) {
                                                NotificationHelper.notifyRecovered(this@MonitorService, transition.profile.id, transition.profile.name, result.delayMs, transition.outageDurationMs)
                                            }
                                        }
                                    }
                                } catch (cancelled: CancellationException) {
                                    restorePendingCheck(repository, checkedProfileId, stateBeforeCheck)
                                    throw cancelled
                                } catch (_: Exception) {
                                    // Restore a temporary CHECKING state if a storage/probe operation failed.
                                    restorePendingCheck(repository, checkedProfileId, stateBeforeCheck)
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

    private fun restorePendingCheck(
        repository: ProfileRepository,
        profileId: String?,
        previousState: LinkState?
    ) {
        if (profileId == null || previousState == null) return
        runCatching {
            val latest = repository.getProfiles().firstOrNull { it.id == profileId }
            if (latest?.state == LinkState.CHECKING) {
                repository.upsert(latest.copy(state = previousState))
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
