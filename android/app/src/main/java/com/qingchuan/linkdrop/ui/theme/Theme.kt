package com.qingchuan.linkdrop.ui.theme

import android.app.Activity
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Typography
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.SideEffect
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp
import androidx.core.view.WindowCompat

object Brand {
    val Primary = Color(0xFF0F4E48)
    val PrimaryDark = Color(0xFF09302D)
    val HeroTop = Color(0xFF155B53)
    val HeroBottom = Color(0xFF072C29)
    val Mint = Color(0xFF66F0D3)
    val MintDeep = Color(0xFF1CB092)
    val MintSoft = Color(0xFFE2FAF3)
    val MintSoftDark = Color(0xFF153B34)
    val Ink = Color(0xFF142D2B)
    val InkSoft = Color(0xFF4E6864)
    val Muted = Color(0xFF70827E)
    val Bg = Color(0xFFF3F7F5)
    val Surface = Color(0xFFFFFFFF)
    val SurfaceSoft = Color(0xFFF7FAF8)
    val Line = Color(0xFFE0EAE6)
    val Amber = Color(0xFFE09E3A)
    val Red = Color(0xFFCD4E58)
    val RedSoft = Color(0xFFFDEEF0)
    val DarkBg = Color(0xFF081411)
    val DarkSurface = Color(0xFF12201D)
    val DarkSurfaceSoft = Color(0xFF182824)
    val DarkLine = Color(0xFF263833)
    val DarkInk = Color(0xFFE8F2EE)
    val DarkMuted = Color(0xFFA4B5B0)
}

private val LightColors = lightColorScheme(
    primary = Brand.Primary,
    onPrimary = Color.White,
    primaryContainer = Brand.MintSoft,
    onPrimaryContainer = Brand.PrimaryDark,
    secondary = Brand.MintDeep,
    onSecondary = Color.White,
    secondaryContainer = Brand.MintSoft,
    onSecondaryContainer = Brand.PrimaryDark,
    background = Brand.Bg,
    onBackground = Brand.Ink,
    surface = Brand.Surface,
    onSurface = Brand.Ink,
    surfaceVariant = Brand.SurfaceSoft,
    onSurfaceVariant = Brand.InkSoft,
    outline = Brand.Line,
    outlineVariant = Brand.Line,
    error = Brand.Red,
    errorContainer = Brand.RedSoft,
    onErrorContainer = Brand.Red
)

private val DarkColors = darkColorScheme(
    primary = Brand.Mint,
    onPrimary = Brand.PrimaryDark,
    primaryContainer = Brand.MintSoftDark,
    onPrimaryContainer = Brand.Mint,
    secondary = Brand.Mint,
    onSecondary = Brand.PrimaryDark,
    secondaryContainer = Brand.MintSoftDark,
    onSecondaryContainer = Brand.Mint,
    background = Brand.DarkBg,
    onBackground = Brand.DarkInk,
    surface = Brand.DarkSurface,
    onSurface = Brand.DarkInk,
    surfaceVariant = Brand.DarkSurfaceSoft,
    onSurfaceVariant = Brand.DarkMuted,
    outline = Brand.DarkLine,
    outlineVariant = Brand.DarkLine,
    error = Color(0xFFFF8B95),
    errorContainer = Color(0xFF3B171B),
    onErrorContainer = Color(0xFFFFD7DA)
)

private val AppTypography = Typography(
    displaySmall = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.Bold, fontSize = 30.sp, lineHeight = 38.sp, letterSpacing = (-0.4).sp),
    headlineMedium = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.Bold, fontSize = 23.sp, lineHeight = 31.sp, letterSpacing = (-0.3).sp),
    titleLarge = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.SemiBold, fontSize = 18.sp, lineHeight = 25.sp),
    titleMedium = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.SemiBold, fontSize = 15.sp, lineHeight = 22.sp),
    titleSmall = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.SemiBold, fontSize = 13.sp, lineHeight = 19.sp),
    bodyLarge = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.Normal, fontSize = 15.sp, lineHeight = 23.sp),
    bodyMedium = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.Normal, fontSize = 13.5.sp, lineHeight = 20.sp),
    bodySmall = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.Normal, fontSize = 12.sp, lineHeight = 18.sp),
    labelLarge = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.SemiBold, fontSize = 13.5.sp, lineHeight = 19.sp),
    labelMedium = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.Medium, fontSize = 12.sp, lineHeight = 17.sp),
    labelSmall = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.Medium, fontSize = 10.5.sp, lineHeight = 15.sp)
)

@Composable
fun LinkDropTheme(content: @Composable () -> Unit) {
    val dark = isSystemInDarkTheme()
    val colors = if (dark) DarkColors else LightColors
    val view = LocalView.current
    if (!view.isInEditMode) {
        SideEffect {
            val window = (view.context as? Activity)?.window ?: return@SideEffect
            WindowCompat.getInsetsController(window, view).isAppearanceLightStatusBars = !dark
            WindowCompat.getInsetsController(window, view).isAppearanceLightNavigationBars = !dark
        }
    }
    MaterialTheme(
        colorScheme = colors,
        typography = AppTypography,
        content = content
    )
}
