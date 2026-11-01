package com.linkwatch.app.ui

import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp

private val LightColors = lightColorScheme(
    primary = Color(0xFF315BCB),
    onPrimary = Color.White,
    primaryContainer = Color(0xFFE4EBFF),
    onPrimaryContainer = Color(0xFF12295F),
    secondary = Color(0xFF087E8B),
    onSecondary = Color.White,
    secondaryContainer = Color(0xFFD4F3F2),
    onSecondaryContainer = Color(0xFF073E46),
    tertiary = Color(0xFF5D5BC8),
    background = Color(0xFFF4F6FB),
    surface = Color(0xFFFCFDFF),
    surfaceVariant = Color(0xFFE9EDF5),
    onSurface = Color(0xFF182235),
    onSurfaceVariant = Color(0xFF596579),
    outline = Color(0xFF8792A6),
    outlineVariant = Color(0xFFD7DDE8),
    error = Color(0xFFBA303B),
    errorContainer = Color(0xFFFFE1E3)
)

private val DarkColors = darkColorScheme(
    primary = Color(0xFFA8BCFF),
    onPrimary = Color(0xFF142B63),
    primaryContainer = Color(0xFF263F7E),
    onPrimaryContainer = Color(0xFFE0E7FF),
    secondary = Color(0xFF73D6D1),
    onSecondary = Color(0xFF003B40),
    secondaryContainer = Color(0xFF15545A),
    onSecondaryContainer = Color(0xFFB9F1EC),
    tertiary = Color(0xFFC1BDFF),
    background = Color(0xFF0D1422),
    surface = Color(0xFF151E2D),
    surfaceVariant = Color(0xFF202C3E),
    onSurface = Color(0xFFE8EEF8),
    onSurfaceVariant = Color(0xFFB0BDCF),
    outline = Color(0xFF8392A8),
    outlineVariant = Color(0xFF35445A),
    error = Color(0xFFFF9298),
    errorContainer = Color(0xFF5B252C)
)

@Composable
fun V2rayWatchTheme(
    darkTheme: Boolean,
    content: @Composable () -> Unit
) {
    MaterialTheme(
        colorScheme = if (darkTheme) DarkColors else LightColors,
        shapes = androidx.compose.material3.Shapes(
            small = RoundedCornerShape(10.dp),
            medium = RoundedCornerShape(17.dp),
            large = RoundedCornerShape(24.dp)
        ),
        content = content
    )
}
