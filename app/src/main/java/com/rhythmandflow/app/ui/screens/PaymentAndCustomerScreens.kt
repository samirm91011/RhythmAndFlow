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
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Search
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
import androidx.compose.ui.unit.dp
import com.rhythmandflow.app.data.AdminUser
import com.rhythmandflow.app.data.PaymentItem
import com.rhythmandflow.app.ui.components.EmptyState
import com.rhythmandflow.app.ui.components.ErrorBox
import com.rhythmandflow.app.ui.components.InfoPill
import com.rhythmandflow.app.ui.components.LoadingBox
import com.rhythmandflow.app.ui.components.OnResume
import com.rhythmandflow.app.ui.components.RfTextField
import com.rhythmandflow.app.ui.components.ScreenHeader
import com.rhythmandflow.app.ui.components.SoftCard
import com.rhythmandflow.app.ui.components.VSpace
import com.rhythmandflow.app.ui.components.formatDate
import com.rhythmandflow.app.ui.components.formatRand
import com.rhythmandflow.app.ui.theme.Brand
import com.rhythmandflow.app.ui.viewmodel.AdminUsersViewModel
import com.rhythmandflow.app.ui.viewmodel.PaymentsViewModel
import com.rhythmandflow.app.ui.viewmodel.appViewModel
import kotlinx.coroutines.launch

/** What the person has been charged, newest first, each with a receipt number they can quote to support. */
@Composable
fun PaymentHistoryScreen(onBack: () -> Unit) {
    val vm = appViewModel(key = "payments") { PaymentsViewModel(it) }
    val state by vm.items.collectAsState()
    OnResume { vm.load() }
    val data = state.data

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Payment history", "Your payments and receipts.", onBack = onBack)
        when {
            state.loading && data == null -> LoadingBox()
            state.error != null && data == null -> ErrorBox(state.error!!, onRetry = vm::load)
            data.isNullOrEmpty() -> EmptyState("No payments yet", "When you subscribe, each payment and its receipt number will appear here.")
            else -> LazyColumn(contentPadding = PaddingValues(horizontal = 20.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                items(data, key = { it.id }) { PaymentCard(it) }
            }
        }
    }
}

@Composable
private fun PaymentCard(p: PaymentItem) {
    val paid = p.status == "COMPLETE"
    SoftCard(Modifier.fillMaxWidth()) {
        Column(Modifier.fillMaxWidth()) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(p.planName, style = MaterialTheme.typography.titleMedium, modifier = Modifier.weight(1f))
                Text(formatRand(p.amount), style = MaterialTheme.typography.titleMedium, color = if (paid) Brand.TealDeep else Brand.Muted)
            }
            VSpace(4)
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text("${formatDate(p.date)} · Receipt ${p.receipt}", style = MaterialTheme.typography.bodySmall, color = Brand.Muted, modifier = Modifier.weight(1f))
                if (paid) InfoPill("Paid") else InfoPill("Didn't go through", color = Brand.TangerineSoft, textColor = Brand.TangerineDeep)
            }
        }
    }
}

/** Administrators: find a customer and switch their account off or back on. The server enforces the admin role. */
@Composable
fun AdminUsersScreen(onBack: () -> Unit, notify: (String) -> Unit) {
    val vm = appViewModel(key = "admin-users") { AdminUsersViewModel(it) }
    val state by vm.items.collectAsState()
    val query by vm.query.collectAsState()
    val scope = rememberCoroutineScope()
    var confirm by remember { mutableStateOf<AdminUser?>(null) }
    OnResume { vm.load() }
    val data = state.data

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Customers", "Find someone, or switch an account off.", onBack = onBack)
        Column(Modifier.padding(horizontal = 20.dp)) {
            RfTextField(query, vm::search, "Search name, username or email", Icons.Default.Search)
        }
        VSpace(8)
        when {
            state.loading && data == null -> LoadingBox()
            state.error != null && data == null -> ErrorBox(state.error!!, onRetry = vm::load)
            data.isNullOrEmpty() -> EmptyState("No one found", "Try a different name or email address.")
            else -> LazyColumn(contentPadding = PaddingValues(horizontal = 20.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                items(data, key = { it.id }) { u -> CustomerCard(u, onToggle = { confirm = u }) }
            }
        }
    }

    confirm?.let { u ->
        val disabling = u.status == "ACTIVE"
        AlertDialog(
            onDismissRequest = { confirm = null },
            title = { Text(if (disabling) "Switch off ${u.fullName}?" else "Switch ${u.fullName} back on?") },
            text = {
                Text(
                    if (disabling) "They will be signed out everywhere and won't be able to log in. Their data is kept, and any subscription is not cancelled automatically."
                    else "They will be able to log in again.",
                )
            },
            confirmButton = {
                TextButton(onClick = {
                    confirm = null
                    scope.launch { notify(vm.setStatus(u.id, if (disabling) "DISABLED" else "ACTIVE") ?: if (disabling) "Account switched off." else "Account switched back on.") }
                }) { Text(if (disabling) "Switch off" else "Switch on", color = if (disabling) Brand.Error else Brand.TealDeep) }
            },
            dismissButton = { TextButton(onClick = { confirm = null }) { Text("Cancel") } },
        )
    }
}

@Composable
private fun CustomerCard(u: AdminUser, onToggle: () -> Unit) {
    val off = u.status != "ACTIVE"
    SoftCard(Modifier.fillMaxWidth()) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Column(Modifier.weight(1f)) {
                Text(u.fullName, style = MaterialTheme.typography.titleSmall)
                Text("${u.email} · @${u.username}", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                VSpace(6)
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    if (u.role == "ADMIN") InfoPill("Admin", color = Brand.LightGrey, textColor = Brand.Ink)
                    u.plan?.let { InfoPill(it) }
                    if (off) InfoPill("Switched off", color = Brand.TangerineSoft, textColor = Brand.TangerineDeep)
                }
            }
            // Administrators are managed elsewhere; only customers can be switched off here.
            if (u.role == "CUSTOMER") TextButton(onClick = onToggle) {
                Text(if (off) "Switch on" else "Switch off", color = if (off) Brand.TealDeep else Brand.Error)
            }
        }
    }
}
