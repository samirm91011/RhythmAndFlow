package com.rhythmandflow.app

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import com.rhythmandflow.app.notifications.Notifier
import com.rhythmandflow.app.ui.nav.AppRoot
import com.rhythmandflow.app.ui.theme.RhythmTheme
import com.rhythmandflow.app.ui.viewmodel.SessionViewModel
import com.rhythmandflow.app.ui.viewmodel.appViewModel

class MainActivity : ComponentActivity() {
    /** Screen a tapped notification asked to open (kept until the user is signed in and the screen is shown). */
    private var pendingRoute by mutableStateOf<String?>(null)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        pendingRoute = intent?.getStringExtra(Notifier.EXTRA_ROUTE)
        setContent {
            RhythmTheme {
                Root(pendingRoute, onRouteHandled = { pendingRoute = null })
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        intent.getStringExtra(Notifier.EXTRA_ROUTE)?.let { pendingRoute = it }
    }
}

@Composable
private fun Root(pendingRoute: String?, onRouteHandled: () -> Unit) {
    val session = appViewModel(key = "session") { SessionViewModel(it) }
    AppRoot(session, pendingRoute, onRouteHandled)
}
