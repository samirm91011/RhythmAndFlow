package com.rhythmandflow.app.notifications

import android.content.Context
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import com.rhythmandflow.app.RhythmApplication
import com.rhythmandflow.app.data.Outcome
import java.util.concurrent.TimeUnit

private const val KEY_LAST_ID = "last_notified_id"
private const val WORK_NAME = "notification-sync"

/**
 * Runs about every 15 minutes (the shortest interval Android allows) while someone is signed in: asks the server for new
 * notifications and shows each one as a phone notification, and refreshes the class reminders from the bookings list.
 * Real push (Firebase) delivers instantly when it is set up; this keeps things arriving even when push is not available.
 */
class NotificationSyncWorker(ctx: Context, params: WorkerParameters) : CoroutineWorker(ctx, params) {
    override suspend fun doWork(): Result {
        val container = (applicationContext as RhythmApplication).container
        val repo = container.repository
        if (!repo.hasToken) return Result.success()

        val prefs = container.localPrefs
        val last = prefs.getInt(KEY_LAST_ID, -1)
        val result = when (val r = repo.notifications(afterId = if (last >= 0) last else null, unreadOnly = true)) {
            is Outcome.Ok -> {
                val items = r.value.sortedBy { it.id }
                if (last >= 0) {
                    items.forEach { n ->
                        val channel = if (n.kind == "ADMIN_ERROR") Notifier.CH_ADMIN else Notifier.CH_GENERAL
                        Notifier.show(applicationContext, n.id, n.title, n.body, n.route, channel)
                    }
                }
                // First run on this phone: remember where we are instead of flooding with old notifications.
                prefs.putInt(KEY_LAST_ID, maxOf(last, items.maxOfOrNull { it.id } ?: 0))
                Result.success()
            }
            // No signal: try again later. A server answer (even an error) is not worth retrying hard.
            is Outcome.Fail -> if (r.code == null) Result.retry() else Result.success()
        }

        // Keep class reminders in step with the server (covers a new phone, a reinstall, or a class cancelled by the studio).
        (repo.bookings() as? Outcome.Ok)?.let { ReminderScheduler.sync(applicationContext, prefs, it.value) }
        return result
    }
}

object NotificationSync {
    private val needsNetwork = Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build()

    fun start(ctx: Context) {
        val request = PeriodicWorkRequestBuilder<NotificationSyncWorker>(15, TimeUnit.MINUTES).setConstraints(needsNetwork).build()
        WorkManager.getInstance(ctx).enqueueUniquePeriodicWork(WORK_NAME, ExistingPeriodicWorkPolicy.KEEP, request)
    }

    /** One extra check right now (used after turning reminders on). */
    fun runOnce(ctx: Context) {
        WorkManager.getInstance(ctx).enqueue(OneTimeWorkRequestBuilder<NotificationSyncWorker>().setConstraints(needsNetwork).build())
    }

    fun stop(ctx: Context) {
        WorkManager.getInstance(ctx).cancelUniqueWork(WORK_NAME)
    }
}
