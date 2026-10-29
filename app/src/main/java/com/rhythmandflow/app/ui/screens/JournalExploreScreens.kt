package com.rhythmandflow.app.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.FavoriteBorder
import androidx.compose.material.icons.filled.Restaurant
import androidx.compose.material.icons.filled.Explore
import androidx.compose.material.icons.filled.Lightbulb
import androidx.compose.material.icons.filled.Psychology
import androidx.compose.material.icons.filled.ShoppingBag
import androidx.compose.material.icons.filled.ShoppingCart
import androidx.compose.material.icons.filled.Spa
import androidx.compose.material.icons.filled.SelfImprovement
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.rhythmandflow.app.data.JournalEntry
import com.rhythmandflow.app.ui.components.*
import com.rhythmandflow.app.ui.theme.Brand
import com.rhythmandflow.app.ui.viewmodel.JournalViewModel
import com.rhythmandflow.app.ui.viewmodel.MoveViewModel
import com.rhythmandflow.app.ui.viewmodel.appViewModel
import com.rhythmandflow.app.ui.viewmodel.container
import java.time.LocalDate
import kotlinx.coroutines.launch

/** The affirmations and journal wording are the client's, edited from the admin screens (see ContentCache). */
@Composable
fun JournalScreen(onLesson: (Int) -> Unit, notify: (String) -> Unit) {
    val vm = appViewModel(key = "journal") { JournalViewModel(it) }
    val entries by vm.entries.collectAsState()
    val prefs = container().localPrefs
    var tab by remember { mutableIntStateOf(0) }
    val tabs = listOf("Check-in", "Journal", "Meditation", "Affirmations")
    val scope = rememberCoroutineScope()

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Journal", "Reconnect with yourself.")
        ScrollableTabRow(selectedTabIndex = tab, containerColor = Color.White, contentColor = Brand.TealDeep, edgePadding = 12.dp) {
            tabs.forEachIndexed { i, t -> Tab(selected = tab == i, onClick = { tab = i }, text = { Text(t) }) }
        }
        when (tab) {
            0 -> CheckInTab(onSave = { kind, text ->
                scope.launch { notify(vm.add(kind, null, text) ?: "Saved. Well done for checking in.") }
            })
            1 -> JournalListTab(entries.data, entries.loading, entries.error, onDelete = { id -> scope.launch { vm.delete(id) } }, onRetry = vm::load)
            2 -> MeditationTab(onLesson)
            else -> AffirmationsTab(prefs.getSet("fav_affirmations"), onToggle = { a ->
                val set = prefs.getSet("fav_affirmations").toMutableSet()
                if (!set.add(a)) set.remove(a)
                prefs.putSet("fav_affirmations", set)
            })
        }
    }
}

@Composable
private fun CheckInTab(onSave: (String, String) -> Unit) {
    var reflection by remember { mutableStateOf("") }
    var gratitude by remember { mutableStateOf("") }
    val content by com.rhythmandflow.app.data.ContentCache.flow.collectAsState()
    val affirmations = content.affirmations
    val today = affirmations[LocalDate.now().dayOfYear % affirmations.size]

    Column(Modifier.vScroll().padding(20.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
        SoftCard(Modifier.fillMaxWidth(), background = Brand.TangerineSoft) {
            Column(Modifier.fillMaxWidth()) {
                Text(content.journal.reflectionTitle, style = MaterialTheme.typography.titleLarge)
                Text(content.journal.reflectionHint, color = Brand.Muted, style = MaterialTheme.typography.bodyMedium)
                VSpace(10)
                RfTextField(reflection, { reflection = it }, "Write freely…", singleLine = false, minLines = 3)
                VSpace(10)
                PrimaryButton("Save Entry", enabled = reflection.isNotBlank(), onClick = { onSave("REFLECTION", reflection.trim()); reflection = "" })
            }
        }
        SoftCard(Modifier.fillMaxWidth()) {
            Column(Modifier.fillMaxWidth()) {
                Text("Daily Affirmation", style = MaterialTheme.typography.titleMedium, color = Brand.Muted)
                VSpace(6)
                Text("“$today”", style = MaterialTheme.typography.titleLarge)
            }
        }
        SoftCard(Modifier.fillMaxWidth()) {
            Column(Modifier.fillMaxWidth()) {
                Text(content.journal.gratitudeTitle, style = MaterialTheme.typography.titleLarge)
                Text(content.journal.gratitudeHint, color = Brand.Muted, style = MaterialTheme.typography.bodyMedium)
                VSpace(10)
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Box(Modifier.weight(1f)) { RfTextField(gratitude, { gratitude = it }, "I'm grateful for…") }
                    HSpace(8)
                    FilledIconButton(
                        onClick = { onSave("GRATITUDE", gratitude.trim()); gratitude = "" },
                        enabled = gratitude.isNotBlank(),
                        colors = IconButtonDefaults.filledIconButtonColors(containerColor = Brand.TealDeep),
                    ) { Icon(Icons.Default.Add, "Add gratitude") }
                }
            }
        }
    }
}

@Composable
private fun JournalListTab(entries: List<JournalEntry>?, loading: Boolean, error: String?, onDelete: (Int) -> Unit, onRetry: () -> Unit) {
    when {
        loading && entries == null -> LoadingBox()
        error != null && entries == null -> ErrorBox(error, onRetry)
        entries.isNullOrEmpty() -> EmptyState("Your journal is empty", "Check-ins, reflections and gratitude notes will appear here.")
        else -> LazyColumn(contentPadding = PaddingValues(20.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            items(entries, key = { it.id }) { e ->
                SoftCard(Modifier.fillMaxWidth()) {
                    Row(verticalAlignment = Alignment.Top) {
                        Column(Modifier.weight(1f)) {
                            Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                                InfoPill(e.kind.lowercase().replaceFirstChar { it.uppercase() })
                                if (e.mood.isNotBlank()) InfoPill(e.mood, color = Brand.TangerineSoft, textColor = Brand.TangerineDeep)
                            }
                            if (e.text.isNotBlank()) { VSpace(6); Text(e.text) }
                            VSpace(4)
                            Text(formatDayTime(e.createdAt), style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                        }
                        IconButton(onClick = { onDelete(e.id) }) { Icon(Icons.Default.Delete, "Delete entry", tint = Brand.Muted) }
                    }
                }
            }
        }
    }
}

@Composable
private fun MeditationTab(onLesson: (Int) -> Unit) {
    val vm = appViewModel(key = "meditation") { MoveViewModel(it).also { m -> m.setCategory("Meditation") } }
    val lessons by vm.lessons.collectAsState()
    val data = lessons.data
    when {
        lessons.loading && data == null -> LoadingBox()
        data.isNullOrEmpty() -> EmptyState("No meditations yet", "Guided meditations will appear here.")
        else -> LazyColumn(contentPadding = PaddingValues(20.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            items(data, key = { it.id }) { l -> LessonRow(l, onClick = { onLesson(l.id) }) }
        }
    }
}

@Composable
private fun AffirmationsTab(favourites: Set<String>, onToggle: (String) -> Unit) {
    var favs by remember { mutableStateOf(favourites) }
    LazyColumn(contentPadding = PaddingValues(20.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
        items(com.rhythmandflow.app.data.ContentCache.flow.value.affirmations) { a ->
            SoftCard(Modifier.fillMaxWidth()) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text("“$a”", style = MaterialTheme.typography.titleMedium, modifier = Modifier.weight(1f))
                    IconButton(onClick = { onToggle(a); favs = if (a in favs) favs - a else favs + a }) {
                        Icon(if (a in favs) Icons.Default.Favorite else Icons.Default.FavoriteBorder, "Favourite", tint = Brand.Tangerine)
                    }
                }
            }
        }
    }
}

// ============================================================ Explore / articles / shop

/** Only the look (icon and colours) is fixed here; the words are the client's, edited from the admin screens. */
private data class ExploreLook(val icon: ImageVector, val colors: List<Color>)

private val exploreLooks = listOf(
    ExploreLook(Icons.Default.Spa, listOf(Brand.TealDeep, Brand.Teal)),
    ExploreLook(Icons.Default.Restaurant, listOf(Brand.Tangerine, Color(0xFFEE8E6E))),
    ExploreLook(Icons.Default.Lightbulb, listOf(Color(0xFF3D4F6B), Color(0xFF7C94B8))),
    ExploreLook(Icons.Default.Explore, listOf(Brand.Teal, Color(0xFF7DB8AD))),
    ExploreLook(Icons.Default.Psychology, listOf(Color(0xFF2B3A37), Brand.Teal)),
    ExploreLook(Icons.Default.ShoppingBag, listOf(Brand.Black, Color(0xFF444444))),
)

@Composable
fun ExploreScreen(onBack: () -> Unit, onArticle: (String) -> Unit, onShop: () -> Unit) {
    val sections = com.rhythmandflow.app.data.ContentCache.flow.collectAsState().value.explore
    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Explore", "A holistic wellness library.", onBack = onBack)
        LazyColumn(contentPadding = PaddingValues(horizontal = 20.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            items(sections.size) { i ->
                val item = sections[i]; val look = exploreLooks[i]
                SoftCard(Modifier.fillMaxWidth(), onClick = { if (i == 5) onShop() else onArticle(i.toString()) }, background = Color.White) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        GradientBox(Modifier.size(64.dp).clip(RoundedCornerShape(16.dp)), look.colors) {
                            Icon(look.icon, null, tint = Color.White, modifier = Modifier.size(30.dp))
                        }
                        Column(Modifier.weight(1f).padding(horizontal = 14.dp)) {
                            Text(item.title, style = MaterialTheme.typography.titleMedium)
                            Text(item.blurb, style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                        }
                        Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, null, tint = Brand.Muted)
                    }
                }
            }
        }
    }
}

/** The article text is the client's, edited from the admin screens; the section is picked by its position in Explore. */
@Composable
fun ArticleScreen(title: String, onBack: () -> Unit, notify: (String) -> Unit) {
    val n = (title.toIntOrNull() ?: 0).coerceIn(0, 4)
    val item = com.rhythmandflow.app.data.ContentCache.flow.collectAsState().value.explore[n]; val look = exploreLooks[n]
    Column(Modifier.fillMaxSize()) {
        Box {
            GradientBox(Modifier.fillMaxWidth().height(200.dp), look.colors) {
                Icon(look.icon, null, tint = Color.White.copy(alpha = 0.6f), modifier = Modifier.size(80.dp))
            }
            IconButton(onClick = onBack, modifier = Modifier.statusBarsPadding().padding(8.dp)) {
                Icon(Icons.AutoMirrored.Filled.ArrowBack, "Back", tint = Color.White)
            }
        }
        Column(Modifier.vScroll().padding(20.dp)) {
            Text(item.title, style = MaterialTheme.typography.headlineMedium)
            Text("MINDSET  •  5 MIN READ", style = MaterialTheme.typography.labelMedium, color = Brand.Muted)
            VSpace(12)
            item.body.split(Regex("\n\\s*\n")).filter { it.isNotBlank() }.forEach { Text(it.trim(), color = Brand.Muted); VSpace(12) }
            VSpace(4)
            Text("Full articles from Rhythm & Flow are coming soon.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
        }
    }
}

@Composable
fun ShopScreen(onBack: () -> Unit, notify: (String) -> Unit) {
    val content = com.rhythmandflow.app.data.ContentCache.flow.collectAsState().value
    val products = content.shop.products
    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Shop", content.explore[5].blurb, onBack = onBack) {
            IconButton(onClick = { notify("The shop is coming soon.") }) { Icon(Icons.Default.ShoppingCart, "Cart") }
        }
        Column(Modifier.padding(horizontal = 20.dp)) {
            SoftCard(Modifier.fillMaxWidth(), background = Brand.TangerineSoft) {
                Text(content.shop.intro, style = MaterialTheme.typography.bodyMedium)
            }
        }
        VSpace(12)
        androidx.compose.foundation.lazy.grid.LazyVerticalGrid(
            columns = androidx.compose.foundation.lazy.grid.GridCells.Fixed(2),
            contentPadding = PaddingValues(20.dp), horizontalArrangement = Arrangement.spacedBy(12.dp), verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            items(products.size) { i ->
                val p = products[i]
                SoftCard(Modifier.fillMaxWidth()) {
                    Column(Modifier.fillMaxWidth()) {
                        GradientBox(Modifier.fillMaxWidth().height(90.dp).clip(RoundedCornerShape(12.dp)), listOf(Brand.LightGrey, Color(0xFFEDEDED))) {
                            Icon(Icons.Default.ShoppingBag, null, tint = Brand.Muted)
                        }
                        VSpace(8)
                        Text(p.name, style = MaterialTheme.typography.titleSmall)
                        Text(p.price, color = Brand.TealDeep, style = MaterialTheme.typography.titleSmall)
                    }
                }
            }
        }
    }
}
