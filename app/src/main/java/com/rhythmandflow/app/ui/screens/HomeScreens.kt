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

@Composable
fun HomeScreen(session: SessionViewModel, onNavigate: (String) -> Unit, notify: (String) -> Unit) {
    val vm = appViewModel { HomeViewModel(it) }
    val state by vm.state.collectAsState()
    val user = (session.state.collectAsState().value as? com.rhythmandflow.app.ui.viewmodel.SessionState.SignedIn)?.user
    val subs by session.subscriptions.collectAsState()
    var mood by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()
    val firstName = user?.fullName?.substringBefore(' ') ?: ""
    val content by com.rhythmandflow.app.data.ContentCache.flow.collectAsState()
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
        MoodGrid(content.moods, selected = mood, onSelect = { m ->
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
                        Text(content.home.classTitle, style = MaterialTheme.typography.titleLarge)
                        Text(content.home.classBody, color = Brand.Muted, style = MaterialTheme.typography.bodyMedium)
                    }
                }
            }
        }

        state.resume?.let { l ->
            VSpace(20)
            Column(Modifier.padding(horizontal = 20.dp)) {
                SoftCard(onClick = { onNavigate("lesson/${l.id}") }, modifier = Modifier.fillMaxWidth()) {
                    Column(Modifier.fillMaxWidth()) {
                        InfoPill("Continue where you left off")
                        VSpace(8)
                        Text(l.title, style = MaterialTheme.typography.titleLarge)
                        val left = ((l.durationSeconds - l.watchTimeSeconds).coerceAtLeast(0) + 59) / 60
                        Text("${l.completionPercentage.toInt()}% done · about $left min left", color = Brand.Muted, style = MaterialTheme.typography.bodyMedium)
                        VSpace(10)
                        androidx.compose.material3.LinearProgressIndicator(
                            progress = { (l.completionPercentage / 100.0).toFloat() },
                            modifier = Modifier.fillMaxWidth().height(6.dp).clip(CircleShape),
                            color = Brand.TealDeep, trackColor = Brand.TealSoft,
                        )
                    }
                }
            }
        }

        VSpace(20)
        SectionTitle("Your Rhythm Today", action = "See All", onAction = { onNavigate("rhythm") })
        VSpace(8)
        RhythmCards(content.rhythmCards, onNavigate)

        VSpace(20)
        state.summary?.let { s ->
            Row(Modifier.padding(horizontal = 20.dp).fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                StatCard("${s.sessionsThisMonth}", "sessions this month", Modifier.weight(1f), onClick = { onNavigate("progress") })
                StatCard("${s.minutesWatched}", "minutes moved", Modifier.weight(1f), onClick = { onNavigate("progress") })
                StatCard("${s.completedLessons}", "completed", Modifier.weight(1f), onClick = { onNavigate("progress") })
            }
            VSpace(20)
        }

        if (subs.none { it.grantsAccess }) {
            Column(Modifier.padding(horizontal = 20.dp)) {
                GradientBox(Modifier.fillMaxWidth().clip(RoundedCornerShape(24.dp)), listOf(Brand.Black, Color(0xFF2B3A37))) {
                    Column(Modifier.fillMaxWidth().padding(20.dp)) {
                        Text(content.home.bannerTitle, style = MaterialTheme.typography.headlineSmall, color = Color.White)
                        VSpace(6)
                        Text(content.home.bannerBody, color = Color.White.copy(alpha = 0.85f), style = MaterialTheme.typography.bodyMedium)
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
                    "“${content.homeQuote}”",
                    style = MaterialTheme.typography.titleLarge, textAlign = TextAlign.Center, modifier = Modifier.fillMaxWidth(),
                    color = Brand.Muted,
                )
            }
        }
        VSpace(24)
    }
}

@Composable
private fun MoodGrid(moods: List<com.rhythmandflow.app.data.MoodItem>, selected: String?, onSelect: (String) -> Unit) {
    // With very large text, three tiles across would break words in the middle, so use two.
    val columns = if (androidx.compose.ui.platform.LocalDensity.current.fontScale > 1.3f) 2 else 3
    Column(Modifier.padding(horizontal = 20.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
        moods.chunked(columns).forEach { row ->
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                row.forEach { m ->
                    val isSel = selected == m.label
                    val bg = try { Color(android.graphics.Color.parseColor(m.color)) } catch (_: Exception) { Brand.TealSoft }
                    val interaction = remember { MutableInteractionSource() }
                    val shape = RoundedCornerShape(20.dp)
                    Column(
                        Modifier.weight(1f).heightIn(min = 48.dp).pressScale(interaction)
                            .clip(shape).background(bg)
                            .let { if (isSel) it.background(Brand.TealDeep.copy(alpha = 0.14f)).border(2.dp, Brand.TealDeep, shape) else it }
                            .selectable(selected = isSel, interactionSource = interaction, indication = LocalIndication.current, role = Role.RadioButton) { onSelect(m.label) }
                            .padding(vertical = 14.dp, horizontal = 4.dp),
                        horizontalAlignment = Alignment.CenterHorizontally,
                    ) {
                        Text(m.emoji, fontSize = androidx.compose.ui.unit.TextUnit(26f, androidx.compose.ui.unit.TextUnitType.Sp))
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
private fun StatCard(value: String, label: String, modifier: Modifier = Modifier, onClick: (() -> Unit)? = null) {
    SoftCard(modifier, onClick = onClick) {
        Column(Modifier.fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally) {
            Text(value, style = MaterialTheme.typography.headlineSmall, color = Brand.TealDeep)
            Text(label, style = MaterialTheme.typography.labelSmall, color = Brand.Muted, textAlign = TextAlign.Center)
        }
    }
}

@Composable
private fun RhythmCards(cards: List<com.rhythmandflow.app.data.RhythmCardItem>, onNavigate: (String) -> Unit) {
    // The words are the client's (edited from the admin screens); where each card goes and its colour stay fixed.
    val targets = listOf(Triple("move", Brand.TangerineSoft, 0), Triple("explore", Brand.TealSoft, 1), Triple("move", Brand.Surface, 2))
    Row(Modifier.padding(horizontal = 20.dp).fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
        targets.forEach { (route, colour, i) ->
            val card = cards.getOrNull(i) ?: return@forEach
            RhythmCard(card.title, card.subtitle, colour, Modifier.weight(1f)) { onNavigate(route) }
        }
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
        RhythmCards(com.rhythmandflow.app.data.ContentCache.flow.collectAsState().value.rhythmCards, onNavigate)
        VSpace(20)
        Column(Modifier.padding(horizontal = 20.dp)) {
            GradientBox(Modifier.fillMaxWidth().clip(RoundedCornerShape(24.dp)), listOf(Brand.TealDeep, Brand.Teal)) {
                Column(Modifier.fillMaxWidth().padding(24.dp)) {
                    Text(com.rhythmandflow.app.data.ContentCache.flow.collectAsState().value.home.bannerTitle, style = MaterialTheme.typography.headlineMedium, color = Color.White)
                    VSpace(14)
                    PrimaryButtonOnDark("Explore Today") { onNavigate("explore") }
                }
            }
            VSpace(16)
            SoftCard(Modifier.fillMaxWidth()) {
                Text(
                    "“${com.rhythmandflow.app.data.ContentCache.flow.collectAsState().value.rhythmQuote}”",
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
