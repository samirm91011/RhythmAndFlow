package com.rhythmandflow.app.diagnostics

import android.app.Application
import android.content.Context
import android.os.Build
import android.util.Log
import com.rhythmandflow.app.BuildConfig
import com.rhythmandflow.app.data.Api
import com.rhythmandflow.app.data.ErrorReportBody
import com.rhythmandflow.app.data.ErrorSink
import java.util.concurrent.ConcurrentHashMap
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch

/**
 * Tells the Rhythm & Flow team when something goes wrong for a customer.
 *  - A crash is saved to the phone the moment it happens and sent the next time the app opens.
 *  - Unexpected errors and server failures are sent straight away (at most a few per session, and the same
 *    problem is not repeated within five minutes) so a broken screen cannot flood the server.
 * Reports contain the technical message only: no passwords, no tokens. The server adds who it was from the login.
 */
class ErrorReporter(private val app: Application, private val api: Api) : ErrorSink {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val prefs = app.getSharedPreferences("rf_diagnostics", Context.MODE_PRIVATE)
    private val lastSent = ConcurrentHashMap<String, Long>()
    @Volatile private var sentThisSession = 0

    /** Call once from Application.onCreate. */
    fun install() {
        val previous = Thread.getDefaultUncaughtExceptionHandler()
        Thread.setDefaultUncaughtExceptionHandler { thread, error ->
            try { saveCrash(error) } catch (_: Throwable) { /* never let reporting hide the real crash */ }
            previous?.uncaughtException(thread, error)
        }
        sendSavedCrash()
    }

    private fun saveCrash(error: Throwable) {
        // commit() (not apply()) so the report is on disk before the process dies
        prefs.edit()
            .putString("crash_message", "${error.javaClass.simpleName}: ${error.message}".take(450))
            .putString("crash_details", Log.getStackTraceString(error).take(7500))
            .commit()
    }

    private fun sendSavedCrash() {
        val message = prefs.getString("crash_message", null) ?: return
        val details = prefs.getString("crash_details", null)
        scope.launch {
            if (post("[CRASH] $message", details, "app crash", fatal = true)) {
                prefs.edit().remove("crash_message").remove("crash_details").apply()
            }
        }
    }

    override fun report(message: String, details: String?, route: String?, fatal: Boolean) {
        val key = message.take(120)
        val now = System.currentTimeMillis()
        if (sentThisSession >= MAX_PER_SESSION) return
        if (now - (lastSent[key] ?: 0L) < REPEAT_WINDOW_MS) return
        lastSent[key] = now
        sentThisSession++
        scope.launch { post(message, details, route, fatal) }
    }

    private suspend fun post(message: String, details: String?, route: String?, fatal: Boolean): Boolean = try {
        api.reportError(
            ErrorReportBody(
                message = message.take(500), details = details?.take(8000), route = route?.take(200),
                appVersion = BuildConfig.VERSION_NAME,
                device = "${Build.MANUFACTURER} ${Build.MODEL} (Android ${Build.VERSION.RELEASE})".take(120),
                fatal = fatal,
            ),
        ).isSuccessful
    } catch (_: Throwable) {
        false // offline or server down: nothing more we can do, and we must not report the failure of reporting
    }

    private companion object {
        const val MAX_PER_SESSION = 10
        const val REPEAT_WINDOW_MS = 5 * 60 * 1000L
    }
}
