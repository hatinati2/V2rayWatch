package com.linkwatch.app

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.viewModels
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.unit.LayoutDirection
import com.linkwatch.app.monitor.NotificationHelper
import com.linkwatch.app.ui.LinkWatchScreen
import com.linkwatch.app.ui.MainViewModel
import com.linkwatch.app.ui.MainViewModelFactory

class MainActivity : ComponentActivity() {
    private val viewModel: MainViewModel by viewModels { MainViewModelFactory(application) }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val sharedText = if (intent?.action == Intent.ACTION_SEND) {
            intent.getStringExtra(Intent.EXTRA_TEXT)
        } else null
        val appContext = applicationContext

        setContent {
            var language by remember { mutableStateOf(AppLanguage.load(appContext)) }
            CompositionLocalProvider(
                LocalLayoutDirection provides if (language.isEnglish) LayoutDirection.Ltr else LayoutDirection.Rtl
            ) {
                MaterialTheme(colorScheme = lightColorScheme(
                    primary = Color(0xFF315BCE),
                    onPrimary = Color.White,
                    primaryContainer = Color(0xFFE8EEFF),
                    secondary = Color(0xFF52627A),
                    background = Color(0xFFF6F7FB),
                    surface = Color.White,
                    surfaceVariant = Color(0xFFEEF1F7)
                )) {
                    LinkWatchScreen(
                        viewModel = viewModel,
                        sharedText = sharedText,
                        language = language,
                        onLanguageChange = { selected ->
                            language = selected
                            AppLanguage.save(appContext, selected)
                            viewModel.setLanguage(selected)
                            NotificationHelper.createChannels(appContext)
                        }
                    )
                }
            }
        }
        viewModel.setLanguage(AppLanguage.load(appContext))
    }
}
