package com.rhythmandflow.app.ui.screens

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CalendarMonth
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.onClick
import androidx.compose.ui.semantics.role
import java.time.LocalDate
import java.time.ZoneOffset
import java.time.format.DateTimeFormatter
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.Notifications
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import com.rhythmandflow.app.data.Outcome
import com.rhythmandflow.app.data.VideoUploader
import com.rhythmandflow.app.data.ClassUpsert
import com.rhythmandflow.app.data.LessonUpsert
import com.rhythmandflow.app.data.AdminPlan
import com.rhythmandflow.app.data.PlanUpsert
import com.rhythmandflow.app.ui.components.*
import com.rhythmandflow.app.ui.theme.Brand
import com.rhythmandflow.app.ui.viewmodel.AdminViewModel
import com.rhythmandflow.app.ui.viewmodel.appViewModel
import java.time.Instant
import kotlinx.coroutines.launch

/** The administrator's home. The server enforces the ADMIN role on every call; these screens are only a convenience. */
@Composable
fun AdminHomeScreen(name: String, onNavigate: (String) -> Unit, onCustomerView: () -> Unit) {
    val vm = appViewModel(key = "admin") { AdminViewModel(it) }
    val state by vm.state.collectAsState()
    OnResume { vm.load() }
    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Admin dashboard", if (name.isBlank()) "Manage Rhythm & Flow." else "Signed in as $name") {
            IconButton(onClick = { onNavigate("notifications") }) { Icon(Icons.Default.Notifications, "Notifications") }
        }
        when {
            state.loading -> LoadingBox()
            state.error != null -> ErrorBox(state.error!!, onRetry = { vm.load() })
            else -> Column(Modifier.vScroll().padding(20.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                state.summary?.let { s ->
                    Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        Stat("${s.users}", "customers", Modifier.weight(1f)) { onNavigate("admin/users") }
                        Stat("${s.activeSubscriptions}", "active plans", Modifier.weight(1f)) { onNavigate("admin/subscriptions") }
                    }
                    Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        Stat("${s.upcomingClasses}", "upcoming classes", Modifier.weight(1f)) { onNavigate("admin/classes") }
                        Stat("${s.activeBookings}", "bookings", Modifier.weight(1f)) { onNavigate("admin/bookings") }
                    }
                    Stat(formatRand(s.monthlyRecurringRevenue), "monthly recurring revenue", Modifier.fillMaxWidth())
                    if (s.openErrors > 0) AdminLink("Needs attention", "${s.openErrors} open problem(s) in the error log") { onNavigate("admin/errors") }
                }
                Text("Manage", style = MaterialTheme.typography.titleMedium)
                AdminLink("Classes & events", "Schedule classes, workshops and retreats, set ticket prices, see who is booked") { onNavigate("admin/classes") }
                AdminLink("Event requests & refunds", "Late cancellation requests and refunds to pay on PayFast") { onNavigate("admin/events") }
                AdminLink("Lessons & videos", "Add, edit or remove lessons") { onNavigate("admin/lessons") }
                AdminLink("Programmes", "Add, edit or hide programmes") { onNavigate("admin/programmes") }
                AdminLink("Subscription plans", "Add plans, change prices, hide a plan") { onNavigate("admin/plans") }
                Text("Review", style = MaterialTheme.typography.titleMedium)
                AdminLink("Bookings", "Everyone booked into an upcoming class") { onNavigate("admin/bookings") }
                AdminLink("Active plans", "Who is subscribed, and to what") { onNavigate("admin/subscriptions") }
                AdminLink("Customers", "Find people and switch accounts off") { onNavigate("admin/users") }
                AdminLink("Error log", if ((state.summary?.openErrors ?: 0) > 0) "${state.summary?.openErrors} open problem(s) to review" else "Nothing open") { onNavigate("admin/errors") }
            }
        }
    }
}

@Composable
private fun Stat(value: String, label: String, modifier: Modifier, onClick: (() -> Unit)? = null) {
    SoftCard(modifier, onClick = onClick) {
        Column(Modifier.fillMaxWidth()) {
            Text(value, style = MaterialTheme.typography.headlineSmall, color = Brand.TealDeep)
            Text(label, style = MaterialTheme.typography.labelMedium, color = Brand.Muted)
        }
    }
}

@Composable
private fun AdminLink(title: String, sub: String, onClick: () -> Unit) {
    SoftCard(Modifier.fillMaxWidth(), onClick = onClick) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Column(Modifier.weight(1f)) {
                Text(title, style = MaterialTheme.typography.titleMedium)
                Text(sub, style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
            }
            Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, null, tint = Brand.Muted)
        }
    }
}

@Composable
fun AdminLessonsScreen(onBack: () -> Unit, notify: (String) -> Unit) {
    val vm = appViewModel(key = "admin") { AdminViewModel(it) }
    val state by vm.state.collectAsState()
    val scope = rememberCoroutineScope()
    var delete by remember { mutableStateOf<Int?>(null) }

    val programmes = state.lessons.map { it.programmeId to it.programmeName }.distinct()
    var title by remember { mutableStateOf("") }
    var description by remember { mutableStateOf("") }
    var category by remember { mutableStateOf("Yoga") }
    var seconds by remember { mutableStateOf("") }
    var url by remember { mutableStateOf("") }
    var preview by remember { mutableStateOf(false) }
    var programmeId by remember { mutableStateOf<Int?>(null) }
    var editingId by remember { mutableStateOf<Int?>(null) }
    var level by remember { mutableStateOf("Beginner") }
    var provider by remember { mutableStateOf("S3") }
    var fileName by remember { mutableStateOf("") }
    var uploadPct by remember { mutableStateOf<Int?>(null) }
    var uploadError by remember { mutableStateOf<String?>(null) }
    val context = androidx.compose.ui.platform.LocalContext.current
    val chosenProgramme = programmeId ?: programmes.firstOrNull()?.first
    val scroll = rememberScrollState()
    fun clearForm() { editingId = null; title = ""; description = ""; category = "Yoga"; seconds = ""; url = ""; preview = false; level = "Beginner"; provider = "S3"; fileName = ""; uploadError = null }
    fun fileLabel(key: String) = key.substringAfterLast('/').replace(Regex("^[0-9a-f]{32}-"), "")

    // Picks a video from the phone and sends it straight to the private AWS storage with a signed link from the server.
    val picker = rememberLauncherForActivityResult(ActivityResultContracts.OpenDocument()) { uri ->
        if (uri != null) scope.launch {
            uploadError = null
            val video = VideoUploader.inspect(context, uri)
            if (video == null || video.sizeBytes <= 0) { uploadError = "That file couldn't be read."; return@launch }
            uploadPct = 0
            when (val r = vm.videoUploadUrl(video.name, video.mimeType, video.sizeBytes)) {
                is Outcome.Fail -> { uploadError = r.message; uploadPct = null }
                is Outcome.Ok -> {
                    val err = VideoUploader.upload(context, video, r.value.uploadUrl, r.value.contentType) { uploadPct = it }
                    uploadPct = null
                    if (err == null) {
                        url = r.value.key; fileName = video.name
                        if (seconds.isBlank() && video.seconds > 0) seconds = video.seconds.toString()
                    } else uploadError = err
                }
            }
        }
    }

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Lessons & videos", onBack = onBack)
        Column(Modifier.verticalScroll(scroll).padding(20.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text(if (editingId == null) "Add a lesson" else "Edit lesson", style = MaterialTheme.typography.titleLarge)
            Text("Programme", style = MaterialTheme.typography.labelMedium, color = Brand.Muted)
            programmes.chunked(2).forEach { row ->
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    row.forEach { (id, name) -> SelectChip(name, chosenProgramme == id, onClick = { programmeId = id }) }
                }
            }
            RfTextField(title, { title = it }, "Title")
            RfTextField(description, { description = it }, "Description", singleLine = false, minLines = 2)
            Text("Category", style = MaterialTheme.typography.labelMedium, color = Brand.Muted)
            categoryNames.chunked(2).forEach { pair ->
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    pair.forEach { c -> SelectChip(c, category == c, { category = c }) }
                }
            }
            categoryDescriptions[category]?.let { Text(it, style = MaterialTheme.typography.bodySmall, color = Brand.Muted) }
            Text("Level", style = MaterialTheme.typography.labelMedium, color = Brand.Muted)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                levelNames.forEach { l -> SelectChip(l, level == l, { level = l }) }
            }
            RfTextField(seconds, { seconds = it.filter(Char::isDigit) }, "Length in seconds (filled in when you upload)", keyboardType = KeyboardType.Number)
            Text("Where is the video?", style = MaterialTheme.typography.labelMedium, color = Brand.Muted)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                SelectChip("Upload a video", provider == "S3", onClick = { if (uploadPct == null) { provider = "S3"; url = ""; fileName = ""; uploadError = null } })
                SelectChip("YouTube", provider == "YouTube", onClick = { if (uploadPct == null) { provider = "YouTube"; url = ""; uploadError = null } })
                SelectChip("Video link", provider == "Remote", onClick = { if (uploadPct == null) { provider = "Remote"; url = ""; uploadError = null } })
            }
            if (provider == "S3") {
                if (url.startsWith("videos/")) Text("✔ ${fileName.ifBlank { fileLabel(url) }} is uploaded.", style = MaterialTheme.typography.bodyMedium, color = Brand.TealDeep)
                else Text("Choose the video from your phone. It is stored privately, and only members whose plan allows it can watch it.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                SecondaryButton(if (url.startsWith("videos/")) "Replace the video" else "Choose a video to upload", enabled = uploadPct == null, onClick = { picker.launch(arrayOf("video/*")) })
                uploadPct?.let { p ->
                    LinearProgressIndicator(progress = { p / 100f }, modifier = Modifier.fillMaxWidth(), color = Brand.TealDeep)
                    Text("Uploading… $p%. Keep the app open until it finishes.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                }
                uploadError?.let { Text(it, style = MaterialTheme.typography.bodySmall, color = Brand.Error) }
            } else RfTextField(
                url, { url = it }, if (provider == "YouTube") "YouTube link (https://youtu.be/…)" else "Video address (https://…)",
                keyboardType = KeyboardType.Uri,
            )
            if (provider == "YouTube") Text("Play a public YouTube video inside the app. Tick free preview so anyone with an account can watch it.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
            Row(verticalAlignment = Alignment.CenterVertically) {
                Switch(checked = preview, onCheckedChange = { preview = it }, colors = SwitchDefaults.colors(checkedTrackColor = Brand.TealDeep))
                HSpace(10); Text("Free preview (no subscription needed)")
            }
            PrimaryButton(if (editingId == null) "Add lesson" else "Save changes", enabled = uploadPct == null && title.isNotBlank() && seconds.isNotBlank() && chosenProgramme != null &&
                (when (provider) { "S3" -> url.startsWith("videos/"); "YouTube" -> url.length > 5; else -> url.startsWith("http") }), onClick = {
                scope.launch {
                    val body = LessonUpsert(chosenProgramme!!, title.trim(), description.trim(), category, level, seconds.toInt().coerceAtLeast(1), provider, url.trim(), preview)
                    val id = editingId
                    val err = if (id == null) vm.createLesson(body) else vm.updateLesson(id, body)
                    if (err == null) clearForm()
                    notify(err ?: if (id == null) "Lesson added." else "Lesson updated.")
                }
            })
            if (editingId != null) SecondaryButton("Cancel editing", onClick = { clearForm() })
            VSpace(8)
            Text("Existing lessons", style = MaterialTheme.typography.titleLarge)
            state.lessons.forEach { l ->
                SoftCard(Modifier.fillMaxWidth()) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Column(Modifier.weight(1f)) {
                            Text(l.title, style = MaterialTheme.typography.titleSmall)
                            Text("${l.programmeName} · ${l.category} · ${l.durationLabel}", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                        }
                        IconButton(onClick = {
                            editingId = l.id; title = l.title; description = l.description; category = l.category; seconds = l.durationSeconds.toString()
                            provider = if (l.videoProvider.equals("YouTube", true)) "YouTube" else if (l.videoProvider.equals("S3", true)) "S3" else "Remote"
                            url = if (provider == "YouTube") "https://youtu.be/${l.videoReference}" else l.videoReference
                            fileName = if (provider == "S3") fileLabel(l.videoReference) else ""; uploadError = null
                            preview = l.isPreview; programmeId = l.programmeId; level = if (l.level in levelNames) l.level else "Beginner"
                            scope.launch { scroll.animateScrollTo(0) }
                        }) { Icon(Icons.Default.Edit, "Edit lesson", tint = Brand.TealDeep) }
                        IconButton(onClick = { delete = l.id }) { Icon(Icons.Default.Delete, "Delete lesson", tint = Brand.Error) }
                    }
                }
            }
        }
    }
    delete?.let { id ->
        AlertDialog(
            onDismissRequest = { delete = null },
            title = { Text("Delete this lesson?") }, text = { Text("Customers will no longer be able to watch it, and their progress on it is removed.") },
            confirmButton = { TextButton(onClick = { delete = null; scope.launch { notify(vm.deleteLesson(id) ?: "Lesson deleted.") } }) { Text("Delete", color = Brand.Error) } },
            dismissButton = { TextButton(onClick = { delete = null }) { Text("Keep it") } },
        )
    }
}

/** A field that shows the chosen value and opens a picker when tapped, so nothing has to be typed in a fixed format. */
@Composable
private fun PickerField(value: String, label: String, icon: ImageVector, modifier: Modifier = Modifier, onClick: () -> Unit) {
    Box(
        modifier.clearAndSetSemantics {
            contentDescription = "$label: ${value.ifBlank { "not chosen" }}"
            role = Role.Button
            onClick("Choose $label") { onClick(); true }
        },
    ) {
        RfTextField(value, {}, label, icon = icon)
        Box(Modifier.matchParentSize().clickable(onClick = onClick))
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AdminClassesScreen(onBack: () -> Unit, notify: (String) -> Unit, onAttendees: (Int) -> Unit = {}) {
    val vm = appViewModel(key = "admin") { AdminViewModel(it) }
    val state by vm.state.collectAsState()
    val scope = rememberCoroutineScope()
    var name by remember { mutableStateOf("") }
    var coach by remember { mutableStateOf("Deni") }
    var location by remember { mutableStateOf("") }
    var date by remember { mutableStateOf("") }
    var time by remember { mutableStateOf("") }
    var showDate by remember { mutableStateOf(false) }
    var showTime by remember { mutableStateOf(false) }
    var minutes by remember { mutableStateOf("60") }
    var capacity by remember { mutableStateOf("20") }
    var cancel by remember { mutableStateOf<Int?>(null) }
    var editingId by remember { mutableStateOf<Int?>(null) }
    var description by remember { mutableStateOf("All levels welcome.") }
    // Event details: what it is, what a ticket costs, how people join it
    var kind by remember { mutableStateOf("CLASS") }
    var format by remember { mutableStateOf("IN_PERSON") }
    var price by remember { mutableStateOf("") }
    var posterUrl by remember { mutableStateOf("") }
    var closeHours by remember { mutableStateOf("24") }
    var opensOn by remember { mutableStateOf("") }
    var earlyHours by remember { mutableStateOf("") }
    var platform by remember { mutableStateOf("Zoom") }
    var joinLink by remember { mutableStateOf("") }
    var practicalNote by remember { mutableStateOf("") }
    var waitlist by remember { mutableStateOf(false) }
    val scroll = rememberScrollState()
    fun clearForm() {
        editingId = null; name = ""; date = ""; time = ""; location = ""; minutes = "60"; capacity = "20"; description = "All levels welcome."
        kind = "CLASS"; format = "IN_PERSON"; price = ""; posterUrl = ""; closeHours = "24"; opensOn = ""; earlyHours = ""
        platform = "Zoom"; joinLink = ""; practicalNote = ""; waitlist = false
    }

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Classes", onBack = onBack)
        Column(Modifier.verticalScroll(scroll).padding(20.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text(if (editingId == null) "Schedule a class" else "Edit class", style = MaterialTheme.typography.titleLarge)
            RfTextField(name, { name = it }, "Class name")
            RfTextField(coach, { coach = it }, "Coach")
            RfTextField(location, { location = it }, "Location (studio or online)")
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                val dateShown = runCatching { LocalDate.parse(date).format(DateTimeFormatter.ofPattern("EEE d MMM yyyy")) }.getOrDefault("")
                PickerField(dateShown, "Date", Icons.Default.CalendarMonth, Modifier.weight(1f)) { showDate = true }
                PickerField(time, "Start time", Icons.Default.Schedule, Modifier.weight(1f)) { showTime = true }
            }
            if (showDate) {
                val today = LocalDate.now().atStartOfDay(ZoneOffset.UTC).toInstant().toEpochMilli()
                val picker = rememberDatePickerState(
                    initialSelectedDateMillis = runCatching { LocalDate.parse(date).atStartOfDay(ZoneOffset.UTC).toInstant().toEpochMilli() }.getOrDefault(today),
                    selectableDates = object : SelectableDates { override fun isSelectableDate(utcTimeMillis: Long) = utcTimeMillis >= today },
                )
                DatePickerDialog(
                    onDismissRequest = { showDate = false },
                    confirmButton = {
                        TextButton(onClick = {
                            picker.selectedDateMillis?.let { date = Instant.ofEpochMilli(it).atZone(ZoneOffset.UTC).toLocalDate().toString() }
                            showDate = false
                        }) { Text("OK") }
                    },
                    dismissButton = { TextButton(onClick = { showDate = false }) { Text("Cancel") } },
                ) { DatePicker(picker) }
            }
            if (showTime) {
                val picker = rememberTimePickerState(initialHour = time.substringBefore(':').toIntOrNull() ?: 9, initialMinute = time.substringAfter(':', "").toIntOrNull() ?: 0, is24Hour = true)
                AlertDialog(
                    onDismissRequest = { showTime = false },
                    text = { TimePicker(picker) },
                    confirmButton = { TextButton(onClick = { time = "%02d:%02d".format(picker.hour, picker.minute); showTime = false }) { Text("OK") } },
                    dismissButton = { TextButton(onClick = { showTime = false }) { Text("Cancel") } },
                )
            }
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Box(Modifier.weight(1f)) { RfTextField(minutes, { minutes = it.filter(Char::isDigit) }, "Minutes", keyboardType = KeyboardType.Number) }
                Box(Modifier.weight(1f)) { RfTextField(capacity, { capacity = it.filter(Char::isDigit) }, "Capacity", keyboardType = KeyboardType.Number) }
            }
            Text("What is it?", style = MaterialTheme.typography.labelMedium, color = Brand.Muted)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                listOf("CLASS" to "Class", "WORKSHOP" to "Workshop").forEach { (k, label) -> SelectChip(label, kind == k, onClick = { kind = k }) }
            }
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                listOf("RETREAT" to "Retreat", "COACHING" to "Coaching").forEach { (k, label) -> SelectChip(label, kind == k, onClick = { kind = k; if (k == "RETREAT") format = "IN_PERSON" }) }
            }
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                SelectChip("In person", format == "IN_PERSON", onClick = { format = "IN_PERSON" })
                if (kind != "RETREAT") SelectChip("Online", format == "ONLINE", onClick = { format = "ONLINE" })
            }
            RfTextField(price, { price = it.filter(Char::isDigit) }, "Ticket price in rand, VAT included (empty = free booking)", keyboardType = KeyboardType.Number)
            if ((price.toIntOrNull() ?: 0) > 0) {
                RfTextField(posterUrl, { posterUrl = it }, "Poster image address (https://…). 1200 x 630 px works best", keyboardType = KeyboardType.Uri)
                RfTextField(closeHours, { closeHours = it.filter(Char::isDigit) }, "Ticket sales close this many hours before the start", keyboardType = KeyboardType.Number)
                RfTextField(opensOn, { opensOn = it }, "Ticket sales open on (yyyy-MM-dd, optional)", keyboardType = KeyboardType.Ascii)
                RfTextField(earlyHours, { earlyHours = it.filter(Char::isDigit) }, "Members of the second plan can buy this many hours earlier", keyboardType = KeyboardType.Number)
                Text("Priority booking: this works together with the 'sales open on' date above. Second-plan members can buy this many hours before that date, and they go first when a place opens on the waiting list.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Switch(checked = waitlist, onCheckedChange = { waitlist = it }, colors = SwitchDefaults.colors(checkedTrackColor = Brand.TealDeep))
                    HSpace(10); Text("Waiting list when sold out")
                }
            }
            if (format == "ONLINE") {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    SelectChip("Zoom", platform == "Zoom", onClick = { platform = "Zoom" })
                    SelectChip("Google Meet", platform == "Google Meet", onClick = { platform = "Google Meet" })
                }
                RfTextField(joinLink, { joinLink = it }, "Joining link (https://…)", keyboardType = KeyboardType.Uri)
                Text("Ticket holders get the link in a separate email. You can add it later.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
            }
            RfTextField(practicalNote, { practicalNote = it }, "Practical note for the confirmation email (optional, replaces the standard one)", singleLine = false, minLines = 2)
            if (editingId != null) Text("Anyone already booked is told if the time or place changes. People who bought a ticket choose to keep it or take a refund or credit.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
            PrimaryButton(if (editingId == null) "Add class" else "Save changes", enabled = name.isNotBlank() && location.isNotBlank() && date.isNotBlank() && time.isNotBlank(), onClick = {
                val start = localToUtcIso(date, time)
                val mins = minutes.toIntOrNull() ?: 0
                val cap = capacity.toIntOrNull() ?: 0
                val ticket = price.toIntOrNull() ?: 0
                val opensIso = if (opensOn.isBlank()) null else localToUtcIso(opensOn, "00:00")
                when {
                    start == null -> notify("Check the date (yyyy-mm-dd) and time (HH:mm).")
                    mins < 5 || cap < 1 -> notify("Enter the class length and capacity.")
                    ticket in 1..4 -> notify("A ticket price must be free or at least R5.")
                    opensOn.isNotBlank() && opensIso == null -> notify("Enter the day ticket sales open as yyyy-MM-dd.")
                    else -> scope.launch {
                        val end = Instant.parse(start).plusSeconds(mins * 60L).toString()
                        val body = ClassUpsert(
                            name.trim(), description, coach.trim(), location.trim(), start, end, cap,
                            kind = kind, format = format, price = ticket.toDouble(),
                            posterUrl = posterUrl.trim().ifBlank { null }, salesCloseHours = closeHours.toIntOrNull() ?: 24,
                            salesOpenAt = opensIso, memberEarlyHours = earlyHours.toIntOrNull() ?: 0,
                            joinPlatform = if (format == "ONLINE") platform else null, joinLink = joinLink.trim().ifBlank { null },
                            practicalNote = practicalNote.trim().ifBlank { null }, waitlistEnabled = waitlist && ticket > 0,
                        )
                        val id = editingId
                        val err = if (id == null) vm.createClass(body) else vm.updateClass(id, body)
                        if (err == null) clearForm()
                        notify(err ?: if (id == null) "Class scheduled." else "Class updated.")
                    }
                }
            })
            if (editingId != null) SecondaryButton("Cancel editing", onClick = { clearForm() })
            VSpace(8)
            Text("Scheduled classes", style = MaterialTheme.typography.titleLarge)
            state.classes.forEach { c ->
                SoftCard(Modifier.fillMaxWidth(), onClick = { onAttendees(c.id) }) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Column(Modifier.weight(1f)) {
                            Text(c.name, style = MaterialTheme.typography.titleSmall)
                            Text("${formatDayTime(c.startTime)} · ${c.capacity - c.spotsLeft}/${c.capacity} booked", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                            if (c.price > 0 || c.format == "ONLINE") Text("${if (c.price > 0) formatRand(c.price) + " ticket · " else ""}${if (c.format == "ONLINE") "Online" else "In person"}", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                            Text("Tap to see who is booked", style = MaterialTheme.typography.labelSmall, color = Brand.TealDeep)
                        }
                        if (c.status == "SCHEDULED") {
                            Column(horizontalAlignment = Alignment.End) {
                                TextButton(onClick = {
                                    val s = parseInstant(c.startTime)
                                    val e = parseInstant(c.endTime)
                                    editingId = c.id; name = c.name; coach = c.coachName; location = c.location; description = c.description
                                    date = s?.toLocalDate()?.toString().orEmpty(); time = s?.let { "%02d:%02d".format(it.hour, it.minute) }.orEmpty()
                                    minutes = if (s != null && e != null) java.time.Duration.between(s, e).toMinutes().toString() else "60"
                                    capacity = c.capacity.toString()
                                    kind = c.kind; format = c.format; price = c.price.takeIf { it > 0 }?.toInt()?.toString().orEmpty()
                                    posterUrl = c.posterUrl.orEmpty(); closeHours = (c.salesCloseHours ?: 24).toString()
                                    opensOn = parseInstant(c.salesOpenAt)?.toLocalDate()?.toString().orEmpty()
                                    earlyHours = c.memberEarlyHours.takeIf { it > 0 }?.toString().orEmpty()
                                    platform = c.joinPlatform ?: "Zoom"; joinLink = c.joinLink.orEmpty(); practicalNote = c.practicalNote.orEmpty()
                                    waitlist = c.waitlistEnabled
                                    scope.launch { scroll.animateScrollTo(0) }
                                }) { Text("Edit", color = Brand.TealDeep) }
                                if (c.format == "ONLINE" && c.price > 0) TextButton(onClick = { scope.launch { notify(vm.sendLinks(c.id) ?: "Joining links sent to everyone who has not had one.") } }) { Text("Send links", color = Brand.TealDeep) }
                                TextButton(onClick = { cancel = c.id }) { Text("Cancel", color = Brand.Error) }
                            }
                        } else InfoPill("Cancelled", color = Brand.LightGrey, textColor = Brand.Muted)
                    }
                }
            }
        }
    }
    cancel?.let { id ->
        AlertDialog(
            onDismissRequest = { cancel = null },
            title = { Text("Cancel this class?") }, text = { Text("Everyone who booked is told. People who paid for a ticket are offered a full refund or account credit, their choice.") },
            confirmButton = { TextButton(onClick = { cancel = null; scope.launch { notify(vm.cancelClass(id) ?: "Class cancelled.") } }) { Text("Cancel class", color = Brand.Error) } },
            dismissButton = { TextButton(onClick = { cancel = null }) { Text("Keep it") } },
        )
    }
}

@Composable
fun AdminPlansScreen(onBack: () -> Unit, notify: (String) -> Unit) {
    val vm = appViewModel(key = "admin") { AdminViewModel(it) }
    val state by vm.state.collectAsState()
    var editing by remember { mutableStateOf<AdminPlan?>(null) }
    var adding by remember { mutableStateOf(false) }

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Subscription plans", onBack = onBack)
        Column(Modifier.vScroll().padding(20.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            PrimaryButton("Add plan", onClick = { adding = true })
            state.plans.forEach { p ->
                SoftCard(Modifier.fillMaxWidth(), onClick = { editing = p }) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Column(Modifier.weight(1f)) {
                            Text(p.name, style = MaterialTheme.typography.titleMedium)
                            Text("${formatRand(p.price)} / month · access level ${p.tier}", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                            p.annualPrice?.let { Text("${formatRand(it)} / year", style = MaterialTheme.typography.bodySmall, color = Brand.Muted) }
                            p.launchPrice?.let { Text("Launch: ${formatRand(it)} first month, until ${formatDate(p.launchEndsAt)}", style = MaterialTheme.typography.bodySmall, color = Brand.Muted) }
                            if (p.trialDays > 0) Text("${p.trialDays}-day free trial", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                            if (p.status != "ACTIVE") {
                                VSpace(4)
                                InfoPill("Hidden from customers", color = Brand.LightGrey, textColor = Brand.Muted)
                            }
                        }
                        Text("Edit", color = Brand.TealDeep)
                    }
                }
            }
        }
    }
    if (adding) PlanFormDialog(
        initial = null, onDismiss = { adding = false },
        onSave = { vm.createPlan(it) },
        onSaved = { adding = false; notify("Plan added.") },
    )
    editing?.let { p ->
        PlanFormDialog(
            initial = p, onDismiss = { editing = null },
            onSave = { vm.updatePlan(p.id, it) },
            onSaved = { editing = null; notify("Plan updated.") },
        )
    }
}

/** Add or edit a plan. Stays open and shows the server's message if the save is refused. */
@Composable
private fun PlanFormDialog(
    initial: AdminPlan?,
    onDismiss: () -> Unit,
    onSave: suspend (PlanUpsert) -> String?,
    onSaved: () -> Unit,
) {
    val scope = rememberCoroutineScope()
    var name by remember { mutableStateOf(initial?.name.orEmpty()) }
    var price by remember { mutableStateOf(initial?.price?.toInt()?.toString().orEmpty()) }
    var description by remember { mutableStateOf(initial?.description.orEmpty()) }
    var tier by remember { mutableStateOf(initial?.tier ?: 1) }
    var features by remember { mutableStateOf(initial?.features?.joinToString("\n").orEmpty()) }
    var visible by remember { mutableStateOf(initial?.status != "INACTIVE") }
    var annual by remember { mutableStateOf(initial?.annualPrice?.toInt()?.toString().orEmpty()) }
    var launch by remember { mutableStateOf(initial?.launchPrice?.toInt()?.toString().orEmpty()) }
    var launchEnds by remember { mutableStateOf(parseInstant(initial?.launchEndsAt)?.toLocalDate()?.toString().orEmpty()) }
    var trialDays by remember { mutableStateOf(initial?.trialDays?.takeIf { it > 0 }?.toString().orEmpty()) }
    var error by remember { mutableStateOf<String?>(null) }
    var saving by remember { mutableStateOf(false) }

    AlertDialog(
        onDismissRequest = { if (!saving) onDismiss() },
        title = { Text(if (initial == null) "Add plan" else "Edit plan") },
        text = {
            Column(Modifier.vScroll(), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                RfTextField(name, { name = it }, "Name")
                RfTextField(price, { price = it.filter(Char::isDigit) }, "Price per month (R)", keyboardType = KeyboardType.Number)
                RfTextField(description, { description = it }, "Short description", singleLine = false, minLines = 2)
                Text("Access level", style = MaterialTheme.typography.labelMedium, color = Brand.Muted)
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    (1..3).forEach { t -> SelectChip("Level $t", tier == t, onClick = { tier = t }) }
                }
                Text("A plan unlocks every programme at its level or below.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                RfTextField(features, { features = it }, "What's included (one per line)", singleLine = false, minLines = 3)
                Text("Billing options (all optional)", style = MaterialTheme.typography.labelMedium, color = Brand.Muted)
                RfTextField(annual, { annual = it.filter(Char::isDigit) }, "Price for a whole year (R). Leave empty for monthly only", keyboardType = KeyboardType.Number)
                RfTextField(launch, { launch = it.filter(Char::isDigit) }, "Launch price for the first month (R)", keyboardType = KeyboardType.Number)
                RfTextField(launchEnds, { launchEnds = it }, "Launch price ends on (yyyy-MM-dd)", keyboardType = KeyboardType.Ascii)
                RfTextField(trialDays, { trialDays = it.filter(Char::isDigit).take(2) }, "Free trial days (empty = no trial)", keyboardType = KeyboardType.Number)
                Text("The launch price and the free trial are only for people who have never held a plan.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Switch(checked = visible, onCheckedChange = { visible = it }, colors = SwitchDefaults.colors(checkedTrackColor = Brand.TealDeep))
                    HSpace(10); Text("Visible to customers")
                }
                if (initial != null) Text("Changing a price only affects new subscribers. Existing PayFast subscriptions keep their amount.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                error?.let { Text(it, color = Brand.Error, style = MaterialTheme.typography.bodySmall) }
            }
        },
        confirmButton = {
            TextButton(enabled = !saving, onClick = {
                val amount = price.toDoubleOrNull()
                if (name.isBlank() || amount == null || amount < 1) { error = "Enter a name and a price of at least R1."; return@TextButton }
                val launchAmount = launch.toDoubleOrNull()
                val endsIso = if (launchAmount == null) null else localToUtcIso(launchEnds, "23:59")
                if (launchAmount != null && endsIso == null) { error = "Enter the day the launch price ends as yyyy-MM-dd, for example 2026-11-30."; return@TextButton }
                saving = true; error = null
                scope.launch {
                    val problem = onSave(
                        PlanUpsert(
                            name.trim(), description.trim(), amount, tier, features.trim(), if (visible) "ACTIVE" else "INACTIVE",
                            annualPrice = annual.toDoubleOrNull(), launchPrice = launchAmount, launchEndsAt = endsIso,
                            trialDays = trialDays.toIntOrNull() ?: 0,
                        ),
                    )
                    saving = false
                    if (problem == null) onSaved() else error = problem
                }
            }) { Text(if (saving) "Saving…" else "Save") }
        },
        dismissButton = { TextButton(enabled = !saving, onClick = onDismiss) { Text("Cancel") } },
    )
}

/** Late cancellation requests to decide, and refunds waiting to be paid on PayFast. */
@Composable
fun AdminEventsScreen(onBack: () -> Unit, notify: (String) -> Unit) {
    val vm = appViewModel(key = "admin") { AdminViewModel(it) }
    val scope = rememberCoroutineScope()
    var requests by remember { mutableStateOf<List<com.rhythmandflow.app.data.AdminEventRequest>?>(null) }
    var refunds by remember { mutableStateOf<List<com.rhythmandflow.app.data.AdminRefund>?>(null) }
    val amounts = remember { mutableStateMapOf<Int, String>() }
    suspend fun refresh() { requests = vm.eventRequests(); refunds = vm.refunds() }
    LaunchedEffect(Unit) { refresh() }

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Event requests & refunds", onBack = onBack)
        if (requests == null || refunds == null) LoadingBox()
        else Column(Modifier.vScroll().padding(20.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text("Late cancellation requests", style = MaterialTheme.typography.titleLarge)
            Text("These came in less than 48 hours before the event. You can agree up to half of the fee, or decline.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
            if (requests!!.isEmpty()) Text("Nothing waiting.", color = Brand.Muted)
            requests!!.forEach { r ->
                SoftCard(Modifier.fillMaxWidth()) {
                    Column(Modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                        Text(r.eventName, style = MaterialTheme.typography.titleMedium)
                        Text("${r.fullName} · ${r.email}", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                        Text("${formatDayTime(r.startTime)} · paid ${formatRand(r.totalPaid)} · ${r.reference}", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                        Text("Asked for: ${if (r.choice == "REFUND") "a refund" else "account credit"}. You can agree up to ${formatRand(r.maxAmount)}.", style = MaterialTheme.typography.bodyMedium)
                        RfTextField(amounts[r.bookingId] ?: r.maxAmount.toInt().toString(), { amounts[r.bookingId] = it.filter(Char::isDigit) }, "Amount to agree (R)", keyboardType = KeyboardType.Number)
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            PrimaryButton("Approve", onClick = {
                                val amount = (amounts[r.bookingId] ?: r.maxAmount.toInt().toString()).toDoubleOrNull()
                                scope.launch { notify(vm.resolveRequest(r.bookingId, true, amount) ?: "Approved. The customer has been told."); refresh() }
                            }, modifier = Modifier.weight(1f))
                            SecondaryButton("Decline", onClick = {
                                scope.launch { notify(vm.resolveRequest(r.bookingId, false, null) ?: "Declined. The customer has been told."); refresh() }
                            }, modifier = Modifier.weight(1f))
                        }
                    }
                }
            }
            VSpace(8)
            Text("Refunds to pay on PayFast", style = MaterialTheme.typography.titleLarge)
            Text("Pay each refund from your PayFast dashboard, then mark it here so the customer is told it has been sent.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
            if (refunds!!.isEmpty()) Text("Nothing to pay.", color = Brand.Muted)
            refunds!!.forEach { r ->
                SoftCard(Modifier.fillMaxWidth()) {
                    Column(Modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                        Text("${formatRand(r.amount)} to ${r.fullName}", style = MaterialTheme.typography.titleMedium)
                        Text("${r.eventName} · ${r.reference}", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                        Text(r.email, style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                        r.payFastPaymentId?.let { Text("PayFast payment: $it", style = MaterialTheme.typography.bodySmall, color = Brand.Muted) }
                        SecondaryButton("I've paid this refund", onClick = {
                            scope.launch { notify(vm.markRefunded(r.bookingId) ?: "Marked as refunded. The customer has been told."); refresh() }
                        })
                    }
                }
            }
            VSpace(16)
        }
    }
}