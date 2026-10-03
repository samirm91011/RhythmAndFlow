package com.rhythmandflow.app.ui.screens

import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.foundation.verticalScroll
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
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.rhythmandflow.app.data.ErrorLogItem
import com.rhythmandflow.app.ui.components.EmptyState
import com.rhythmandflow.app.ui.components.ErrorBox
import com.rhythmandflow.app.ui.components.InfoPill
import com.rhythmandflow.app.ui.components.LoadingBox
import com.rhythmandflow.app.ui.components.OnResume
import com.rhythmandflow.app.ui.components.ScreenHeader
import com.rhythmandflow.app.ui.components.SelectChip
import com.rhythmandflow.app.ui.components.SoftCard
import com.rhythmandflow.app.ui.components.VSpace
import com.rhythmandflow.app.ui.components.timeAgo
import com.rhythmandflow.app.ui.theme.Brand
import com.rhythmandflow.app.ui.viewmodel.AdminErrorsViewModel
import com.rhythmandflow.app.ui.viewmodel.appViewModel
import kotlinx.coroutines.launch

/** What went wrong for customers and on the server, newest first. Administrators only (the server enforces it). */
@Composable
fun AdminErrorsScreen(onBack: () -> Unit, notify: (String) -> Unit) {
    val vm = appViewModel(key = "admin-errors") { AdminErrorsViewModel(it) }
    val state by vm.items.collectAsState()
    val status by vm.status.collectAsState()
    val scope = rememberCoroutineScope()
    var open by remember { mutableStateOf<ErrorLogItem?>(null) }
    var confirmAll by remember { mutableStateOf(false) }
    OnResume { vm.load() }
    val data = state.data

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Error log", "Problems reported by the app and the server.", onBack = onBack) {
            if (status == "NEW" && !data.isNullOrEmpty()) TextButton(onClick = { confirmAll = true }) { Text("Resolve all", color = Brand.TealDeep) }
        }
        Row(Modifier.padding(horizontal = 20.dp).horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            listOf("NEW" to "Open", "RESOLVED" to "Resolved", "ALL" to "All").forEach { (value, label) ->
                SelectChip(label, status == value, onClick = { vm.setStatus(value) })
            }
        }
        VSpace(8)
        when {
            state.loading && data == null -> LoadingBox()
            state.error != null && data == null -> ErrorBox(state.error!!, onRetry = vm::load)
            data.isNullOrEmpty() -> EmptyState(
                if (status == "NEW") "No open problems" else "Nothing here",
                "When something goes wrong for a customer or on the server, it appears here and you get an alert.",
            )
            else -> LazyColumn(contentPadding = PaddingValues(horizontal = 20.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                items(data, key = { it.id }) { e -> ErrorCard(e, onClick = { open = e }) }
            }
        }
    }

    open?.let { e ->
        AlertDialog(
            onDismissRequest = { open = null },
            title = { Text(e.message, maxLines = 3, style = MaterialTheme.typography.titleMedium) },
            text = {
                Column(Modifier.heightIn(max = 420.dp).verticalScroll(rememberScrollState())) {
                    Text(
                        "${e.source} · seen ${e.count}× · first ${timeAgo(e.firstSeen)} · last ${timeAgo(e.lastSeen)}\n" +
                            "Where: ${e.route ?: "-"}\nWho: ${e.userEmail ?: "not signed in"}\nDevice: ${e.device ?: "-"}  App: ${e.appVersion ?: "-"}",
                        style = MaterialTheme.typography.bodySmall, color = Brand.Muted,
                    )
                    VSpace(10)
                    SelectionContainer { Text(e.details.ifBlank { "No technical details." }, fontFamily = FontFamily.Monospace, fontSize = 11.sp) }
                }
            },
            confirmButton = {
                if (e.status == "NEW") TextButton(onClick = {
                    open = null
                    scope.launch { notify(vm.resolve(e.id) ?: "Marked as resolved.") }
                }) { Text("Mark resolved", color = Brand.TealDeep) }
                else TextButton(onClick = { open = null }) { Text("Close") }
            },
            dismissButton = { if (e.status == "NEW") TextButton(onClick = { open = null }) { Text("Close") } },
        )
    }

    if (confirmAll) {
        AlertDialog(
            onDismissRequest = { confirmAll = false },
            title = { Text("Resolve all open problems?") },
            text = { Text("They'll move to Resolved. If one happens again you'll be alerted again.") },
            confirmButton = {
                TextButton(onClick = { confirmAll = false; scope.launch { notify(vm.resolveAll() ?: "All marked as resolved.") } }) { Text("Resolve all", color = Brand.TealDeep) }
            },
            dismissButton = { TextButton(onClick = { confirmAll = false }) { Text("Cancel") } },
        )
    }
}

@Composable
private fun ErrorCard(e: ErrorLogItem, onClick: () -> Unit) {
    val crash = e.message.startsWith("[CRASH]")
    SoftCard(Modifier.fillMaxWidth(), onClick = onClick, background = if (e.status == "NEW") Brand.TangerineSoft else Color.White) {
        Column(Modifier.fillMaxWidth()) {
            Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                InfoPill(if (e.source == "APP") "App" else "Server", color = Brand.White, textColor = Brand.TealDeep)
                if (crash) InfoPill("Crash", color = Brand.Tangerine, textColor = Brand.White)
                if (e.count > 1) InfoPill("${e.count}×", color = Brand.White, textColor = Brand.Muted)
            }
            VSpace(6)
            Text(e.message.removePrefix("[CRASH] "), style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.SemiBold, maxLines = 3)
            VSpace(2)
            Text(
                listOfNotNull(timeAgo(e.lastSeen), e.route, e.userEmail).joinToString(" · "),
                style = MaterialTheme.typography.bodySmall, color = Brand.Muted, maxLines = 2,
            )
        }
    }
}
