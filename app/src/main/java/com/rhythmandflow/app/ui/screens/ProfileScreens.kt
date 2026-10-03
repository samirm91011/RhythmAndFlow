package com.rhythmandflow.app.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.automirrored.filled.Logout
import androidx.compose.material.icons.filled.AdminPanelSettings
import androidx.compose.material.icons.filled.Alarm
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.material.icons.filled.CalendarMonth
import androidx.compose.material.icons.filled.CreditCard
import androidx.compose.material.icons.filled.DeleteForever
import androidx.compose.material.icons.filled.Download
import androidx.compose.material.icons.filled.Email
import androidx.compose.material.icons.filled.Help
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.Notifications
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Policy
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.unit.dp
import com.rhythmandflow.app.data.Lesson
import com.rhythmandflow.app.data.Outcome
import com.rhythmandflow.app.ui.components.*
import com.rhythmandflow.app.ui.theme.Brand
import com.rhythmandflow.app.ui.viewmodel.HomeViewModel
import com.rhythmandflow.app.ui.viewmodel.SessionState
import com.rhythmandflow.app.ui.viewmodel.SessionViewModel
import com.rhythmandflow.app.ui.viewmodel.appViewModel
import com.rhythmandflow.app.ui.viewmodel.container
import kotlinx.coroutines.launch

private val intentions = listOf("Move more", "Reduce stress", "Improve sleep", "Build confidence", "Create balance", "Nourish my body", "Connect with myself", "Make time for myself")
private val practices = listOf("Yoga", "Dance", "Barre", "Meditation", "Breathwork", "Stretch", "Walking", "Strength")

@Composable
fun YouScreen(session: SessionViewModel, onNavigate: (String) -> Unit, onLesson: (Int) -> Unit) {
    val user = (session.state.collectAsState().value as? SessionState.SignedIn)?.user
    val home = appViewModel(key = "you-home") { HomeViewModel(it) }
    val summary by home.state.collectAsState()
    val subs by session.subscriptions.collectAsState()
    val prefs = container().localPrefs
    var tab by remember { mutableIntStateOf(0) }
    var chosenIntentions by remember { mutableStateOf(prefs.getSet("intentions")) }
    var chosenPractices by remember { mutableStateOf(prefs.getSet("practices")) }
    val tabs = listOf("Overview", "Intentions", "Preferences", "Saved")
    val activeSub = subs.firstOrNull { it.grantsAccess }
    OnResume { home.load(); session.refreshSubscriptions() }

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("You", "Your journey, your way.") {
            IconButton(onClick = { onNavigate("settings") }) { Icon(Icons.Default.Settings, "Settings") }
        }
        ScrollableTabRow(selectedTabIndex = tab, containerColor = Color.White, contentColor = Brand.TealDeep, edgePadding = 12.dp) {
            tabs.forEachIndexed { i, t -> Tab(selected = tab == i, onClick = { tab = i }, text = { Text(t) }) }
        }
        Column(Modifier.vScroll().padding(20.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
            when (tab) {
                0 -> {
                    SoftCard(Modifier.fillMaxWidth(), background = Brand.TealSoft, onClick = { onNavigate("edit_profile") }) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Box(Modifier.size(52.dp).clip(CircleShape).background(Brand.TealDeep), contentAlignment = Alignment.Center) {
                                Text((user?.fullName?.firstOrNull() ?: 'R').uppercase(), color = Color.White, style = MaterialTheme.typography.titleLarge)
                            }
                            Column(Modifier.weight(1f).padding(start = 14.dp)) {
                                Text(user?.fullName ?: "", style = MaterialTheme.typography.titleLarge)
                                Text(user?.email ?: "", color = Brand.Muted, style = MaterialTheme.typography.bodyMedium)
                            }
                            Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, null, tint = Brand.Muted)
                        }
                    }
                    SoftCard(Modifier.fillMaxWidth()) {
                        Column(Modifier.fillMaxWidth()) {
                            Text("Your Journey", style = MaterialTheme.typography.titleLarge)
                            val n = summary.summary?.sessionsThisMonth ?: 0
                            Text("You've shown up for yourself $n ${if (n == 1) "time" else "times"} this month.", color = Brand.Muted)
                        }
                    }
                    NavRow(Icons.Default.CreditCard, if (activeSub != null) "${activeSub.planName} plan" else "Choose a plan",
                        if (activeSub != null) "Manage your subscription" else "Unlock every programme") {
                        onNavigate(if (activeSub != null) "subscription" else "plans")
                    }
                    NavRow(Icons.Default.CalendarMonth, "My bookings", "Upcoming classes") { onNavigate("bookings") }
                    if (user?.isAdmin == true) NavRow(Icons.Default.AdminPanelSettings, "Admin tools", "Manage lessons, classes and plans") { onNavigate("admin") }
                }
                1 -> {
                    Text("How do you want to feel?", style = MaterialTheme.typography.titleLarge)
                    Text("Select your wellness intentions:", color = Brand.Muted)
                    intentions.chunked(2).forEach { row ->
                        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            row.forEach { i ->
                                Row(Modifier.weight(1f), verticalAlignment = Alignment.CenterVertically) {
                                    Checkbox(checked = i in chosenIntentions, onCheckedChange = {
                                        chosenIntentions = if (it) chosenIntentions + i else chosenIntentions - i
                                        prefs.putSet("intentions", chosenIntentions)
                                    }, colors = CheckboxDefaults.colors(checkedColor = Brand.TealDeep))
                                    Text(i, style = MaterialTheme.typography.bodyMedium)
                                }
                            }
                        }
                    }
                }
                2 -> {
                    Text("Favourite practices", style = MaterialTheme.typography.titleLarge)
                    practices.chunked(3).forEach { row ->
                        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            row.forEach { p ->
                                SelectChip(p, p in chosenPractices, onClick = {
                                    chosenPractices = if (p in chosenPractices) chosenPractices - p else chosenPractices + p
                                    prefs.putSet("practices", chosenPractices)
                                })
                            }
                        }
                    }
                }
                else -> SavedTab(prefs.getSet("saved_lessons"), onLesson)
            }
        }
    }
}

@Composable
private fun SavedTab(savedIds: Set<String>, onLesson: (Int) -> Unit) {
    val repo = container().repository
    var lessons by remember { mutableStateOf<List<Lesson>?>(null) }
    LaunchedEffect(Unit) { lessons = (repo.lessons() as? Outcome.Ok)?.value?.filter { it.id.toString() in savedIds } ?: emptyList() }
    when {
        lessons == null -> Box(Modifier.fillMaxWidth().height(120.dp)) { LoadingBox() }
        lessons!!.isEmpty() -> EmptyState("Nothing saved yet", "Tap the heart on a practice to save it here.")
        else -> Column(verticalArrangement = Arrangement.spacedBy(10.dp)) { lessons!!.forEach { LessonRow(it, onClick = { onLesson(it.id) }) } }
    }
}

@Composable
private fun NavRow(icon: ImageVector, title: String, subtitle: String, onClick: () -> Unit) {
    SoftCard(Modifier.fillMaxWidth(), onClick = onClick) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(icon, null, tint = Brand.TealDeep)
            Column(Modifier.weight(1f).padding(horizontal = 14.dp)) {
                Text(title, style = MaterialTheme.typography.titleSmall)
                Text(subtitle, style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
            }
            Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, null, tint = Brand.Muted)
        }
    }
}

@Composable
fun EditProfileScreen(session: SessionViewModel, onBack: () -> Unit, notify: (String) -> Unit) {
    val user = (session.state.collectAsState().value as? SessionState.SignedIn)?.user
    var name by remember { mutableStateOf(user?.fullName ?: "") }
    var email by remember { mutableStateOf(user?.email ?: "") }
    var about by remember { mutableStateOf(user?.about ?: "") }
    var busy by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Edit Profile", onBack = onBack)
        Column(Modifier.vScroll().padding(20.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            RfTextField(name, { name = it }, "Full name", Icons.Default.Person)
            RfTextField(email, { email = it }, "Email", Icons.Default.Email, keyboardType = androidx.compose.ui.text.input.KeyboardType.Email)
            RfTextField(about, { if (it.length <= 300) about = it }, "About me", singleLine = false, minLines = 3)
            error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            PrimaryButton("Save Changes", loading = busy, enabled = name.trim().length >= 2 && email.contains("@"), onClick = {
                busy = true; error = null
                scope.launch {
                    val err = session.updateProfile(name, email, about)
                    busy = false
                    if (err == null) { notify("Profile updated."); onBack() } else error = err
                }
            })
        }
    }
}

@Composable
fun SettingsScreen(session: SessionViewModel, onBack: () -> Unit, onNavigate: (String) -> Unit, onSignOut: () -> Unit) {
    var dialog by remember { mutableStateOf<Pair<String, String>?>(null) }
    var deleting by remember { mutableStateOf(false) }
    var exporting by remember { mutableStateOf(false) }
    var exportError by remember { mutableStateOf<String?>(null) }
    val context = androidx.compose.ui.platform.LocalContext.current
    val scope = rememberCoroutineScope()
    fun openPage(url: String) {
        try { context.startActivity(android.content.Intent(android.content.Intent.ACTION_VIEW, android.net.Uri.parse(url))) }
        catch (_: Exception) { dialog = "Can't open the page" to "No web browser was found on this phone." }
    }
    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Settings", onBack = onBack)
        Column(Modifier.vScroll().padding(20.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            NavRow(Icons.Default.Person, "Account", "Personal information") { onNavigate("edit_profile") }
            NavRow(Icons.Default.Lock, "Password", "Change your password") { onNavigate("change_password") }
            NavRow(Icons.Default.CreditCard, "Subscription", "Plan and billing") { onNavigate("subscription") }
            NavRow(Icons.Default.Notifications, "Notifications", "Your updates and alerts") { onNavigate("notifications") }
            ReminderSwitchRow()
            NavRow(Icons.Default.Lock, "Privacy Policy", "Read it on rhythmandflow.co.za/privacy-policy") { openPage(LegalLinks.PRIVACY) }
            NavRow(Icons.Default.Policy, "Terms of Use", "Read it on rhythmandflow.co.za/terms-of-use") { openPage(LegalLinks.TERMS) }
            NavRow(Icons.Default.Help, "Help & Support", "Get answers and contact us") {
                dialog = "Help & Support" to "Visit rhythmandflow.co.za to get in touch with the Rhythm & Flow team."
            }
            NavRow(Icons.Default.Download, if (exporting) "Preparing your data..." else "Download my data", "Get a copy of everything we hold about you") {
                if (!exporting) {
                    exporting = true
                    scope.launch {
                        val (json, err) = session.exportData()
                        exporting = false
                        if (json != null) {
                            val send = android.content.Intent(android.content.Intent.ACTION_SEND).apply {
                                type = "text/plain"
                                putExtra(android.content.Intent.EXTRA_SUBJECT, "My Rhythm & Flow data")
                                putExtra(android.content.Intent.EXTRA_TEXT, json)
                            }
                            context.startActivity(android.content.Intent.createChooser(send, "Save or share my data"))
                        } else exportError = err ?: "Something went wrong. Please try again."
                    }
                }
            }
            NavRow(Icons.Default.DeleteForever, "Delete my account", "Erase your account and personal data") { deleting = true }
            VSpace(8)
            OutlinedButton(
                onClick = onSignOut, modifier = Modifier.fillMaxWidth().height(54.dp), shape = RoundedCornerShape(28.dp),
                border = androidx.compose.foundation.BorderStroke(1.dp, Brand.TangerineDeep),
                colors = ButtonDefaults.outlinedButtonColors(contentColor = Brand.TangerineDeep),
            ) {
                Icon(Icons.AutoMirrored.Filled.Logout, null); HSpace(8); Text("Log Out")
            }
        }
    }
    dialog?.let { (t, body) ->
        AlertDialog(onDismissRequest = { dialog = null }, title = { Text(t) }, text = { Text(body) },
            confirmButton = { TextButton(onClick = { dialog = null }) { Text("OK") } })
    }
    exportError?.let { msg ->
        AlertDialog(onDismissRequest = { exportError = null }, title = { Text("Couldn't get your data") }, text = { Text(msg) },
            confirmButton = { TextButton(onClick = { exportError = null }) { Text("OK") } })
    }
    if (deleting) DeleteAccountDialog(session, onDismiss = { deleting = false })
}

/** Asks for the password, then deletes the account. Plain about what is and is not erased. */
@Composable
private fun DeleteAccountDialog(session: SessionViewModel, onDismiss: () -> Unit) {
    var password by remember { mutableStateOf("") }
    var busy by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()
    AlertDialog(
        onDismissRequest = { if (!busy) onDismiss() },
        title = { Text("Delete your account?") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Text("This permanently erases your profile, journal, bookings, progress and notifications. Any active subscription is cancelled first. Payment records are kept without your name, because we must keep financial records. This can't be undone.")
                RfTextField(password, { password = it; error = null }, "Your password", isPassword = true, error = error)
            }
        },
        confirmButton = {
            TextButton(enabled = password.isNotBlank() && !busy, onClick = {
                busy = true
                scope.launch {
                    val err = session.deleteAccount(password)
                    busy = false
                    if (err != null) error = err   // on success the person is signed out and this screen goes away
                }
            }) { Text(if (busy) "Deleting..." else "Delete forever", color = Brand.Error) }
        },
        dismissButton = { TextButton(enabled = !busy, onClick = onDismiss) { Text("Keep my account") } },
    )
}

/** Turns the "1 hour before class" reminder on or off for this phone. */
@Composable
private fun ReminderSwitchRow() {
    val context = androidx.compose.ui.platform.LocalContext.current
    val prefs = container().localPrefs
    var on by remember { mutableStateOf(com.rhythmandflow.app.notifications.ReminderScheduler.enabled(prefs)) }
    SoftCard(Modifier.fillMaxWidth()) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(Icons.Default.Alarm, null, tint = Brand.TealDeep)
            Column(Modifier.weight(1f).padding(horizontal = 14.dp)) {
                Text("Class reminders", style = MaterialTheme.typography.titleSmall)
                Text("A notification 1 hour before each class you book", style = MaterialTheme.typography.bodySmall, color = Brand.Muted)
            }
            Switch(
                checked = on,
                onCheckedChange = { on = it; com.rhythmandflow.app.notifications.ReminderScheduler.setEnabled(context.applicationContext, prefs, it) },
                modifier = Modifier.semantics { contentDescription = "Class reminders" },
                colors = SwitchDefaults.colors(checkedTrackColor = Brand.TealDeep),
            )
        }
    }
}