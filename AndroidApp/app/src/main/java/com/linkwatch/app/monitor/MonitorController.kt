package com.linkwatch.app.monitor

import android.content.Context
import android.content.Intent
import androidx.core.content.ContextCompat

object MonitorController {
    fun start(context: Context) {
        val intent = Intent(context, MonitorService::class.java).setAction(MonitorService.ACTION_START)
        ContextCompat.startForegroundService(context, intent)
    }

    fun stop(context: Context) {
        val intent = Intent(context, MonitorService::class.java).setAction(MonitorService.ACTION_STOP)
        context.startService(intent)
    }
}
