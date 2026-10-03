package com.rhythmandflow.app.data

import android.content.Context
import android.content.SharedPreferences
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey

/** Keeps the login token in encrypted storage (Android Keystore backed). */
class TokenStore(context: Context) {
    private val prefs: SharedPreferences = try {
        val key = MasterKey.Builder(context).setKeyScheme(MasterKey.KeyScheme.AES256_GCM).build()
        EncryptedSharedPreferences.create(
            context, "rf_secure", key,
            EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
            EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM,
        )
    } catch (e: Exception) {
        // Keystore problems on some devices/emulators: fall back to private (unencrypted) storage rather than crash.
        context.getSharedPreferences("rf_secure_fallback", Context.MODE_PRIVATE)
    }

    var token: String?
        get() = prefs.getString("token", null)
        set(value) = prefs.edit().apply { if (value == null) remove("token") else putString("token", value) }.apply()

    fun clear() = prefs.edit().remove("token").apply()
}

/** Non-sensitive, per-device preferences (intentions, favourites, saved lessons). */
class LocalPrefs(context: Context) {
    private val prefs = context.getSharedPreferences("rf_local", Context.MODE_PRIVATE)

    fun getSet(key: String): Set<String> = prefs.getStringSet(key, emptySet()) ?: emptySet()
    fun putSet(key: String, value: Set<String>) = prefs.edit().putStringSet(key, value).apply()
    fun getInt(key: String, default: Int = -1) = prefs.getInt(key, default)
    fun putInt(key: String, value: Int) = prefs.edit().putInt(key, value).apply()
    fun getBool(key: String, default: Boolean = false) = prefs.getBoolean(key, default)
    fun putBool(key: String, value: Boolean) = prefs.edit().putBoolean(key, value).apply()
}
