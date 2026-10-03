package com.rhythmandflow.app.notifications

import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import com.rhythmandflow.app.MainActivity
import com.rhythmandflow.app.R

/** Creates the notification channels and shows phone notifications that open the right screen when tapped. */
object Notifier {
    const val CH_GENERAL = "general"
    const val CH_REMINDERS = "reminders"
    const val CH_ADMIN = "admin"

    const val EXTRA_ROUTE = "route"

    fun createChannels(ctx: Context) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val manager = ctx.getSystemService(NotificationManager::class.java)
        manager.createNotificationChannels(
            listOf(
                NotificationChannel(CH_GENERAL, "Updates", NotificationManager.IMPORTANCE_DEFAULT)
                    .apply { description = "Subscription, payment and class updates" },
                NotificationChannel(CH_REMINDERS, "Class reminders", NotificationManager.IMPORTANCE_HIGH)
                    .apply { description = "A reminder before a class you booked" },
                NotificationChannel(CH_ADMIN, "Admin alerts", NotificationManager.IMPORTANCE_DEFAULT)
                    .apply { description = "Errors reported by the app and server (administrators)" },
            ),
        )
    }

    fun show(ctx: Context, id: Int, title: String, body: String, route: String?, channel: String = CH_GENERAL) {
        val manager = NotificationManagerCompat.from(ctx)
        if (!manager.areNotificationsEnabled()) return   // the user switched notifications off, or has not allowed them yet
        val intent = Intent(ctx, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
            route?.let { putExtra(EXTRA_ROUTE, it) }
        }
        val tap = PendingIntent.getActivity(ctx, id, intent, PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
        val notification = NotificationCompat.Builder(ctx, channel)
            .setSmallIcon(R.drawable.ic_notification)
            .setColor(0xFF2F7A6E.toInt())
            .setContentTitle(title)
            .setContentText(body)
            .setStyle(NotificationCompat.BigTextStyle().bigText(body))
            .setAutoCancel(true)
            .setContentIntent(tap)
            .setPriority(if (channel == CH_REMINDERS) NotificationCompat.PRIORITY_HIGH else NotificationCompat.PRIORITY_DEFAULT)
            .build()
        try { manager.notify(id, notification) } catch (_: SecurityException) { /* permission revoked while running */ }
    }
}
