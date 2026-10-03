package com.rhythmandflow.app.ui.screens

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Event
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Place
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import com.rhythmandflow.app.data.Booking
import com.rhythmandflow.app.data.ClassItem
import com.rhythmandflow.app.ui.components.*
import com.rhythmandflow.app.ui.theme.Brand
import com.rhythmandflow.app.ui.viewmodel.ClassesViewModel
import com.rhythmandflow.app.ui.viewmodel.appViewModel
import kotlinx.coroutines.launch

@Composable
fun ClassesScreen(onBookings: () -> Unit, notify: (String) -> Unit) {
    val vm = appViewModel(key = "classes") { ClassesViewModel(it) }
    val state by vm.state.collectAsState()
    val scope = rememberCoroutineScope()
    var busyId by remember { mutableStateOf<Int?>(null) }
    OnResume { vm.load() }

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
                    ClassCard(c, busy = busyId == c.id, onBook = {
                        busyId = c.id
                        scope.launch {
                            val err = vm.book(c.id)
                            busyId = null
                            notify(err ?: "You're booked in for ${c.name}.")
                        }
                    })
                }
            }
        }
    }
}

@Composable
private fun ClassCard(c: ClassItem, busy: Boolean, onBook: () -> Unit) {
    SoftCard(Modifier.fillMaxWidth(), background = Color.White) {
        Column(Modifier.fillMaxWidth()) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(c.name, style = MaterialTheme.typography.titleLarge, modifier = Modifier.weight(1f))
                when {
                    c.bookedByMe -> InfoPill("Booked")
                    c.spotsLeft == 0 -> InfoPill("Full", color = Brand.LightGrey, textColor = Brand.Muted)
                    c.spotsLeft <= 5 -> InfoPill("${c.spotsLeft} spots left", color = Brand.TangerineSoft, textColor = Brand.Tangerine)
                    else -> InfoPill("${c.spotsLeft} spots")
                }
            }
            VSpace(6)
            DetailLine(Icons.Default.Event, "${formatDay(c.startTime)} · ${formatTime(c.startTime)} – ${formatTime(c.endTime)}")
            DetailLine(Icons.Default.Person, c.coachName)
            DetailLine(Icons.Default.Place, c.location)
            if (c.description.isNotBlank()) { VSpace(6); Text(c.description, style = MaterialTheme.typography.bodyMedium, color = Brand.Muted) }
            VSpace(12)
            when {
                c.bookedByMe -> SecondaryButton("You're booked in", onClick = {}, enabled = false)
                c.spotsLeft == 0 -> SecondaryButton("Class is full", onClick = {}, enabled = false)
                else -> PrimaryButton("Book this class", onClick = onBook, loading = busy)
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

@Composable
fun BookingsScreen(onBack: () -> Unit, onBrowse: () -> Unit, notify: (String) -> Unit) {
    val vm = appViewModel(key = "bookings") { ClassesViewModel(it) }
    val state by vm.state.collectAsState()
    val scope = rememberCoroutineScope()
    var confirm by remember { mutableStateOf<Booking?>(null) }
    OnResume { vm.load() }

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("My bookings", "Your upcoming classes.", onBack = onBack)
        when {
            state.loading -> LoadingBox()
            state.error != null -> ErrorBox(state.error!!, onRetry = { vm.load() })
            state.bookings.isEmpty() -> Column(Modifier.fillMaxWidth().padding(horizontal = 20.dp)) {
                EmptyState("No bookings yet", "Reserve a class and it will show up here.")
                PrimaryButton("Browse classes", onClick = onBrowse)
            }
            else -> LazyColumn(contentPadding = PaddingValues(horizontal = 20.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                items(state.bookings, key = { it.id }) { b ->
                    SoftCard(Modifier.fillMaxWidth()) {
                        Column(Modifier.fillMaxWidth()) {
                            Text(b.className, style = MaterialTheme.typography.titleLarge)
                            VSpace(4)
                            DetailLine(Icons.Default.Event, "${formatDay(b.startTime)} · ${formatTime(b.startTime)} – ${formatTime(b.endTime)}")
                            DetailLine(Icons.Default.Person, b.coachName)
                            DetailLine(Icons.Default.Place, b.location)
                            VSpace(10)
                            if (b.canCancel) SecondaryButton("Cancel booking", onClick = { confirm = b })
                            else Text("Too close to the class start to cancel online.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                        }
                    }
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
}
