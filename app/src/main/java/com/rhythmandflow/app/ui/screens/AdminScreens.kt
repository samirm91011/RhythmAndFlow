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
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
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

/** Administrator tools. The server enforces the ADMIN role on every call; these screens are only a convenience. */
@Composable
fun AdminHomeScreen(onBack: () -> Unit, onNavigate: (String) -> Unit) {
    val vm = appViewModel(key = "admin") { AdminViewModel(it) }
    val state by vm.state.collectAsState()
    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Admin tools", "Manage Rhythm & Flow.", onBack = onBack)
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
                }
                AdminLink("Error log", if ((state.summary?.openErrors ?: 0) > 0) "${state.summary?.openErrors} open problem(s) to review" else "Nothing open") { onNavigate("admin/errors") }
                AdminLink("Customers", "Find people and switch accounts off") { onNavigate("admin/users") }
                AdminLink("Programmes", "Add, edit or hide programmes") { onNavigate("admin/programmes") }
                AdminLink("Lessons & videos", "Add or remove lessons") { onNavigate("admin/lessons") }
                AdminLink("Classes", "Schedule and cancel classes, see who is booked") { onNavigate("admin/classes") }
                AdminLink("Bookings", "Everyone booked into an upcoming class") { onNavigate("admin/bookings") }
                AdminLink("Active plans", "Who is subscribed, and to what") { onNavigate("admin/subscriptions") }
                AdminLink("Subscription plans", "Edit names and prices") { onNavigate("admin/plans") }
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
    val chosenProgramme = programmeId ?: programmes.firstOrNull()?.first

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Lessons & videos", onBack = onBack)
        Column(Modifier.vScroll().padding(20.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text("Add a lesson", style = MaterialTheme.typography.titleLarge)
            Text("Programme", style = MaterialTheme.typography.labelMedium, color = Brand.Muted)
            programmes.chunked(2).forEach { row ->
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    row.forEach { (id, name) -> SelectChip(name, chosenProgramme == id, onClick = { programmeId = id }) }
                }
            }
            RfTextField(title, { title = it }, "Title")
            RfTextField(description, { description = it }, "Description", singleLine = false, minLines = 2)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                listOf("Yoga", "Barre", "Dance", "Stretch", "Meditation").chunked(3).first().forEach { c -> SelectChip(c, category == c, { category = c }) }
            }
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                listOf("Stretch", "Meditation").forEach { c -> SelectChip(c, category == c, { category = c }) }
            }
            RfTextField(seconds, { seconds = it.filter(Char::isDigit) }, "Length in seconds", keyboardType = KeyboardType.Number)
            RfTextField(url, { url = it }, "Video address (https://…)", keyboardType = KeyboardType.Uri)
            Row(verticalAlignment = Alignment.CenterVertically) {
                Switch(checked = preview, onCheckedChange = { preview = it }, colors = SwitchDefaults.colors(checkedTrackColor = Brand.TealDeep))
                HSpace(10); Text("Free preview (no subscription needed)")
            }
            PrimaryButton("Add lesson", enabled = title.isNotBlank() && seconds.isNotBlank() && url.startsWith("http") && chosenProgramme != null, onClick = {
                scope.launch {
                    val err = vm.createLesson(
                        LessonUpsert(chosenProgramme!!, title.trim(), description.trim(), category, "All levels", seconds.toInt().coerceAtLeast(1), "Remote", url.trim(), preview),
                    )
                    if (err == null) { title = ""; description = ""; seconds = ""; url = ""; preview = false }
                    notify(err ?: "Lesson added.")
                }
            })
            VSpace(8)
            Text("Existing lessons", style = MaterialTheme.typography.titleLarge)
            state.lessons.forEach { l ->
                SoftCard(Modifier.fillMaxWidth()) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Column(Modifier.weight(1f)) {
                            Text(l.title, style = MaterialTheme.typography.titleSmall)
                            Text("${l.programmeName} · ${l.category} · ${l.durationLabel}", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                        }
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

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Classes", onBack = onBack)
        Column(Modifier.vScroll().padding(20.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text("Schedule a class", style = MaterialTheme.typography.titleLarge)
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
            PrimaryButton("Add class", enabled = name.isNotBlank() && location.isNotBlank() && date.isNotBlank() && time.isNotBlank(), onClick = {
                val start = localToUtcIso(date, time)
                val mins = minutes.toIntOrNull() ?: 0
                val cap = capacity.toIntOrNull() ?: 0
                when {
                    start == null -> notify("Check the date (yyyy-mm-dd) and time (HH:mm).")
                    mins < 5 || cap < 1 -> notify("Enter the class length and capacity.")
                    else -> scope.launch {
                        val end = Instant.parse(start).plusSeconds(mins * 60L).toString()
                        val err = vm.createClass(ClassUpsert(name.trim(), "All levels welcome.", coach.trim(), location.trim(), start, end, cap))
                        if (err == null) { name = ""; date = ""; time = "" }
                        notify(err ?: "Class scheduled.")
                    }
                }
            })
            VSpace(8)
            Text("Scheduled classes", style = MaterialTheme.typography.titleLarge)
            state.classes.forEach { c ->
                SoftCard(Modifier.fillMaxWidth(), onClick = { onAttendees(c.id) }) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Column(Modifier.weight(1f)) {
                            Text(c.name, style = MaterialTheme.typography.titleSmall)
                            Text("${formatDayTime(c.startTime)} · ${c.capacity - c.spotsLeft}/${c.capacity} booked", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                            Text("Tap to see who is booked", style = MaterialTheme.typography.labelSmall, color = Brand.TealDeep)
                        }
                        if (c.status == "SCHEDULED") TextButton(onClick = { cancel = c.id }) { Text("Cancel", color = Brand.Error) }
                        else InfoPill("Cancelled", color = Brand.LightGrey, textColor = Brand.Muted)
                    }
                }
            }
        }
    }
    cancel?.let { id ->
        AlertDialog(
            onDismissRequest = { cancel = null },
            title = { Text("Cancel this class?") }, text = { Text("Everyone who booked will lose their reservation.") },
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
                saving = true; error = null
                scope.launch {
                    val problem = onSave(PlanUpsert(name.trim(), description.trim(), amount, tier, features.trim(), if (visible) "ACTIVE" else "INACTIVE"))
                    saving = false
                    if (problem == null) onSaved() else error = problem
                }
            }) { Text(if (saving) "Saving…" else "Save") }
        },
        dismissButton = { TextButton(enabled = !saving, onClick = onDismiss) { Text("Cancel") } },
    )
}
