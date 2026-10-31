package com.linkwatch.app

import android.app.Application
import com.linkwatch.app.data.ProfileRepository
import com.linkwatch.app.monitor.NotificationHelper

class LinkWatchApplication : Application() {
    lateinit var profiles: ProfileRepository
        private set

    override fun onCreate() {
        super.onCreate()
        profiles = ProfileRepository.get(this)
        NotificationHelper.createChannels(this)
    }
}
