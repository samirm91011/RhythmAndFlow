package com.rhythmandflow.app.notifications

import android.content.Context
import com.google.firebase.FirebaseApp
import com.google.firebase.messaging.FirebaseMessaging
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import com.rhythmandflow.app.RhythmApplication
import com.rhythmandflow.app.data.Outcome
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch

private const val KEY_TOKEN = "fcm_token"                 // this phone's Firebase token
private const val KEY_REGISTERED = "fcm_token_registered" // the token the server last accepted for the signed-in person

/**
 * Tells the server which phone to push to. Everything here quietly does nothing when Firebase is not set up
 * (no google-services.json in the build), so the app still works and relies on its periodic sync instead.
 */
object PushRegistration {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    private fun available(ctx: Context): Boolean = try { FirebaseApp.getApps(ctx).isNotEmpty() } catch (_: Throwable) { false }

    /** Asks Firebase for this phone's token and registers it with the server. Safe to call as often as needed. */
    fun sync(ctx: Context) {
        if (!available(ctx)) return
        val app = ctx.applicationContext
        try {
            FirebaseMessaging.getInstance().token.addOnSuccessListener { token -> onToken(app, token) }
        } catch (_: Throwable) { /* Play services missing or busy: the periodic sync still delivers */ }
    }

    fun onToken(ctx: Context, token: String) {
        val container = (ctx.applicationContext as RhythmApplication).container
        val prefs = container.localPrefs
        prefs.putString(KEY_TOKEN, token)
        if (!container.repository.hasToken) return              // not signed in yet; sync() runs again after sign-in
        if (prefs.getString(KEY_REGISTERED) == token) return    // the server already has it
        scope.launch {
            if (container.repository.registerDeviceToken(token) is Outcome.Ok) prefs.putString(KEY_REGISTERED, token)
        }
    }

    /** Called before signing out, while the login is still valid, so this phone stops getting this person's notifications. */
    suspend fun unregister(ctx: Context) {
        val container = (ctx.applicationContext as RhythmApplication).container
        val prefs = container.localPrefs
        val token = prefs.getString(KEY_TOKEN)
        if (token != null && container.repository.hasToken) container.repository.removeDeviceToken(token)
        prefs.putString(KEY_REGISTERED, null)
    }
}

/** Receives push messages from the server and shows them like any other notification. */
class PushService : FirebaseMessagingService() {
    override fun onNewToken(token: String) {
        PushRegistration.onToken(applicationContext, token)
    }

    override fun onMessageReceived(message: RemoteMessage) {
        val data = message.data
        val id = data["id"]?.toIntOrNull() ?: return
        val title = data["title"] ?: return
        val container = (applicationContext as RhythmApplication).container
        if (!container.repository.hasToken) return              // signed out: ignore anything that still arrives
        val channel = if (data["kind"] == "ADMIN_ERROR") Notifier.CH_ADMIN else Notifier.CH_GENERAL
        // Same id as the in-app copy, so the 15-minute sync replaces this notification instead of showing a second one.
        Notifier.show(applicationContext, id, title, data["body"].orEmpty(), data["route"]?.takeIf { it.isNotBlank() }, channel)
    }
}
