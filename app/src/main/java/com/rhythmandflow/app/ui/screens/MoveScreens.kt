package com.rhythmandflow.app.ui.screens

import android.app.Activity
import android.content.Context
import android.content.ContextWrapper
import android.view.WindowManager
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.DirectionsRun
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.FavoriteBorder
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.MusicNote
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.SelfImprovement
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.Spa
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
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
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.media3.common.MediaItem
import androidx.media3.common.Player
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.ui.PlayerView
import com.rhythmandflow.app.data.Lesson
import com.rhythmandflow.app.ui.components.AccentButton
import com.rhythmandflow.app.ui.components.EmptyState
import com.rhythmandflow.app.ui.components.ErrorBox
import com.rhythmandflow.app.ui.components.GradientBox
import com.rhythmandflow.app.ui.components.InfoPill
import com.rhythmandflow.app.ui.components.LoadingBox
import com.rhythmandflow.app.ui.components.LockBadge
import com.rhythmandflow.app.ui.components.OnResume
import com.rhythmandflow.app.ui.components.PrimaryButton
import com.rhythmandflow.app.ui.components.RfTextField
import com.rhythmandflow.app.ui.components.ScreenHeader
import com.rhythmandflow.app.ui.components.SecondaryButton
import com.rhythmandflow.app.ui.components.SelectChip
import com.rhythmandflow.app.ui.components.SoftCard
import com.rhythmandflow.app.ui.components.vScroll
import com.rhythmandflow.app.ui.components.VSpace
import com.rhythmandflow.app.ui.theme.Brand
import com.rhythmandflow.app.ui.viewmodel.JournalViewModel
import com.rhythmandflow.app.ui.viewmodel.LessonViewModel
import com.rhythmandflow.app.ui.viewmodel.MoveViewModel
import com.rhythmandflow.app.ui.viewmodel.PlayerViewModel
import com.rhythmandflow.app.ui.viewmodel.appViewModel
import com.rhythmandflow.app.ui.viewmodel.container
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

private val categories = listOf("All", "Yoga", "Barre", "Dance", "Stretch", "Meditation")

fun categoryIcon(category: String): ImageVector = when (category) {
    "Yoga" -> Icons.Default.SelfImprovement
    "Dance" -> Icons.Default.MusicNote
    "Barre" -> Icons.Default.DirectionsRun
    "Meditation" -> Icons.Default.Spa
    else -> Icons.Default.SelfImprovement
}

private fun categoryColors(category: String): List<Color> = when (category) {
    "Dance" -> listOf(Brand.Tangerine, Color(0xFFEE8E6E))
    "Barre" -> listOf(Color(0xFF2B3A37), Brand.Teal)
    "Stretch" -> listOf(Brand.Teal, Color(0xFF7DB8AD))
    "Meditation" -> listOf(Color(0xFF3D4F6B), Color(0xFF7C94B8))
    else -> listOf(Brand.TealDeep, Brand.Teal)
}

@Composable
fun MoveScreen(onLesson: (Int) -> Unit) {
    val vm = appViewModel { MoveViewModel(it) }
    val lessons by vm.lessons.collectAsState()
    val category by vm.category.collectAsState()
    val query by vm.query.collectAsState()
    OnResume { vm.load() }

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Move", "Practices to help you feel good.")
        Column(Modifier.padding(horizontal = 20.dp)) {
            Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                categories.forEach { c -> SelectChip(c, category == c, onClick = { vm.setCategory(c) }) }
            }
            VSpace(10)
            RfTextField(query, vm::setQuery, "Search practices…", Icons.Default.Search)
        }
        VSpace(8)
        val data = lessons.data
        when {
            lessons.loading && data == null -> LoadingBox()
            lessons.error != null && data == null -> ErrorBox(lessons.error!!, onRetry = { vm.load() })
            data.isNullOrEmpty() -> EmptyState("Nothing here yet", "Try a different category or search.")
            else -> LazyColumn(contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 20.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                items(data, key = { it.id }) { l -> LessonRow(l, onClick = { onLesson(l.id) }) }
            }
        }
    }
}

@OptIn(androidx.compose.foundation.layout.ExperimentalLayoutApi::class)
@Composable
fun LessonRow(l: Lesson, onClick: () -> Unit) {
    SoftCard(Modifier.fillMaxWidth(), onClick = onClick, background = Color.White) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box {
                GradientBox(Modifier.size(84.dp).clip(RoundedCornerShape(16.dp)), categoryColors(l.category)) {
                    Icon(categoryIcon(l.category), null, tint = Color.White.copy(alpha = 0.9f), modifier = Modifier.size(34.dp))
                }
                if (l.locked) LockBadge(Modifier.align(Alignment.TopEnd).padding(4.dp))
            }
            Column(Modifier.weight(1f).padding(horizontal = 14.dp)) {
                Text(l.title, style = MaterialTheme.typography.titleMedium)
                Text(l.description, style = MaterialTheme.typography.bodySmall, color = Brand.Muted, maxLines = 2)
                VSpace(6)
                FlowRow(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    InfoPill(l.durationLabel)
                    InfoPill(l.category, color = Brand.Surface, textColor = Brand.Muted)
                    if (l.isPreview) InfoPill("Preview", color = Brand.TangerineSoft, textColor = Brand.Tangerine)
                }
                if (l.completionPercentage > 0) {
                    VSpace(8)
                    LinearProgressIndicator(
                        progress = { (l.completionPercentage / 100.0).toFloat() },
                        modifier = Modifier.fillMaxWidth().height(4.dp).clip(CircleShape), color = Brand.TealDeep, trackColor = Brand.LightGrey,
                    )
                }
            }
            Box(Modifier.size(40.dp).clip(CircleShape).background(if (l.locked) Brand.LightGrey else Brand.TealDeep), contentAlignment = Alignment.Center) {
                Icon(if (l.locked) Icons.Default.Lock else Icons.Default.PlayArrow, if (l.locked) "Locked" else "Play", tint = Color.White)
            }
        }
    }
}

@Composable
fun LessonDetailScreen(lessonId: Int, onBack: () -> Unit, onBegin: () -> Unit, onPlans: () -> Unit, onLesson: (Int) -> Unit) {
    val vm = appViewModel(key = "lesson$lessonId") { LessonViewModel(it, lessonId) }
    val state by vm.lesson.collectAsState()
    val related by vm.related.collectAsState()
    OnResume { vm.load() }
    val prefs = container().localPrefs
    var saved by remember { mutableStateOf(prefs.getSet("saved_lessons").contains(lessonId.toString())) }
    var tab by remember { mutableStateOf(0) }

    val lesson = state.data
    when {
        state.loading -> LoadingBox()
        lesson == null -> Column { ScreenHeader("Practice", onBack = onBack); ErrorBox(state.error ?: "Not found", onRetry = { vm.load() }) }
        else -> Column(Modifier.fillMaxSize()) {
            Box {
                GradientBox(Modifier.fillMaxWidth().height(230.dp), categoryColors(lesson.category)) {
                    Icon(categoryIcon(lesson.category), null, tint = Color.White.copy(alpha = 0.55f), modifier = Modifier.size(96.dp))
                }
                IconButton(onClick = onBack, modifier = Modifier.statusBarsPadding().padding(8.dp)) {
                    Icon(Icons.AutoMirrored.Filled.ArrowBack, "Back", tint = Color.White)
                }
                IconButton(
                    onClick = {
                        val set = prefs.getSet("saved_lessons").toMutableSet()
                        if (!set.add(lessonId.toString())) set.remove(lessonId.toString())
                        prefs.putSet("saved_lessons", set); saved = set.contains(lessonId.toString())
                    },
                    modifier = Modifier.align(Alignment.TopEnd).statusBarsPadding().padding(8.dp),
                ) { Icon(if (saved) Icons.Default.Favorite else Icons.Default.FavoriteBorder, if (saved) "Remove from saved" else "Save", tint = Color.White) }
            }
            Column(
                Modifier.weight(1f).clip(RoundedCornerShape(topStart = 28.dp, topEnd = 28.dp)).background(Color.White)
                    .vScroll().padding(20.dp),
            ) {
                Text(lesson.title, style = MaterialTheme.typography.headlineMedium)
                VSpace(8)
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    InfoPill(lesson.durationLabel); InfoPill(lesson.level); InfoPill(lesson.category)
                }
                VSpace(12)
                Text(lesson.description, color = Brand.Muted)
                VSpace(16)
                if (lesson.locked) {
                    SoftCard(Modifier.fillMaxWidth(), background = Brand.TangerineSoft) {
                        Column {
                            Text("Included with a subscription", style = MaterialTheme.typography.titleMedium)
                            Text("Subscribe to ${lesson.programmeName} and everything in your plan.", color = Brand.Muted, style = MaterialTheme.typography.bodyMedium)
                        }
                    }
                    VSpace(12)
                    AccentButton("View plans", onClick = onPlans)
                } else {
                    Text("What you'll need", style = MaterialTheme.typography.titleLarge)
                    VSpace(8)
                    Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        listOf("Yoga mat", "Water", "Open space").forEach { SoftCard(Modifier.weight(1f)) { Text(it, style = MaterialTheme.typography.labelMedium, textAlign = TextAlign.Center, modifier = Modifier.fillMaxWidth()) } }
                    }
                    VSpace(16)
                    PrimaryButton(if (lesson.watchTimeSeconds > 0 && lesson.completionPercentage < 95) "Resume Practice" else "Begin Practice", onClick = onBegin)
                }
                VSpace(16)
                TabRow(selectedTabIndex = tab, containerColor = Color.White, contentColor = Brand.TealDeep) {
                    listOf("About", "Guide", "Related").forEachIndexed { i, t -> Tab(selected = tab == i, onClick = { tab = i }, text = { Text(t) }) }
                }
                VSpace(12)
                when (tab) {
                    0 -> Text("Part of ${lesson.programmeName}. ${lesson.description}", color = Brand.Muted)
                    1 -> Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                        listOf("Find a quiet space and roll out your mat.", "Move at your own pace. There is no wrong way to do this.", "Stop if anything hurts and rest.", "Finish with a few deep breaths and notice how you feel.")
                            .forEach { Text("•  $it", color = Brand.Muted) }
                    }
                    else -> if (related.isEmpty()) Text("More practices are coming soon.", color = Brand.Muted) else Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                        related.forEach { r -> LessonRow(r, onClick = { onLesson(r.id) }) }
                    }
                }
                VSpace(24)
            }
        }
    }
}

private tailrec fun Context.findActivity(): Activity? = when (this) {
    is Activity -> this
    is ContextWrapper -> baseContext.findActivity()
    else -> null
}

@Composable
fun PlayerScreen(lessonId: Int, onBack: () -> Unit, onFinished: () -> Unit, onPlans: () -> Unit) {
    val vm = appViewModel(key = "player$lessonId") { PlayerViewModel(it, lessonId) }
    val state by vm.state.collectAsState()
    val context = LocalContext.current
    val lifecycle = LocalLifecycleOwner.current.lifecycle

    // Block screenshots and screen recording while a lesson is on screen (helps stop customers sharing paid videos).
    DisposableEffect(Unit) {
        val window = context.findActivity()?.window
        window?.addFlags(WindowManager.LayoutParams.FLAG_SECURE)
        onDispose { window?.clearFlags(WindowManager.LayoutParams.FLAG_SECURE) }
    }

    val player = remember { ExoPlayer.Builder(context).build() }

    LaunchedEffect(state.url) {
        val url = state.url ?: return@LaunchedEffect
        player.setMediaItem(MediaItem.fromUri(url))
        player.prepare()
        if (state.resumeMs > 0) player.seekTo(state.resumeMs)
        player.playWhenReady = true
    }

    // Report watch time every few seconds so progress survives the app being closed.
    LaunchedEffect(player) {
        while (true) {
            delay(5000)
            if (player.isPlaying) vm.report((player.currentPosition / 1000).toInt())
        }
    }

    DisposableEffect(player) {
        val listener = object : Player.Listener {
            override fun onPlaybackStateChanged(playbackState: Int) {
                if (playbackState == Player.STATE_ENDED) {
                    vm.report(((player.duration.coerceAtLeast(0)) / 1000).toInt() + 1, force = true)
                    onFinished()
                }
            }
        }
        val observer = LifecycleEventObserver { _, e -> if (e == Lifecycle.Event.ON_STOP) player.pause() }
        player.addListener(listener)
        lifecycle.addObserver(observer)
        onDispose {
            vm.report((player.currentPosition / 1000).toInt(), force = true)
            lifecycle.removeObserver(observer)
            player.removeListener(listener)
            player.release()
        }
    }

    Box(Modifier.fillMaxSize().background(Color.Black)) {
        when {
            state.loading -> LoadingBox()
            state.error != null -> Column(Modifier.fillMaxSize().padding(24.dp), verticalArrangement = Arrangement.Center, horizontalAlignment = Alignment.CenterHorizontally) {
                Text(state.error!!, color = Color.White, textAlign = TextAlign.Center)
                VSpace(16)
                if (state.locked) AccentButton("View plans", onClick = onPlans) else SecondaryButton("Try again", onClick = { vm.load() })
            }
            else -> AndroidView(
                factory = { ctx -> PlayerView(ctx).apply { this.player = player; useController = true } },
                modifier = Modifier.fillMaxSize(),
            )
        }
        IconButton(onClick = onBack, modifier = Modifier.statusBarsPadding().padding(8.dp)) {
            Icon(Icons.AutoMirrored.Filled.ArrowBack, "Back", tint = Color.White)
        }
    }
}

private val completeMoods = listOf("Calmer", "More energised", "Lighter", "Stronger", "More present", "Other")

@Composable
fun PracticeCompleteScreen(onHome: () -> Unit, onSaved: () -> Unit, notify: (String) -> Unit) {
    val vm = appViewModel { JournalViewModel(it) }
    var mood by remember { mutableStateOf<String?>(null) }
    var note by remember { mutableStateOf("") }
    var busy by remember { mutableStateOf(false) }
    val scope = rememberCoroutineScope()

    Column(Modifier.fillMaxSize().vScroll().padding(24.dp), horizontalAlignment = Alignment.CenterHorizontally) {
        VSpace(32)
        Box(Modifier.size(84.dp).clip(CircleShape).background(Brand.TealDeep), contentAlignment = Alignment.Center) {
            Icon(Icons.Default.Check, null, tint = Color.White, modifier = Modifier.size(44.dp))
        }
        VSpace(16)
        Text("Practice Complete!", style = MaterialTheme.typography.headlineLarge)
        Text("You showed up for yourself. How do you feel now?", color = Brand.Muted, textAlign = TextAlign.Center)
        VSpace(20)
        completeMoods.chunked(3).forEach { row ->
            Row(Modifier.fillMaxWidth().padding(vertical = 4.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                row.forEach { m -> SelectChip(m, mood == m, onClick = { mood = m }, modifier = Modifier.weight(1f)) }
            }
        }
        VSpace(16)
        RfTextField(note, { note = it }, "What did your body need today?", singleLine = false, minLines = 3)
        VSpace(16)
        PrimaryButton("Save to Journal", loading = busy, onClick = {
            busy = true
            scope.launch {
                val err = vm.add("REFLECTION", mood, note.ifBlank { null })
                busy = false
                if (err == null) { notify("Saved to your journal."); onSaved() } else notify(err)
            }
        })
        VSpace(8)
        androidx.compose.material3.TextButton(onClick = onHome) { Text("Back to Home", color = Brand.TealDeep) }
    }
}

