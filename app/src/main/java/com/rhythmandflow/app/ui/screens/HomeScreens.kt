package com.rhythmandflow.app.ui.screens

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.LocalIndication
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.selection.selectable
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.semantics.Role
import com.rhythmandflow.app.ui.components.pressScale
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.BatteryAlert
import androidx.compose.material.icons.filled.BlurOn
import androidx.compose.material.icons.filled.Cloud
import androidx.compose.material.icons.filled.Eco
import androidx.compose.material.icons.filled.FavoriteBorder
import androidx.compose.material.icons.filled.NightsStay
import androidx.compose.material.icons.filled.SentimentSatisfiedAlt
import androidx.compose.material.icons.filled.WbSunny
import androidx.compose.material3.Badge
import androidx.compose.material3.BadgedBox
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.foundation.layout.Spacer
import androidx.compose.material.icons.filled.Notifications
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.rhythmandflow.app.R
import com.rhythmandflow.app.ui.components.AccentButton
import com.rhythmandflow.app.ui.components.GradientBox
import com.rhythmandflow.app.ui.components.InfoPill
import com.rhythmandflow.app.ui.components.OnResume
import com.rhythmandflow.app.ui.components.PageColumn
import com.rhythmandflow.app.ui.components.PrimaryButton
import com.rhythmandflow.app.ui.components.ScreenHeader
import com.rhythmandflow.app.ui.components.SectionTitle
import com.rhythmandflow.app.ui.components.SoftCard
import com.rhythmandflow.app.ui.components.VSpace
import com.rhythmandflow.app.ui.components.formatDayTime
import com.rhythmandflow.app.ui.components.greeting
import com.rhythmandflow.app.ui.theme.Brand
import com.rhythmandflow.app.ui.viewmodel.HomeViewModel
import com.rhythmandflow.app.ui.viewmodel.SessionViewModel
import com.rhythmandflow.app.ui.viewmodel.appViewModel
import kotlinx.coroutines.launch

private data class Mood(val label: String, val icon: ImageVector, val tint: Color, val bg: Color)

private val moods = listOf(
    Mood("Energised", Icons.Default.WbSunny, Color(0xFFE08A1E), Color(0xFFFFF1DC)),
    Mood("Tired", Icons.Default.BatteryAlert, Color(0xFF3A6FD8), Color(0xFFE3ECFB)),
    Mood("Stressed", Icons.Default.Cloud, Color(0xFF9B5D7A), Color(0xFFF3E6EE)),
    Mood("Grounded", Icons.Default.Eco, Brand.TealDeep, Brand.TealSoft),
    Mood("Overwhelmed", Icons.Default.BlurOn, Brand.Tangerine, Brand.TangerineSoft),
    Mood("Happy", Icons.Default.SentimentSatisfiedAlt, Color(0xFFD9822B), Color(0xFFFFEFD9)),
    Mood("Low", Icons.Default.NightsStay, Color(0xFF4A5FB5), Color(0xFFE5E8F7)),
    Mood("Need some space", Icons.Default.FavoriteBorder, Color(0xFFC0443F), Color(0xFFFBE4E3)),
)

@Composable
fun HomeScreen(session: SessionViewModel, onNavigate: (String) -> Unit, notify: (String) -> Unit) {
    val vm = appViewModel { HomeViewModel(it) }
    val state by vm.state.collectAsState()
    val user = (session.state.collectAsState().value as? com.rhythmandflow.app.ui.viewmodel.SessionState.SignedIn)?.user
    val subs by session.subscriptions.collectAsState()
    var mood by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()
    val firstName = user?.fullName?.substringBefore(' ') ?: ""
    OnResume { vm.load(); session.refreshSubscriptions() }

    PageColumn {
        Row(Modifier.fillMaxWidth().padding(horizontal = 20.dp, vertical = 12.dp), verticalAlignment = Alignment.CenterVertically) {
            Image(painterResource(R.drawable.rf_logo_black), "Rhythm & Flow", Modifier.size(52.dp))
            Spacer(Modifier.weight(1f))
            BadgedBox(badge = { if (state.unread > 0) Badge(containerColor = Brand.Tangerine) { Text("${state.unread}") } }) {
                IconButton(onClick = { onNavigate("notifications") }) {
                    Icon(Icons.Default.Notifications, if (state.unread > 0) "Notifications, ${state.unread} unread" else "Notifications")
                }
            }
        }
        Column(Modifier.padding(horizontal = 20.dp)) {
            Text("${greeting()}, $firstName", style = MaterialTheme.typography.headlineLarge)
            VSpace(4)
            Text("Take a moment to check in with yourself. How are you feeling today?", color = Brand.Muted)
        }
        VSpace(16)
        MoodGrid(selected = mood, onSelect = { m ->
            mood = m
            scope.launch {
                val err = vm.checkIn(m)
                notify(err ?: "Thanks for checking in. You're feeling $m today.")
            }
        })

        VSpace(20)
        Column(Modifier.padding(horizontal = 20.dp)) {
            val next = state.nextBooking
            if (next != null) {
                SoftCard(onClick = { onNavigate("bookings") }, background = Brand.TealSoft, modifier = Modifier.fillMaxWidth()) {
                    Column {
                        InfoPill("Your next class")
                        VSpace(8)
                        Text(next.className, style = MaterialTheme.typography.titleLarge)
                        Text("${formatDayTime(next.startTime)} · ${next.location}", color = Brand.Muted, style = MaterialTheme.typography.bodyMedium)
                    }
                }
            } else {
                SoftCard(onClick = { onNavigate("classes") }, background = Brand.TangerineSoft, modifier = Modifier.fillMaxWidth()) {
                    Column {
                        Text("Join a class", style = MaterialTheme.typography.titleLarge)
                        Text("Move with Deni and the community. Browse upcoming classes and reserve your spot.", color = Brand.Muted, style = MaterialTheme.typography.bodyMedium)
                    }
                }
            }
        }

        VSpace(20)
        SectionTitle("Your Rhythm Today", action = "See All", onAction = { onNavigate("rhythm") })
        VSpace(8)
        RhythmCards(onNavigate)

        VSpace(20)
        state.summary?.let { s ->
            Row(Modifier.padding(horizontal = 20.dp).fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                StatCard("${s.sessionsThisMonth}", "sessions this month", Modifier.weight(1f))
                StatCard("${s.minutesWatched}", "minutes moved", Modifier.weight(1f))
                StatCard("${s.completedLessons}", "completed", Modifier.weight(1f))
            }
            VSpace(20)
        }

        if (subs.none { it.grantsAccess }) {
            Column(Modifier.padding(horizontal = 20.dp)) {
                GradientBox(Modifier.fillMaxWidth().clip(RoundedCornerShape(24.dp)), listOf(Brand.Black, Color(0xFF2B3A37))) {
                    Column(Modifier.fillMaxWidth().padding(20.dp)) {
                        Text("Build a life that feels good.", style = MaterialTheme.typography.headlineSmall, color = Color.White)
                        VSpace(6)
                        Text("Unlock every programme with a monthly subscription.", color = Color.White.copy(alpha = 0.85f), style = MaterialTheme.typography.bodyMedium)
                        VSpace(14)
                        AccentButton("See plans", onClick = { onNavigate("plans") })
                    }
                }
            }
            VSpace(20)
        }

        Column(Modifier.padding(horizontal = 20.dp)) {
            SoftCard(Modifier.fillMaxWidth()) {
                Text(
                    "“A small check-in can lead to a big shift.”",
                    style = MaterialTheme.typography.titleLarge, textAlign = TextAlign.Center, modifier = Modifier.fillMaxWidth(),
                    color = Brand.Muted,
                )
            }
        }
        VSpace(24)
    }
}

@Composable
private fun MoodGrid(selected: String?, onSelect: (String) -> Unit) {
    // With very large text, three tiles across would break words in the middle, so use two.
    val columns = if (androidx.compose.ui.platform.LocalDensity.current.fontScale > 1.3f) 2 else 3
    Column(Modifier.padding(horizontal = 20.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
        moods.chunked(columns).forEach { row ->
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                row.forEach { m ->
                    val isSel = selected == m.label
                    val interaction = remember { MutableInteractionSource() }
                    val shape = RoundedCornerShape(20.dp)
                    Column(
                        Modifier.weight(1f).heightIn(min = 48.dp).pressScale(interaction)
                            .clip(shape).background(m.bg)
                            .let { if (isSel) it.background(m.tint.copy(alpha = 0.18f)).border(2.dp, m.tint, shape) else it }
                            .selectable(selected = isSel, interactionSource = interaction, indication = LocalIndication.current, role = Role.RadioButton) { onSelect(m.label) }
                            .padding(vertical = 14.dp, horizontal = 4.dp),
                        horizontalAlignment = Alignment.CenterHorizontally,
                    ) {
                        Icon(m.icon, null, tint = m.tint, modifier = Modifier.size(28.dp))
                        VSpace(6)
                        Text(m.label, style = MaterialTheme.typography.labelMedium, textAlign = TextAlign.Center)
                    }
                }
                repeat(columns - row.size) { Box(Modifier.weight(1f)) }
            }
        }
    }
}

@Composable
private fun StatCard(value: String, label: String, modifier: Modifier = Modifier) {
    SoftCard(modifier) {
        Column(Modifier.fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally) {
            Text(value, style = MaterialTheme.typography.headlineSmall, color = Brand.TealDeep)
            Text(label, style = MaterialTheme.typography.labelSmall, color = Brand.Muted, textAlign = TextAlign.Center)
        }
    }
}

@Composable
private fun RhythmCards(onNavigate: (String) -> Unit) {
    Row(Modifier.padding(horizontal = 20.dp).fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
        RhythmCard("Morning Reset", "10 mins", Brand.TangerineSoft, Modifier.weight(1f)) { onNavigate("move") }
        RhythmCard("Nourish Your Body", "Recipes", Brand.TealSoft, Modifier.weight(1f)) { onNavigate("explore") }
        RhythmCard("Move & Release", "15 mins", Brand.Surface, Modifier.weight(1f)) { onNavigate("move") }
    }
}

@Composable
private fun RhythmCard(title: String, sub: String, bg: Color, modifier: Modifier, onClick: () -> Unit) {
    val interaction = remember { MutableInteractionSource() }
    val shape = RoundedCornerShape(22.dp)
    Column(
        modifier.heightIn(min = 120.dp).pressScale(interaction)
            .shadow(4.dp, shape, ambientColor = Brand.Shadow.copy(alpha = 0.10f), spotColor = Brand.Shadow.copy(alpha = 0.16f))
            .clip(shape).background(Brush.verticalGradient(listOf(Color.White, bg)))
            .clickable(interactionSource = interaction, indication = LocalIndication.current, role = Role.Button, onClick = onClick).padding(12.dp),
        verticalArrangement = Arrangement.Bottom,
    ) {
        Text(title, style = MaterialTheme.typography.titleMedium)
        Text(sub, style = MaterialTheme.typography.labelMedium, color = Brand.Muted)
    }
}

@Composable
fun RhythmTodayScreen(onBack: () -> Unit, onNavigate: (String) -> Unit) {
    PageColumn {
        ScreenHeader("Your Rhythm Today", "Small steps that add up.", onBack = onBack)
        RhythmCards(onNavigate)
        VSpace(20)
        Column(Modifier.padding(horizontal = 20.dp)) {
            GradientBox(Modifier.fillMaxWidth().clip(RoundedCornerShape(24.dp)), listOf(Brand.TealDeep, Brand.Teal)) {
                Column(Modifier.fillMaxWidth().padding(24.dp)) {
                    Text("Build a life that feels good.", style = MaterialTheme.typography.headlineMedium, color = Color.White)
                    VSpace(14)
                    PrimaryButtonOnDark("Explore Today") { onNavigate("explore") }
                }
            }
            VSpace(16)
            SoftCard(Modifier.fillMaxWidth()) {
                Text(
                    "“Progress isn’t about doing more, it’s about coming back to yourself.”",
                    style = MaterialTheme.typography.titleLarge, color = Brand.Muted, textAlign = TextAlign.Center, modifier = Modifier.fillMaxWidth(),
                )
            }
        }
        VSpace(24)
    }
}

@Composable
private fun PrimaryButtonOnDark(text: String, onClick: () -> Unit) {
    androidx.compose.material3.Button(
        onClick = onClick, shape = RoundedCornerShape(28.dp),
        colors = androidx.compose.material3.ButtonDefaults.buttonColors(containerColor = Color.White, contentColor = Brand.TealDeep),
    ) { Text(text) }
}
