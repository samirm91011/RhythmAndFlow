package com.rhythmandflow.app.ui.screens

import android.content.Intent
import android.graphics.BitmapFactory
import android.net.Uri
import androidx.compose.foundation.Image
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Event
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Place
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.rhythmandflow.app.data.Booking
import com.rhythmandflow.app.data.ClassItem
import com.rhythmandflow.app.data.Credits
import com.rhythmandflow.app.data.Outcome
import com.rhythmandflow.app.data.TicketQuote
import com.rhythmandflow.app.ui.components.*
import com.rhythmandflow.app.ui.theme.Brand
import com.rhythmandflow.app.ui.viewmodel.ClassesViewModel
import com.rhythmandflow.app.ui.viewmodel.appViewModel
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

private fun kindLabel(kind: String) = when (kind) {
    "WORKSHOP" -> "Workshop"; "RETREAT" -> "Retreat"; "COACHING" -> "Coaching"; else -> "Class"
}

private fun formatLabel(format: String) = if (format == "ONLINE") "Online" else "In person"

/** The event poster, loaded from the address the administrator entered. Nothing is shown if it can't be loaded. */
@Composable
private fun Poster(url: String?) {
    if (url.isNullOrBlank() || !url.startsWith("https://")) return
    var image by remember(url) { mutableStateOf<androidx.compose.ui.graphics.ImageBitmap?>(null) }
    LaunchedEffect(url) {
        image = withContext(Dispatchers.IO) {
            runCatching { java.net.URL(url).openStream().use { BitmapFactory.decodeStream(it) }?.asImageBitmap() }.getOrNull()
        }
    }
    image?.let {
        Image(it, null, Modifier.fillMaxWidth().height(150.dp).clip(RoundedCornerShape(14.dp)), contentScale = ContentScale.Crop)
        VSpace(8)
    }
}

@Composable
fun ClassesScreen(onBookings: () -> Unit, onPayment: (Int) -> Unit, onPolicies: () -> Unit, notify: (String) -> Unit) {
    val vm = appViewModel(key = "classes") { ClassesViewModel(it) }
    val state by vm.state.collectAsState()
    val scope = rememberCoroutineScope()
    val context = LocalContext.current
    var busyId by remember { mutableStateOf<Int?>(null) }
    var buying by remember { mutableStateOf<ClassItem?>(null) }
    var quote by remember { mutableStateOf<TicketQuote?>(null) }
    OnResume { vm.load() }

    // Fetch what the ticket would cost me (after any account credit) when the buy dialog opens.
    LaunchedEffect(buying?.id) {
        quote = null
        buying?.let { quote = vm.quote(it.id) }
    }

    fun startTicket(c: ClassItem) {
        busyId = c.id
        scope.launch {
            when (val r = vm.buyTicket(c.id)) {
                is Outcome.Ok -> {
                    val t = r.value
                    if (t.confirmed || t.paymentUrl == null) notify(t.message)
                    else {
                        // Payment happens on PayFast's own hosted page, not inside this app.
                        try { context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(t.paymentUrl))) } catch (e: Exception) { notify("Couldn't open the payment page.") }
                        onPayment(t.bookingId)
                    }
                }
                is Outcome.Fail -> notify(r.message)
            }
            busyId = null
            buying = null
        }
    }

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Classes", "Reserve your spot and move with the community.") {
            TextButton(onClick = onBookings) { Text("My bookings", color = Brand.TealDeep) }
        }
        when {
            state.loading -> LoadingBox()
            state.error != null -> ErrorBox(state.error!!, onRetry = { vm.load() })
            state.classes.isEmpty() -> EmptyState("No upcoming classes", "New classes will appear here as soon as they are scheduled.")
            else -> LazyColumn(
                contentPadding = PaddingValues(horizontal = 20.dp, vertical = 8.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                items(state.classes, key = { it.id }) { c ->
                    ClassCard(
                        c, busy = busyId == c.id,
                        onBook = {
                            busyId = c.id
                            scope.launch {
                                val err = vm.book(c.id)
                                busyId = null
                                notify(err ?: "You're booked in for ${c.name}.")
                            }
                        },
                        onBuy = { buying = c },
                        onWaitlist = {
                            busyId = c.id
                            scope.launch {
                                val err = vm.joinWaitlist(c.id)
                                busyId = null
                                notify(err ?: "You're on the waiting list. We'll tell you if a place opens.")
                            }
                        },
                    )
                }
            }
        }
    }

    buying?.let { c ->
        AlertDialog(
            onDismissRequest = { if (busyId == null) buying = null },
            title = { Text("Buy a ticket") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(c.name, style = MaterialTheme.typography.titleMedium)
                    Text("${formatDay(c.startTime)} · ${formatTime(c.startTime)} – ${formatTime(c.endTime)} (SA time)", color = Brand.Muted)
                    Text("${formatRand(c.price)} including VAT.")
                    val q = quote
                    if (q != null && q.creditApplied > 0) {
                        Text("You have ${formatRand(q.creditAvailable)} account credit. ${formatRand(q.creditApplied)} will be used, so you pay ${formatRand(q.amountDue)}.", color = Brand.TealDeep)
                    }
                    Text(
                        "Cancel at least 48 hours before the event for a full refund or account credit. Closer than that, a refund or credit isn't guaranteed.",
                        color = Brand.Muted, style = MaterialTheme.typography.bodySmall,
                    )
                    Text(
                        "Read the cancellation policy", color = Brand.TealDeep, style = MaterialTheme.typography.bodyMedium,
                        modifier = Modifier.clickable { buying = null; onPolicies() },
                    )
                    if (c.format == "ONLINE") Text("A separate email with the joining link will follow.", color = Brand.Muted, style = MaterialTheme.typography.bodySmall)
                }
            },
            confirmButton = {
                TextButton(enabled = busyId == null && quote != null, onClick = { startTicket(c) }) {
                    Text(
                        when {
                            busyId != null -> "Working…"
                            quote?.amountDue == 0.0 -> "Book with credit"
                            else -> "Continue to payment"
                        },
                    )
                }
            },
            dismissButton = { TextButton(enabled = busyId == null, onClick = { buying = null }) { Text("Not now") } },
        )
    }
}

@Composable
private fun ClassCard(c: ClassItem, busy: Boolean, onBook: () -> Unit, onBuy: () -> Unit, onWaitlist: () -> Unit) {
    val paid = c.price > 0
    SoftCard(Modifier.fillMaxWidth(), background = Color.White) {
        Column(Modifier.fillMaxWidth()) {
            Poster(c.posterUrl)
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(c.name, style = MaterialTheme.typography.titleLarge, modifier = Modifier.weight(1f))
                when {
                    c.bookedByMe -> InfoPill("Booked")
                    c.spotsLeft == 0 -> InfoPill("Full", color = Brand.LightGrey, textColor = Brand.Muted)
                    c.spotsLeft <= 5 -> InfoPill("${c.spotsLeft} spots left", color = Brand.TangerineSoft, textColor = Brand.TangerineDeep)
                    else -> InfoPill("${c.spotsLeft} spots")
                }
            }
            if (paid || c.kind != "CLASS" || c.format == "ONLINE") {
                VSpace(4)
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalAlignment = Alignment.CenterVertically) {
                    if (c.kind != "CLASS") InfoPill(kindLabel(c.kind), color = Brand.Surface, textColor = Brand.Muted)
                    InfoPill(formatLabel(c.format), color = Brand.Surface, textColor = Brand.Muted)
                    if (paid) InfoPill(formatRand(c.price), color = Brand.TealSoft, textColor = Brand.TealDeep)
                }
            }
            VSpace(6)
            DetailLine(Icons.Default.Event, "${formatDay(c.startTime)} · ${formatTime(c.startTime)} – ${formatTime(c.endTime)} (SA time)")
            DetailLine(Icons.Default.Person, c.coachName)
            DetailLine(Icons.Default.Place, if (c.format == "ONLINE") "Online${c.joinPlatform?.let { " on $it" }.orEmpty()}" else c.location)
            if (c.description.isNotBlank()) { VSpace(6); Text(c.description, style = MaterialTheme.typography.bodyMedium, color = Brand.Muted) }
            if (paid && c.memberEarlyHours > 0 && c.salesOpenAt != null) {
                VSpace(4)
                Text("Members of the second plan can buy ${c.memberEarlyHours} hours before everyone else, and go first on the waiting list.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
            }
            VSpace(12)
            when {
                c.bookedByMe -> SecondaryButton("You're booked in", onClick = {}, enabled = false)
                !paid && c.spotsLeft == 0 -> SecondaryButton("Class is full", onClick = {}, enabled = false)
                !paid -> PrimaryButton("Book this class", onClick = onBook, loading = busy)
                c.canBuyNow -> PrimaryButton(if (c.onWaitlist) "A place is held for you: buy ticket" else "Buy ticket · ${formatRand(c.price)}", onClick = onBuy, loading = busy)
                c.onWaitlist -> SecondaryButton("On the waiting list", onClick = {}, enabled = false)
                c.spotsLeft == 0 && c.waitlistEnabled && c.buyBlockedReason?.contains("sold out") == true ->
                    SecondaryButton("Join the waiting list", onClick = onWaitlist, enabled = !busy)
                else -> {
                    SecondaryButton(if (c.spotsLeft == 0) "Sold out" else "Not available", onClick = {}, enabled = false)
                    c.buyBlockedReason?.let { Text(it, style = MaterialTheme.typography.bodySmall, color = Brand.Muted, modifier = Modifier.padding(top = 4.dp)) }
                }
            }
        }
    }
}

@Composable
private fun DetailLine(icon: androidx.compose.ui.graphics.vector.ImageVector, text: String) {
    Row(Modifier.padding(vertical = 2.dp), verticalAlignment = Alignment.CenterVertically) {
        Icon(icon, null, tint = Brand.Teal, modifier = Modifier.size(18.dp))
        HSpace(8)
        Text(text, style = MaterialTheme.typography.bodyMedium)
    }
}

/** Waits for PayFast to confirm the ticket payment to our server. The app never decides a payment succeeded. */
@Composable
fun TicketPaymentScreen(bookingId: Int, onDone: () -> Unit, onBack: () -> Unit) {
    val vm = appViewModel(key = "classes") { ClassesViewModel(it) }
    var booking by remember { mutableStateOf<Booking?>(null) }
    var timedOut by remember { mutableStateOf(false) }

    LaunchedEffect(bookingId) {
        var tries = 0
        while (tries < 60) {
            vm.load()
            delay(3000)
            val found = vm.state.value.bookings.firstOrNull { it.id == bookingId && it.status == "BOOKED" }
            if (found != null) { booking = found; return@LaunchedEffect }
            tries++
        }
        timedOut = true
    }

    Column(Modifier.fillMaxSize().padding(24.dp), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center) {
        val b = booking
        when {
            b != null -> {
                Icon(Icons.Default.CheckCircle, null, tint = Brand.TealDeep, modifier = Modifier.size(72.dp))
                VSpace(12)
                Text("You're booked!", style = MaterialTheme.typography.headlineMedium)
                Text("${b.className} on ${formatDayTime(b.startTime)}. Reference ${b.reference}. We've emailed your confirmation.", color = Brand.Muted, textAlign = TextAlign.Center)
                VSpace(24)
                PrimaryButton("View my bookings", onClick = onDone)
            }
            timedOut -> {
                Text("Still waiting for confirmation", style = MaterialTheme.typography.headlineMedium, textAlign = TextAlign.Center)
                Text("If you completed the payment, it can take a few minutes. Your booking will appear in My bookings, and we'll email you.", color = Brand.Muted, textAlign = TextAlign.Center)
                VSpace(24)
                PrimaryButton("View my bookings", onClick = onDone)
            }
            else -> {
                CircularProgressIndicator(color = Brand.TealDeep)
                VSpace(16)
                Text("Complete your payment", style = MaterialTheme.typography.headlineMedium)
                Text(
                    "Finish paying on the PayFast page in your browser, then come back here. Your ticket is held for a short time while you pay.",
                    color = Brand.Muted, textAlign = TextAlign.Center,
                )
                VSpace(24)
                SecondaryButton("Back", onClick = onBack)
            }
        }
    }
}

/** A significant technical problem can be reported for an online class once it has started and for 48 hours after it ends. */
private fun canReportProblem(b: Booking): Boolean = try {
    val now = java.time.Instant.now()
    b.format == "ONLINE" && b.status == "BOOKED" && now.isAfter(java.time.Instant.parse(b.startTime)) &&
        now.isBefore(java.time.Instant.parse(b.endTime).plus(java.time.Duration.ofHours(48)))
} catch (_: Exception) { false }

@Composable
fun BookingsScreen(onBack: () -> Unit, onBrowse: () -> Unit, onPolicies: () -> Unit, notify: (String) -> Unit) {
    val vm = appViewModel(key = "bookings") { ClassesViewModel(it) }
    val state by vm.state.collectAsState()
    val scope = rememberCoroutineScope()
    val context = LocalContext.current
    var confirm by remember { mutableStateOf<Booking?>(null) }
    var cancelTicket by remember { mutableStateOf<Booking?>(null) }
    var credits by remember { mutableStateOf<Credits?>(null) }
    var choice by remember { mutableStateOf("CREDIT") }
    var note by remember { mutableStateOf("") }
    var working by remember { mutableStateOf(false) }
    var message by remember { mutableStateOf<String?>(null) }
    var report by remember { mutableStateOf<Booking?>(null) }
    var reportText by remember { mutableStateOf("") }
    OnResume { vm.load(); scope.launch { credits = vm.credits() } }

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("My bookings", "Your upcoming classes and events.", onBack = onBack)
        when {
            state.loading -> LoadingBox()
            state.error != null -> ErrorBox(state.error!!, onRetry = { vm.load() })
            state.bookings.isEmpty() && (credits?.balance ?: 0.0) <= 0.0 -> Column(Modifier.fillMaxWidth().padding(horizontal = 20.dp)) {
                EmptyState("No bookings yet", "Reserve a class and it will show up here.")
                PrimaryButton("Browse classes", onClick = onBrowse)
            }
            else -> LazyColumn(contentPadding = PaddingValues(horizontal = 20.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                credits?.takeIf { it.balance > 0 }?.let { c ->
                    item {
                        SoftCard(Modifier.fillMaxWidth(), background = Brand.TealSoft) {
                            Column {
                                Text("Account credit: ${formatRand(c.balance)}", style = MaterialTheme.typography.titleMedium)
                                c.items.firstOrNull()?.let { Text("Valid until ${formatDate(it.expiresAt)}. It is used automatically when you buy a ticket.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted) }
                            }
                        }
                    }
                }
                items(state.bookings, key = { it.id }) { b ->
                    SoftCard(Modifier.fillMaxWidth()) {
                        Column(Modifier.fillMaxWidth()) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Text(b.className, style = MaterialTheme.typography.titleLarge, modifier = Modifier.weight(1f))
                                when {
                                    b.choiceNeeded != null -> InfoPill("Your choice", color = Brand.TangerineSoft, textColor = Brand.TangerineDeep)
                                    b.status == "WAITLISTED" -> InfoPill("Waiting list", color = Brand.LightGrey, textColor = Brand.Muted)
                                    b.requestStatus == "PENDING" -> InfoPill("Request sent")
                                    b.requestStatus == "DECLINED" -> InfoPill("Booked", color = Brand.TealSoft, textColor = Brand.TealDeep)
                                    else -> InfoPill("Confirmed")
                                }
                            }
                            VSpace(4)
                            DetailLine(Icons.Default.Event, "${formatDay(b.startTime)} · ${formatTime(b.startTime)} – ${formatTime(b.endTime)} (SA time)")
                            DetailLine(Icons.Default.Person, b.coachName)
                            DetailLine(Icons.Default.Place, if (b.format == "ONLINE") "Online${b.joinPlatform?.let { " on $it" }.orEmpty()}" else b.location)
                            if (b.totalPaid > 0) Text("Paid ${formatRand(b.totalPaid)} · Reference ${b.reference}", style = MaterialTheme.typography.bodySmall, color = Brand.Muted, modifier = Modifier.padding(top = 4.dp))
                            if (b.refundStatus == "REQUESTED") Text("Refund on its way. We'll tell you when it's sent.", style = MaterialTheme.typography.bodySmall, color = Brand.TealDeep)
                            VSpace(10)
                            when {
                                b.choiceNeeded != null -> {
                                    Text(
                                        if (b.choiceNeeded == "EVENT_CANCELLED") "Sorry, this event was cancelled. Choose a full refund or account credit."
                                        else "This event moved to a new time. Keep your place, or take a full refund or account credit.",
                                        style = MaterialTheme.typography.bodyMedium,
                                    )
                                    VSpace(8)
                                    if (b.choiceNeeded == "EVENT_RESCHEDULED") PrimaryButton("Keep my place", onClick = {
                                        scope.launch { when (val r = vm.choose(b.id, "KEEP")) { is Outcome.Ok -> notify(r.value.message); is Outcome.Fail -> notify(r.message) } }
                                    })
                                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                        SecondaryButton("Full refund", onClick = {
                                            scope.launch { when (val r = vm.choose(b.id, "REFUND")) { is Outcome.Ok -> message = r.value.message; is Outcome.Fail -> notify(r.message) } }
                                        }, modifier = Modifier.weight(1f))
                                        SecondaryButton("Account credit", onClick = {
                                            scope.launch { when (val r = vm.choose(b.id, "CREDIT")) { is Outcome.Ok -> message = r.value.message; is Outcome.Fail -> notify(r.message) } }
                                        }, modifier = Modifier.weight(1f))
                                    }
                                }
                                b.status == "WAITLISTED" -> SecondaryButton("Leave the waiting list", onClick = {
                                    scope.launch { when (val r = vm.cancelTicket(b.id, null, null)) { is Outcome.Ok -> notify(r.value.message); is Outcome.Fail -> notify(r.message) } }
                                })
                                else -> {
                                    if (b.joinLink != null) PrimaryButton("Open the joining link", onClick = {
                                        try { context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(b.joinLink))) } catch (e: Exception) { notify("Couldn't open the link.") }
                                    })
                                    if (canReportProblem(b)) { VSpace(6); SecondaryButton("Report a problem", onClick = { reportText = ""; report = b }) }
                                    if (b.requestStatus == "PENDING") Text("We've received your cancellation request and will handle it fairly.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                                    else if (b.canCancel) {
                                        VSpace(6)
                                        SecondaryButton(if (b.totalPaid > 0) "Cancel ticket" else "Cancel booking", onClick = {
                                            if (b.totalPaid > 0) { choice = "CREDIT"; note = ""; cancelTicket = b } else confirm = b
                                        })
                                    } else if (b.totalPaid <= 0) Text("Too close to the class start to cancel online.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                                }
                            }
                        }
                    }
                }
                item {
                    Text(
                        "Cancellation and refund policies", color = Brand.TealDeep, textAlign = TextAlign.Center, style = MaterialTheme.typography.bodyMedium,
                        modifier = Modifier.fillMaxWidth().clickable(onClick = onPolicies).padding(vertical = 12.dp),
                    )
                }
            }
        }
    }

    confirm?.let { b ->
        AlertDialog(
            onDismissRequest = { confirm = null },
            title = { Text("Cancel this booking?") },
            text = { Text("${b.className} on ${formatDayTime(b.startTime)}. Your spot will be released for someone else.") },
            confirmButton = {
                TextButton(onClick = {
                    confirm = null
                    scope.launch { notify(vm.cancel(b.id) ?: "Booking cancelled.") }
                }) { Text("Cancel booking", color = Brand.Error) }
            },
            dismissButton = { TextButton(onClick = { confirm = null }) { Text("Keep it") } },
        )
    }

    cancelTicket?.let { b ->
        AlertDialog(
            onDismissRequest = { if (!working) cancelTicket = null },
            title = { Text("Cancel your ticket?") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("${b.className} on ${formatDayTime(b.startTime)}. You paid ${formatRand(b.totalPaid)}.")
                    if (b.freeCancellation) Text("There are more than 48 hours to go, so you can have the full fee back.", color = Brand.TealDeep)
                    else Text("This is less than 48 hours before the event, so a refund or credit isn't guaranteed. We'll look at your request fairly. Up to half of the fee can be agreed.", color = Brand.Muted)
                    Text(if (b.freeCancellation) "I'd like:" else "I'd prefer:", style = MaterialTheme.typography.labelMedium, color = Brand.Muted)
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        SelectChip("Account credit", choice == "CREDIT", onClick = { choice = "CREDIT" })
                        SelectChip("Refund", choice == "REFUND", onClick = { choice = "REFUND" })
                    }
                    Text("Account credit can be used towards any other event for 12 months.", color = Brand.Muted, style = MaterialTheme.typography.bodySmall)
                    if (!b.freeCancellation) RfTextField(note, { note = it.take(500) }, "Anything we should know? (optional)", singleLine = false, minLines = 2)
                    Text(
                        "Read the cancellation policy", color = Brand.TealDeep, style = MaterialTheme.typography.bodyMedium,
                        modifier = Modifier.clickable { cancelTicket = null; onPolicies() },
                    )
                }
            },
            confirmButton = {
                TextButton(enabled = !working, onClick = {
                    working = true
                    scope.launch {
                        when (val r = vm.cancelTicket(b.id, choice, note.trim().ifBlank { null })) {
                            is Outcome.Ok -> message = r.value.message
                            is Outcome.Fail -> notify(r.message)
                        }
                        working = false; cancelTicket = null
                        credits = vm.credits()
                    }
                }) { Text(if (working) "Working…" else if (b.freeCancellation) "Cancel ticket" else "Send request", color = Brand.Error) }
            },
            dismissButton = { TextButton(enabled = !working, onClick = { cancelTicket = null }) { Text("Keep it") } },
        )
    }

    report?.let { b ->
        AlertDialog(
            onDismissRequest = { if (!working) report = null },
            title = { Text("Report a problem") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("${b.className}. Tell us what went wrong and about how long it lasted. We'll look into it and email you with what we can offer.")
                    RfTextField(reportText, { reportText = it.take(1000) }, "What happened?", singleLine = false, minLines = 3)
                }
            },
            confirmButton = {
                TextButton(enabled = !working && reportText.trim().length >= 10, onClick = {
                    working = true
                    scope.launch {
                        when (val r = vm.reportIssue(b.id, reportText.trim())) {
                            is Outcome.Ok -> { report = null; message = r.value.message }
                            is Outcome.Fail -> notify(r.message)
                        }
                        working = false
                    }
                }) { Text(if (working) "Sending…" else "Send report") }
            },
            dismissButton = { TextButton(enabled = !working, onClick = { report = null }) { Text("Cancel") } },
        )
    }

    message?.let { msg ->
        AlertDialog(
            onDismissRequest = { message = null; scope.launch { credits = vm.credits() } },
            title = { Text("Done") },
            text = { Text(msg) },
            confirmButton = { TextButton(onClick = { message = null; scope.launch { credits = vm.credits() } }) { Text("OK") } },
        )
    }
}
