package com.rhythmandflow.app.notifications

import android.content.Context
import androidx.work.CoroutineWorker
import androidx.work.ExistingWorkPolicy
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import androidx.work.workDataOf
import com.rhythmandflow.app.data.Booking
import com.rhythmandflow.app.data.LocalPrefs
import com.rhythmandflow.app.ui.components.formatTime
import java.time.Duration
import java.time.Instant
import java.util.concurrent.TimeUnit

private const val KEY_SCHEDULED = "reminders_scheduled"
private const val KEY_ENABLED = "reminders_enabled"
private const val LEAD_MINUTES = 60L

/** Shows "your class starts soon" one hour before a booked class. The phone keeps the timer, so it works offline. */
class ClassReminderWorker(ctx: Context, params: WorkerParameters) : CoroutineWorker(ctx, params) {
    override suspend fun doWork(): Result {
        val bookingId = inputData.getInt("bookingId", -1)
        val name = inputData.getString("name") ?: return Result.success()
        val place = inputData.getString("location").orEmpty()
        val start = inputData.getString("start")
        val where = if (place.isBlank()) "" else " · $place"
        Notifier.show(
            applicationContext, 100_000 + bookingId, "Class starting soon",
            "$name starts at ${formatTime(start)}$where. See you there!", "bookings", Notifier.CH_REMINDERS,
        )
        return Result.success()
    }
}

object ReminderScheduler {
    fun enabled(prefs: LocalPrefs) = prefs.getBool(KEY_ENABLED, true)

    /**
     * Makes the set of scheduled reminders match the customer's current bookings: new bookings get a reminder,
     * cancelled or changed ones are removed. Safe to call as often as bookings are loaded.
     */
    fun sync(ctx: Context, prefs: LocalPrefs, bookings: List<Booking>) {
        val work = WorkManager.getInstance(ctx)
        val previous = prefs.getSet(KEY_SCHEDULED)
        if (!enabled(prefs)) { cancelAll(ctx, prefs); return }

        val now = Instant.now()
        val keep = mutableSetOf<String>()
        for (b in bookings) {
            if (b.status != "BOOKED") continue
            val start = runCatching { Instant.parse(b.startTime) }.getOrNull() ?: continue
            val wait = Duration.between(now, start.minus(Duration.ofMinutes(LEAD_MINUTES)))
            if (wait.isNegative) continue   // less than an hour away: too late for a "1 hour before" reminder
            val request = OneTimeWorkRequestBuilder<ClassReminderWorker>()
                .setInitialDelay(wait.toMinutes().coerceAtLeast(0), TimeUnit.MINUTES)
                .setInputData(workDataOf("bookingId" to b.id, "name" to b.className, "location" to b.location, "start" to b.startTime))
                .build()
            work.enqueueUniqueWork("reminder-${b.id}", ExistingWorkPolicy.REPLACE, request)
            keep += b.id.toString()
        }
        (previous - keep).forEach { work.cancelUniqueWork("reminder-$it") }
        prefs.putSet(KEY_SCHEDULED, keep)
    }

    fun cancelAll(ctx: Context, prefs: LocalPrefs) {
        val work = WorkManager.getInstance(ctx)
        prefs.getSet(KEY_SCHEDULED).forEach { work.cancelUniqueWork("reminder-$it") }
        prefs.putSet(KEY_SCHEDULED, emptySet())
    }

    fun setEnabled(ctx: Context, prefs: LocalPrefs, on: Boolean) {
        prefs.putBool(KEY_ENABLED, on)
        if (!on) cancelAll(ctx, prefs) else NotificationSync.runOnce(ctx)   // fetch bookings and schedule them now
    }
}
