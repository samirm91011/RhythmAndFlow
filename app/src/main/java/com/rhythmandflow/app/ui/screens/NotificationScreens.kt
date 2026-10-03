package com.rhythmandflow.app.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CreditCard
import androidx.compose.material.icons.filled.Event
import androidx.compose.material.icons.filled.Notifications
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.rhythmandflow.app.data.AppNotification
import com.rhythmandflow.app.ui.components.EmptyState
import com.rhythmandflow.app.ui.components.ErrorBox
import com.rhythmandflow.app.ui.components.LoadingBox
import com.rhythmandflow.app.ui.components.OnResume
import com.rhythmandflow.app.ui.components.ScreenHeader
import com.rhythmandflow.app.ui.components.SoftCard
import com.rhythmandflow.app.ui.components.timeAgo
import com.rhythmandflow.app.ui.theme.Brand
import com.rhythmandflow.app.ui.viewmodel.NotificationsViewModel
import com.rhythmandflow.app.ui.viewmodel.appViewModel

@Composable
fun NotificationsScreen(onBack: () -> Unit, onOpen: (String) -> Unit) {
    val vm = appViewModel(key = "notifications") { NotificationsViewModel(it) }
    val state by vm.items.collectAsState()
    OnResume { vm.load() }
    val data = state.data
    val unread = data?.count { !it.read } ?: 0

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Notifications", if (unread > 0) "$unread unread" else null, onBack = onBack) {
            if (unread > 0) TextButton(onClick = vm::markAllRead) { Text("Mark all read", color = Brand.TealDeep) }
        }
        when {
            state.loading && data == null -> LoadingBox()
            state.error != null && data == null -> ErrorBox(state.error!!, onRetry = vm::load)
            data.isNullOrEmpty() -> EmptyState("You're all caught up", "Updates about your plan, payments and classes will appear here.")
            else -> LazyColumn(contentPadding = PaddingValues(horizontal = 20.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                items(data, key = { it.id }) { n ->
                    NotificationRow(n, onClick = {
                        vm.markRead(n.id)
                        n.route?.let(onOpen)
                    })
                }
            }
        }
    }
}

@Composable
private fun NotificationRow(n: AppNotification, onClick: () -> Unit) {
    SoftCard(
        Modifier.fillMaxWidth().semantics { contentDescription = "${if (n.read) "" else "Unread. "}${n.title}. ${n.body}. ${timeAgo(n.createdAt)}" },
        onClick = onClick,
        background = if (n.read) Color.White else Brand.TealSoft,
    ) {
        Row(verticalAlignment = Alignment.Top) {
            Box(Modifier.size(40.dp).clip(CircleShape).background(Color.White), contentAlignment = Alignment.Center) {
                Icon(iconFor(n.kind), null, tint = if (n.kind == "ADMIN_ERROR") Brand.Tangerine else Brand.TealDeep)
            }
            Column(Modifier.weight(1f).padding(horizontal = 12.dp)) {
                Text(n.title, style = MaterialTheme.typography.titleSmall, fontWeight = if (n.read) FontWeight.Medium else FontWeight.Bold)
                Text(n.body, style = MaterialTheme.typography.bodyMedium, color = Brand.Muted)
                Text(timeAgo(n.createdAt), style = MaterialTheme.typography.labelSmall, color = Brand.Muted, modifier = Modifier.padding(top = 4.dp))
            }
            if (!n.read) Box(Modifier.padding(top = 6.dp).size(10.dp).clip(CircleShape).background(Brand.Tangerine))
        }
    }
}

private fun iconFor(kind: String): ImageVector = when (kind) {
    "PAYMENT", "SUBSCRIPTION" -> Icons.Default.CreditCard
    "CLASS", "BOOKING" -> Icons.Default.Event
    "ADMIN_ERROR" -> Icons.Default.Warning
    else -> Icons.Default.Notifications
}
