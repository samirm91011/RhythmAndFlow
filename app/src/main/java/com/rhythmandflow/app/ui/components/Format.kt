package com.rhythmandflow.app.ui.components

import java.time.Instant
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.LocalTime
import java.time.ZoneId
import java.time.ZonedDateTime
import java.time.format.DateTimeFormatter
import java.util.Locale

private val zone: ZoneId get() = ZoneId.systemDefault()

fun parseInstant(iso: String?): ZonedDateTime? = try {
    if (iso == null) null else Instant.parse(iso).atZone(zone)
} catch (_: Exception) { null }

fun formatDay(iso: String?): String =
    parseInstant(iso)?.format(DateTimeFormatter.ofPattern("EEE d MMM", Locale.getDefault())) ?: ""

fun formatTime(iso: String?): String =
    parseInstant(iso)?.format(DateTimeFormatter.ofPattern("HH:mm", Locale.getDefault())) ?: ""

fun formatDayTime(iso: String?): String = "${formatDay(iso)} · ${formatTime(iso)}"

fun formatDate(iso: String?): String =
    parseInstant(iso)?.format(DateTimeFormatter.ofPattern("d MMM yyyy", Locale.getDefault())) ?: ""

fun formatRand(amount: Double): String =
    if (amount % 1.0 == 0.0) "R${amount.toInt()}" else "R${"%.2f".format(Locale.US, amount)}"

/** Converts local date (yyyy-MM-dd) and time (HH:mm) typed by an admin into a UTC ISO string, or null if invalid. */
fun localToUtcIso(date: String, time: String): String? = try {
    LocalDateTime.of(LocalDate.parse(date.trim()), LocalTime.parse(time.trim())).atZone(zone).toInstant().toString()
} catch (_: Exception) { null }

fun greeting(): String {
    val h = ZonedDateTime.now(zone).hour
    return when {
        h < 12 -> "Good morning"
        h < 17 -> "Good afternoon"
        else -> "Good evening"
    }
}

/** "Just now", "5 min ago", "3 h ago", "Yesterday", or the date. */
fun timeAgo(iso: String?): String {
    val then = try { if (iso == null) return "" else Instant.parse(iso) } catch (_: Exception) { return "" }
    val minutes = java.time.Duration.between(then, Instant.now()).toMinutes()
    return when {
        minutes < 1 -> "Just now"
        minutes < 60 -> "$minutes min ago"
        minutes < 60 * 24 -> "${minutes / 60} h ago"
        minutes < 60 * 48 -> "Yesterday"
        else -> formatDate(iso)
    }
}