package com.rhythmandflow.app.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.rhythmandflow.app.data.AdminProgramme
import com.rhythmandflow.app.data.Lesson
import com.rhythmandflow.app.data.ProgrammeUpsert
import com.rhythmandflow.app.ui.components.EmptyState
import com.rhythmandflow.app.ui.components.ErrorBox
import com.rhythmandflow.app.ui.components.InfoPill
import com.rhythmandflow.app.ui.components.LoadingBox
import com.rhythmandflow.app.ui.components.OnResume
import com.rhythmandflow.app.ui.components.PrimaryButton
import com.rhythmandflow.app.ui.components.RfTextField
import com.rhythmandflow.app.ui.components.ScreenHeader
import com.rhythmandflow.app.ui.components.SelectChip
import com.rhythmandflow.app.ui.components.SoftCard
import com.rhythmandflow.app.ui.components.VSpace
import com.rhythmandflow.app.ui.components.vScroll
import com.rhythmandflow.app.ui.theme.Brand
import com.rhythmandflow.app.ui.viewmodel.AdminProgrammesViewModel
import com.rhythmandflow.app.ui.viewmodel.ProgrammeViewModel
import com.rhythmandflow.app.ui.viewmodel.ProgressViewModel
import com.rhythmandflow.app.ui.viewmodel.appViewModel
import kotlinx.coroutines.launch

/** One programme: what it is, who it is included for, and its lessons (FR-03, FR-04). */
@Composable
fun ProgrammeScreen(programmeId: Int, onBack: () -> Unit, onLesson: (Int) -> Unit, onPlans: () -> Unit) {
    val vm = appViewModel(key = "programme$programmeId") { ProgrammeViewModel(it, programmeId) }
    val state by vm.state.collectAsState()
    OnResume { vm.load() }
    val programme = state.programme

    Column(Modifier.fillMaxSize()) {
        ScreenHeader(
            programme?.name ?: "Programme",
            programme?.let { "${it.lessonCount} ${if (it.lessonCount == 1) "practice" else "practices"}" },
            onBack = onBack,
        )
        when {
            state.loading -> LoadingBox()
            programme == null -> ErrorBox(state.error ?: "We couldn't find that programme.", onRetry = vm::load)
            else -> LazyColumn(contentPadding = PaddingValues(horizontal = 20.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                item {
                    Column(Modifier.fillMaxWidth()) {
                        if (programme.description.isNotBlank()) {
                            Text(programme.description, style = MaterialTheme.typography.bodyLarge, color = Brand.Muted)
                            VSpace(12)
                        }
                        if (programme.locked) {
                            SoftCard(Modifier.fillMaxWidth(), background = Brand.TangerineSoft) {
                                Column(Modifier.fillMaxWidth()) {
                                    Text(
                                        "Included in the ${state.planName ?: "paid"} plan and above.",
                                        style = MaterialTheme.typography.titleSmall,
                                    )
                                    Text("Subscribe to unlock every practice in this programme.", style = MaterialTheme.typography.bodyMedium, color = Brand.Muted)
                                    VSpace(10)
                                    PrimaryButton("View plans", onClick = onPlans)
                                }
                            }
                        } else {
                            InfoPill("Included in your plan")
                        }
                        VSpace(4)
                    }
                }
                if (state.lessons.isEmpty()) item { EmptyState("No practices yet", "New practices will appear here.") }
                items(state.lessons, key = { it.id }) { l -> LessonRow(l, onClick = { onLesson(l.id) }) }
            }
        }
    }
}

/** The person's workout progress (FR-13, FR-14): totals, practices in progress, finished ones. */
@Composable
fun ProgressScreen(onBack: () -> Unit, onLesson: (Int) -> Unit) {
    val vm = appViewModel(key = "progress") { ProgressViewModel(it) }
    val state by vm.state.collectAsState()
    OnResume { vm.load() }

    val started = state.lessons.filter { !it.locked && it.watchTimeSeconds > 0 }
    val inProgress = started.filter { it.completionPercentage < 95 }.sortedByDescending { it.completionPercentage }
    val finished = started.filter { it.completionPercentage >= 95 }

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("My progress", "Every practice you've started.", onBack = onBack)
        when {
            state.loading -> LoadingBox()
            state.error != null && state.lessons.isEmpty() -> ErrorBox(state.error!!, onRetry = vm::load)
            else -> LazyColumn(contentPadding = PaddingValues(horizontal = 20.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                state.summary?.let { s ->
                    item {
                        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                            ProgressStat("${s.sessionsThisMonth}", "sessions this month", Modifier.weight(1f))
                            ProgressStat("${s.minutesWatched}", "minutes moved", Modifier.weight(1f))
                            ProgressStat("${s.completedLessons}", "completed", Modifier.weight(1f))
                        }
                    }
                }
                if (started.isEmpty()) {
                    item { EmptyState("Nothing started yet", "Pick a practice in Move and your progress will show up here.") }
                }
                if (inProgress.isNotEmpty()) {
                    item { Text("In progress", style = MaterialTheme.typography.titleLarge) }
                    items(inProgress, key = { "p${it.id}" }) { l -> LessonRow(l, onClick = { onLesson(l.id) }) }
                }
                if (finished.isNotEmpty()) {
                    item { Text("Completed", style = MaterialTheme.typography.titleLarge) }
                    items(finished, key = { "c${it.id}" }) { l -> LessonRow(l, onClick = { onLesson(l.id) }) }
                }
            }
        }
    }
}

@Composable
private fun ProgressStat(value: String, label: String, modifier: Modifier = Modifier) {
    SoftCard(modifier) {
        Column(Modifier.fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally) {
            Text(value, style = MaterialTheme.typography.headlineSmall, color = Brand.TealDeep)
            Text(label, style = MaterialTheme.typography.labelSmall, color = Brand.Muted, textAlign = TextAlign.Center)
        }
    }
}

/** Administrators: add a programme, rename it, change which plan level unlocks it, or hide it (FR-22). */
@Composable
fun AdminProgrammesScreen(onBack: () -> Unit, notify: (String) -> Unit) {
    val vm = appViewModel(key = "admin-programmes") { AdminProgrammesViewModel(it) }
    val state by vm.items.collectAsState()
    val scope = rememberCoroutineScope()
    var name by remember { mutableStateOf("") }
    var description by remember { mutableStateOf("") }
    var level by remember { mutableStateOf(1) }
    var editing by remember { mutableStateOf<AdminProgramme?>(null) }
    OnResume { vm.load() }
    val data = state.data

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Programmes", "Group lessons into programmes.", onBack = onBack)
        Column(Modifier.vScroll().padding(20.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text("Add a programme", style = MaterialTheme.typography.titleLarge)
            RfTextField(name, { name = it }, "Programme name")
            RfTextField(description, { description = it }, "What it is about", singleLine = false, minLines = 2)
            LevelChips(level) { level = it }
            PrimaryButton("Add programme", enabled = name.isNotBlank(), onClick = {
                scope.launch {
                    val err = vm.create(ProgrammeUpsert(name.trim(), description.trim(), level, true))
                    if (err == null) { name = ""; description = ""; level = 1 }
                    notify(err ?: "Programme added.")
                }
            })
            VSpace(8)
            Text("Your programmes", style = MaterialTheme.typography.titleLarge)
            when {
                state.loading && data == null -> LoadingBox(Modifier.padding(vertical = 24.dp))
                state.error != null && data == null -> ErrorBox(state.error!!, onRetry = vm::load)
                else -> data.orEmpty().forEach { p ->
                    SoftCard(Modifier.fillMaxWidth()) {
                        Column(Modifier.fillMaxWidth()) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Column(Modifier.weight(1f)) {
                                    Text(p.name, style = MaterialTheme.typography.titleSmall)
                                    Text("Level ${p.minTier} · ${p.lessonCount} ${if (p.lessonCount == 1) "practice" else "practices"}", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                                }
                                if (!p.active) InfoPill("Hidden", color = Brand.LightGrey, textColor = Brand.Muted)
                            }
                            Row(horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                                TextButton(onClick = { editing = p }) { Text("Edit", color = Brand.TealDeep) }
                                TextButton(onClick = {
                                    scope.launch {
                                        val err = vm.update(p.id, ProgrammeUpsert(p.name, p.description, p.minTier, !p.active))
                                        notify(err ?: if (p.active) "Hidden from customers." else "Visible to customers again.")
                                    }
                                }) { Text(if (p.active) "Hide" else "Show", color = if (p.active) Brand.Error else Brand.TealDeep) }
                            }
                        }
                    }
                }
            }
            VSpace(16)
        }
    }

    editing?.let { p ->
        var n by remember(p.id) { mutableStateOf(p.name) }
        var d by remember(p.id) { mutableStateOf(p.description) }
        var lv by remember(p.id) { mutableStateOf(p.minTier) }
        AlertDialog(
            onDismissRequest = { editing = null },
            title = { Text("Edit programme") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    RfTextField(n, { n = it }, "Programme name")
                    RfTextField(d, { d = it }, "What it is about", singleLine = false, minLines = 2)
                    LevelChips(lv) { lv = it }
                }
            },
            confirmButton = {
                TextButton(enabled = n.isNotBlank(), onClick = {
                    editing = null
                    scope.launch { notify(vm.update(p.id, ProgrammeUpsert(n.trim(), d.trim(), lv, p.active)) ?: "Saved.") }
                }) { Text("Save", color = Brand.TealDeep) }
            },
            dismissButton = { TextButton(onClick = { editing = null }) { Text("Cancel") } },
        )
    }
}

@Composable
private fun LevelChips(level: Int, onChange: (Int) -> Unit) {
    Column {
        Text("Unlocked from plan level", style = MaterialTheme.typography.labelMedium, color = Brand.Muted)
        VSpace(4)
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            (1..3).forEach { SelectChip("Level $it", level == it, onClick = { onChange(it) }) }
        }
        Text("A plan unlocks programmes at its own level and below.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
    }
}
