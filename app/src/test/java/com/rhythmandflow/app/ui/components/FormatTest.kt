package com.rhythmandflow.app.ui.components

import java.time.Instant
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.LocalTime
import java.time.ZoneId
import java.time.temporal.ChronoUnit
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class FormatTest {
    @Test fun `whole rand amounts have no cents`() {
        assertEquals("R199", formatRand(199.0))
    }

    @Test fun `rand amounts with cents show two decimals`() {
        assertEquals("R99.50", formatRand(99.5))
    }

    @Test fun `admin local date and time convert to the right UTC instant`() {
        val expected = LocalDateTime.of(LocalDate.of(2026, 10, 7), LocalTime.of(9, 30)).atZone(ZoneId.systemDefault()).toInstant().toString()
        assertEquals(expected, localToUtcIso("2026-10-07", "09:30"))
    }

    @Test fun `surrounding spaces are ignored when converting`() {
        assertNotNull(localToUtcIso(" 2026-10-07 ", " 09:30 "))
    }

    @Test fun `invalid dates and times give null instead of crashing`() {
        assertNull(localToUtcIso("07/10/2026", "09:30"))
        assertNull(localToUtcIso("2026-10-07", "9.30am"))
        assertNull(localToUtcIso("", ""))
    }

    @Test fun `unreadable timestamps give empty text`() {
        assertEquals("", formatDay(null))
        assertEquals("", formatTime("not a date"))
        assertEquals("", formatDate(""))
    }

    @Test fun `time is shown in 24 hour form`() {
        val iso = LocalDateTime.of(2026, 10, 7, 17, 5).atZone(ZoneId.systemDefault()).toInstant().toString()
        assertEquals("17:05", formatTime(iso))
    }

    @Test fun `timeAgo buckets`() {
        fun ago(unit: ChronoUnit, n: Long) = Instant.now().minus(n, unit).toString()
        assertEquals("Just now", timeAgo(ago(ChronoUnit.SECONDS, 10)))
        assertEquals("5 min ago", timeAgo(ago(ChronoUnit.MINUTES, 5)))
        assertEquals("3 h ago", timeAgo(ago(ChronoUnit.HOURS, 3)))
        assertEquals("Yesterday", timeAgo(ago(ChronoUnit.HOURS, 30)))
    }

    @Test fun `timeAgo copes with missing or broken input`() {
        assertEquals("", timeAgo(null))
        assertEquals("", timeAgo("garbage"))
    }

    @Test fun `older times fall back to a date`() {
        assertTrue(timeAgo(Instant.now().minus(10, ChronoUnit.DAYS).toString()).isNotEmpty())
    }

    @Test fun `greeting is one of the three phrases`() {
        assertTrue(greeting() in setOf("Good morning", "Good afternoon", "Good evening"))
    }
}
