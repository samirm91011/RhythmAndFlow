package com.rhythmandflow.app.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import com.rhythmandflow.app.data.AppContent
import com.rhythmandflow.app.data.ContentCache
import com.rhythmandflow.app.data.ExploreSection
import com.rhythmandflow.app.data.HomeText
import com.rhythmandflow.app.data.ShopContent
import com.rhythmandflow.app.data.ShopProduct
import com.rhythmandflow.app.data.JournalPrompts
import com.rhythmandflow.app.data.MoodItem
import com.rhythmandflow.app.data.RhythmCardItem
import com.rhythmandflow.app.ui.components.LoadingBox
import com.rhythmandflow.app.ui.components.PrimaryButton
import com.rhythmandflow.app.ui.components.RfTextField
import com.rhythmandflow.app.ui.components.ScreenHeader
import com.rhythmandflow.app.ui.components.SecondaryButton
import com.rhythmandflow.app.ui.components.SoftCard
import com.rhythmandflow.app.ui.components.VSpace
import com.rhythmandflow.app.ui.components.vScroll
import com.rhythmandflow.app.ui.theme.Brand
import com.rhythmandflow.app.ui.viewmodel.AdminViewModel
import com.rhythmandflow.app.ui.viewmodel.appViewModel
import kotlinx.coroutines.launch

/**
 * Administrators: edit the wording customers see (check-in moods, daily affirmations, quotes, the Your Rhythm Today cards, the
 * journal prompts, Home wording, the Explore sections with their articles, and the Shop). A saved change shows in the apps straight away; "original wording" goes back to what the app came with.
 */
@Composable
fun AdminContentScreen(onBack: () -> Unit, notify: (String) -> Unit) {
    val vm = appViewModel(key = "admin") { AdminViewModel(it) }
    val scope = rememberCoroutineScope()
    var loaded by remember { mutableStateOf(false) }
    val moods = remember { mutableStateListOf<MoodItem>() }
    var affirmations by remember { mutableStateOf("") }
    var homeQuote by remember { mutableStateOf("") }
    var rhythmQuote by remember { mutableStateOf("") }
    val cards = remember { mutableStateListOf<RhythmCardItem>() }
    var reflectionTitle by remember { mutableStateOf("") }
    var reflectionHint by remember { mutableStateOf("") }
    var gratitudeTitle by remember { mutableStateOf("") }
    var gratitudeHint by remember { mutableStateOf("") }
    var classTitle by remember { mutableStateOf("") }
    var classBody by remember { mutableStateOf("") }
    var bannerTitle by remember { mutableStateOf("") }
    var bannerBody by remember { mutableStateOf("") }
    val sections = remember { mutableStateListOf<ExploreSection>() }
    var shopIntro by remember { mutableStateOf("") }
    val products = remember { mutableStateListOf<ShopProduct>() }
    var saving by remember { mutableStateOf(false) }
    var confirmReset by remember { mutableStateOf(false) }

    fun fill(c: AppContent) {
        moods.clear(); moods.addAll(c.moods); affirmations = c.affirmations.joinToString("\n")
        homeQuote = c.homeQuote; rhythmQuote = c.rhythmQuote; cards.clear(); cards.addAll(c.rhythmCards)
        reflectionTitle = c.journal.reflectionTitle; reflectionHint = c.journal.reflectionHint
        gratitudeTitle = c.journal.gratitudeTitle; gratitudeHint = c.journal.gratitudeHint
        classTitle = c.home.classTitle; classBody = c.home.classBody; bannerTitle = c.home.bannerTitle; bannerBody = c.home.bannerBody
        sections.clear(); sections.addAll(c.explore); shopIntro = c.shop.intro; products.clear(); products.addAll(c.shop.products)
    }
    LaunchedEffect(Unit) { vm.content()?.let { fill(it) }; loaded = true }

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("App content", "The wording customers see. Changes show straight away.", onBack = onBack)
        if (!loaded) LoadingBox()
        else Column(Modifier.vScroll().padding(20.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            SoftCard(Modifier.fillMaxWidth()) {
                Column(Modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Check-in moods", style = MaterialTheme.typography.titleLarge)
                    Text("The tiles on Home. Up to 12. Use an emoji and a soft colour such as #FFF1DC.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                    moods.forEachIndexed { i, m ->
                        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                            Box(Modifier.width(78.dp)) { RfTextField(m.emoji, { moods[i] = m.copy(emoji = it.take(8)) }, "Emoji") }
                            Box(Modifier.weight(1f)) { RfTextField(m.label, { moods[i] = m.copy(label = it.take(30)) }, "Mood") }
                            IconButton(onClick = { if (moods.size > 1) moods.removeAt(i) else notify("Keep at least one mood.") }) { Icon(Icons.Default.Delete, "Remove mood", tint = Brand.Error) }
                        }
                        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            val swatch = try { Color(android.graphics.Color.parseColor(m.color)) } catch (_: Exception) { Color.LightGray }
                            Box(Modifier.size(36.dp).clip(RoundedCornerShape(10.dp)).background(swatch))
                            Box(Modifier.weight(1f)) { RfTextField(m.color, { moods[i] = m.copy(color = it.take(7)) }, "Colour (like #FFF1DC)") }
                        }
                    }
                    SecondaryButton("Add a mood", enabled = moods.size < 12, onClick = { moods.add(MoodItem("New mood", "🙂", "#E3F0ED")) })
                }
            }
            SoftCard(Modifier.fillMaxWidth()) {
                Column(Modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Daily affirmations", style = MaterialTheme.typography.titleLarge)
                    Text("One per line. The Journal shows a different one each day.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                    RfTextField(affirmations, { affirmations = it }, "Affirmations", singleLine = false, minLines = 6)
                }
            }
            SoftCard(Modifier.fillMaxWidth()) {
                Column(Modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Quotes", style = MaterialTheme.typography.titleLarge)
                    RfTextField(homeQuote, { homeQuote = it.take(200) }, "Quote at the bottom of Home", singleLine = false, minLines = 2)
                    RfTextField(rhythmQuote, { rhythmQuote = it.take(200) }, "Quote on Your Rhythm Today", singleLine = false, minLines = 2)
                }
            }
            SoftCard(Modifier.fillMaxWidth()) {
                Column(Modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Your Rhythm Today cards", style = MaterialTheme.typography.titleLarge)
                    cards.forEachIndexed { i, c ->
                        RfTextField(c.title, { cards[i] = c.copy(title = it.take(40)) }, "Card ${i + 1} title")
                        RfTextField(c.subtitle, { cards[i] = c.copy(subtitle = it.take(40)) }, "Card ${i + 1} small line")
                    }
                }
            }
            SoftCard(Modifier.fillMaxWidth()) {
                Column(Modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Journal wording", style = MaterialTheme.typography.titleLarge)
                    RfTextField(reflectionTitle, { reflectionTitle = it.take(80) }, "Reflection title")
                    RfTextField(reflectionHint, { reflectionHint = it.take(80) }, "Reflection prompt")
                    RfTextField(gratitudeTitle, { gratitudeTitle = it.take(80) }, "Gratitude title")
                    RfTextField(gratitudeHint, { gratitudeHint = it.take(80) }, "Gratitude prompt")
                }
            }
            SoftCard(Modifier.fillMaxWidth()) {
                Column(Modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Home wording", style = MaterialTheme.typography.titleLarge)
                    RfTextField(classTitle, { classTitle = it.take(80) }, "Class card title")
                    RfTextField(classBody, { classBody = it.take(200) }, "Class card text", singleLine = false, minLines = 2)
                    RfTextField(bannerTitle, { bannerTitle = it.take(80) }, "Banner title")
                    RfTextField(bannerBody, { bannerBody = it.take(200) }, "Banner text", singleLine = false, minLines = 2)
                }
            }
            SoftCard(Modifier.fillMaxWidth()) {
                Column(Modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Explore sections and articles", style = MaterialTheme.typography.titleLarge)
                    Text("The six sections in Explore. The last one opens the Shop. Leave a blank line between paragraphs in an article.", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
                    sections.forEachIndexed { i, x ->
                        RfTextField(x.title, { sections[i] = x.copy(title = it.take(40)) }, "Section ${i + 1} title")
                        RfTextField(x.blurb, { sections[i] = x.copy(blurb = it.take(100)) }, "Short line")
                        if (i < 5) RfTextField(x.body, { sections[i] = x.copy(body = it.take(6000)) }, "Article text", singleLine = false, minLines = 5)
                    }
                }
            }
            SoftCard(Modifier.fillMaxWidth()) {
                Column(Modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Shop", style = MaterialTheme.typography.titleLarge)
                    RfTextField(shopIntro, { shopIntro = it.take(300) }, "Message at the top", singleLine = false, minLines = 2)
                    products.forEachIndexed { i, p ->
                        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                            Box(Modifier.weight(1f)) { RfTextField(p.name, { products[i] = p.copy(name = it.take(40)) }, "Product") }
                            Box(Modifier.width(96.dp)) { RfTextField(p.price, { products[i] = p.copy(price = it.take(20)) }, "Price") }
                            IconButton(onClick = { products.removeAt(i) }) { Icon(Icons.Default.Delete, "Remove product", tint = Brand.Error) }
                        }
                    }
                    SecondaryButton("Add a product", enabled = products.size < 12, onClick = { products.add(ShopProduct("New product", "R0")) })
                }
            }
            PrimaryButton("Save changes", loading = saving, onClick = {
                val content = AppContent(
                    moods.map { MoodItem(it.label.trim(), it.emoji.trim(), it.color.trim().uppercase()) },
                    affirmations.lines().map { it.trim() }.filter { it.isNotEmpty() }, homeQuote.trim(), rhythmQuote.trim(),
                    cards.map { RhythmCardItem(it.title.trim(), it.subtitle.trim()) },
                    JournalPrompts(reflectionTitle.trim(), reflectionHint.trim(), gratitudeTitle.trim(), gratitudeHint.trim()),
                    HomeText(classTitle.trim(), classBody.trim(), bannerTitle.trim(), bannerBody.trim()),
                    sections.mapIndexed { i, x -> ExploreSection(x.title.trim(), x.blurb.trim(), if (i < 5) x.body.trim() else "") },
                    ShopContent(shopIntro.trim(), products.map { ShopProduct(it.name.trim(), it.price.trim()) }),
                )
                saving = true
                scope.launch {
                    val error = vm.saveContent(content)
                    saving = false
                    if (error == null) ContentCache.flow.value = content
                    notify(error ?: "Saved. The app now shows your wording.")
                }
            })
            SecondaryButton("Go back to the original wording", onClick = { confirmReset = true })
            VSpace(16)
        }
    }

    if (confirmReset) AlertDialog(
        onDismissRequest = { confirmReset = false },
        title = { Text("Go back to the original wording?") },
        text = { Text("All the wording on this screen is replaced by the one the app came with.") },
        confirmButton = {
            TextButton(onClick = {
                confirmReset = false
                scope.launch {
                    val error = vm.resetContent()
                    if (error == null) { fill(AppContent.builtIn); ContentCache.flow.value = AppContent.builtIn }
                    notify(error ?: "Back to the original wording.")
                }
            }) { Text("Go back", color = Brand.Error) }
        },
        dismissButton = { TextButton(onClick = { confirmReset = false }) { Text("Keep mine") } },
    )
}
