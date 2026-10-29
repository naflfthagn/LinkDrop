package com.qingchuan.linkdrop

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import com.qingchuan.linkdrop.ui.LinkDropApp
import com.qingchuan.linkdrop.ui.MainViewModel
import com.qingchuan.linkdrop.ui.theme.LinkDropTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        installSplashScreen()
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            LinkDropTheme {
                val stableVm: MainViewModel = viewModel()
                LinkDropApp(stableVm)
            }
        }
    }
}
