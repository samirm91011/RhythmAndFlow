package com.rhythmandflow.app.ui.nav

import android.Manifest
import android.os.Build
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.runtime.LaunchedEffect
import com.rhythmandflow.app.ui.viewmodel.container
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CalendarMonth
import androidx.compose.material.icons.filled.FitnessCenter
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.MenuBook
import androidx.compose.material.icons.filled.Person
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.NavigationBarItemDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.material3.NavigationRail
import androidx.compose.material3.NavigationRailItem
import androidx.compose.material3.NavigationRailItemDefaults
import androidx.compose.ui.draw.shadow
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInHorizontally
import androidx.compose.animation.slideOutHorizontally
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.dp
import androidx.navigation.NavHostController
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import com.rhythmandflow.app.R
import com.rhythmandflow.app.ui.screens.*
import com.rhythmandflow.app.ui.theme.Brand
import com.rhythmandflow.app.ui.viewmodel.SessionState
import com.rhythmandflow.app.ui.viewmodel.SessionViewModel
import kotlinx.coroutines.launch

private data class Tab(val route: String, val label: String, val icon: ImageVector)

private val tabs = listOf(
    Tab("home", "Home", Icons.Default.Home),
    Tab("move", "Move", Icons.Default.FitnessCenter),
    Tab("classes", "Classes", Icons.Default.CalendarMonth),
    Tab("journal", "Journal", Icons.Default.MenuBook),
    Tab("you", "You", Icons.Default.Person),
)

/** Screens a notification (or push message) is allowed to open. Anything else is ignored. */
private val knownRoutes = setOf("home", "move", "classes", "journal", "you", "bookings", "plans", "subscription", "notifications", "admin", "admin/errors")

@Composable
fun AppRoot(session: SessionViewModel, pendingRoute: String? = null, onRouteHandled: () -> Unit = {}) {
    val state by session.state.collectAsState()
    val snackbar = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    val notify: (String) -> Unit = { msg -> scope.launch { snackbar.currentSnackbarData?.dismiss(); snackbar.showSnackbar(msg) } }

    Scaffold(snackbarHost = { SnackbarHost(snackbar) }, containerColor = Color.White) { _ ->
        when (state) {
            SessionState.Loading -> Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Image(painterResource(R.drawable.rf_logo_black), "Rhythm & Flow", Modifier.size(140.dp))
            }
            SessionState.SignedOut -> Box(Modifier.fillMaxSize().background(Brand.Canvas), contentAlignment = Alignment.TopCenter) {
                Box(Modifier.widthIn(max = 560.dp).fillMaxHeight().background(Color.White)) { AuthGraph(session, notify) }
            }
            is SessionState.SignedIn -> MainGraph(session, notify, pendingRoute, onRouteHandled)
        }
    }
}

@Composable
private fun AuthGraph(session: SessionViewModel, notify: (String) -> Unit) {
    val nav = rememberNavController()
    NavHost(nav, startDestination = "welcome") {
        composable("welcome") { WelcomeScreen(onStart = { nav.navigate("signin") }) }
        composable("signin") { SignInScreen(session, onSignUp = { nav.navigate("signup") }, onForgot = { nav.navigate("forgot") }, notify = notify) }
        composable("forgot") {
            ForgotPasswordScreen(onBack = { nav.popBackStack() }, onDone = { msg -> nav.popBackStack("signin", false); notify(msg) })
        }
        composable("signup") { SignUpScreen(session, onBack = { nav.popBackStack() }, onLogin = { nav.popBackStack("signin", false) }) }
    }
}

@Composable
private fun MainGraph(session: SessionViewModel, notify: (String) -> Unit, pendingRoute: String?, onRouteHandled: () -> Unit) {
    val nav = rememberNavController()
    val backStack by nav.currentBackStackEntryAsState()
    val route = backStack?.destination?.route
    val showBar = route in tabs.map { it.route }
    // Tablets and other wide windows get a side rail instead of the bottom bar, and content stays in a readable column.
    val wide = androidx.compose.ui.platform.LocalConfiguration.current.screenWidthDp >= 840
    val prefs = container().localPrefs

    // Ask once (Android 13+) for permission to show notifications.
    val permissionLauncher = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { }
    LaunchedEffect(Unit) {
        if (Build.VERSION.SDK_INT >= 33 && !prefs.getBool("asked_notification_permission")) {
            prefs.putBool("asked_notification_permission", true)
            permissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
        }
    }

    // A tapped notification asks us to open a screen; only known screens are opened.
    LaunchedEffect(pendingRoute, backStack != null) {
        val r = pendingRoute
        if (r != null && backStack != null) {
            if (r in knownRoutes) { if (r in tabs.map { it.route }) nav.goTab(r) else nav.navigate(r) }
            onRouteHandled()
        }
    }

    Scaffold(
        containerColor = Color.White,
        bottomBar = {
            if (showBar && !wide) {
                NavigationBar(
                    containerColor = Color.White, tonalElevation = 0.dp,
                    modifier = Modifier.shadow(12.dp, ambientColor = Brand.Shadow.copy(alpha = 0.12f), spotColor = Brand.Shadow.copy(alpha = 0.12f)),
                ) {
                    tabs.forEach { t ->
                        NavigationBarItem(
                            selected = route == t.route,
                            onClick = { nav.goTab(t.route) },
                            icon = { Icon(t.icon, t.label) },
                            label = { Text(t.label) },
                            colors = NavigationBarItemDefaults.colors(
                                selectedIconColor = Brand.TealDeep, selectedTextColor = Brand.TealDeep,
                                indicatorColor = Brand.TealSoft, unselectedIconColor = Brand.Muted, unselectedTextColor = Brand.Muted,
                            ),
                        )
                    }
                }
            }
        },
    ) { padding ->
      Row(Modifier.padding(padding).fillMaxSize()) {
        if (showBar && wide) {
            NavigationRail(containerColor = Color.White) {
                Spacer(Modifier.weight(1f))
                tabs.forEach { t ->
                    NavigationRailItem(
                        selected = route == t.route,
                        onClick = { nav.goTab(t.route) },
                        icon = { Icon(t.icon, t.label) },
                        label = { Text(t.label) },
                        colors = NavigationRailItemDefaults.colors(
                            selectedIconColor = Brand.TealDeep, selectedTextColor = Brand.TealDeep,
                            indicatorColor = Brand.TealSoft, unselectedIconColor = Brand.Muted, unselectedTextColor = Brand.Muted,
                        ),
                    )
                }
                Spacer(Modifier.weight(1f))
            }
        }
        Box(Modifier.weight(1f).fillMaxHeight().background(Brand.Canvas), contentAlignment = Alignment.TopCenter) {
          Box(Modifier.widthIn(max = 720.dp).fillMaxHeight().background(Color.White)) {
        NavHost(
            nav, startDestination = "home", modifier = Modifier.fillMaxSize(),
            enterTransition = { fadeIn(tween(220)) + slideInHorizontally(tween(260)) { it / 14 } },
            exitTransition = { fadeOut(tween(160)) },
            popEnterTransition = { fadeIn(tween(220)) },
            popExitTransition = { fadeOut(tween(160)) + slideOutHorizontally(tween(240)) { it / 14 } },
        ) {
            val go: (String) -> Unit = { r -> if (r in tabs.map { it.route }) nav.goTab(r) else nav.navigate(r) }
            val back: () -> Unit = { nav.popBackStack() }

            // ---- Tabs ----
            composable("home") { HomeScreen(session, onNavigate = go, notify = notify) }
            composable("move") { MoveScreen(onLesson = { nav.navigate("lesson/$it") }) }
            composable("classes") { ClassesScreen(onBookings = { nav.navigate("bookings") }, notify = notify) }
            composable("journal") { JournalScreen(onLesson = { nav.navigate("lesson/$it") }, notify = notify) }
            composable("you") { YouScreen(session, onNavigate = go, onLesson = { nav.navigate("lesson/$it") }) }

            composable("notifications") {
                NotificationsScreen(onBack = back, onOpen = { r -> if (r in knownRoutes && r != "notifications") go(r) })
            }

            // ---- Practice ----
            composable("rhythm") { RhythmTodayScreen(onBack = back, onNavigate = go) }
            composable("lesson/{id}", listOf(navArgument("id") { type = NavType.IntType })) { e ->
                val id = e.arguments!!.getInt("id")
                LessonDetailScreen(id, onBack = back, onBegin = { nav.navigate("player/$id") }, onPlans = { nav.navigate("plans") }, onLesson = { nav.navigate("lesson/$it") })
            }
            composable("player/{id}", listOf(navArgument("id") { type = NavType.IntType })) { e ->
                val id = e.arguments!!.getInt("id")
                PlayerScreen(
                    id, onBack = back,
                    onFinished = { nav.navigate("complete") { popUpTo("lesson/$id") } },
                    onPlans = { nav.navigate("plans") },
                )
            }
            composable("complete") {
                PracticeCompleteScreen(onHome = { nav.goTab("home") }, onSaved = { nav.goTab("journal") }, notify = notify)
            }

            // ---- Explore / shop ----
            composable("explore") { ExploreScreen(onArticle = { nav.navigate("article/${android.net.Uri.encode(it)}") }, onShop = { nav.navigate("shop") }) }
            composable("article/{title}", listOf(navArgument("title") { type = NavType.StringType })) { e ->
                ArticleScreen(e.arguments!!.getString("title") ?: "", onBack = back, notify = notify)
            }
            composable("shop") { ShopScreen(onBack = back, notify = notify) }

            // ---- Subscriptions & bookings ----
            composable("plans") {
                PlansScreen(session, onBack = back, onPayment = { id -> nav.navigate("payment/$id") }, notify = notify)
            }
            composable("payment/{id}", listOf(navArgument("id") { type = NavType.IntType })) { e ->
                PaymentScreen(
                    e.arguments!!.getInt("id"), session,
                    onDone = { nav.goTab("home") }, onBack = back, notify = notify,
                )
            }
            composable("subscription") { SubscriptionScreen(session, onBack = back, onPlans = { nav.navigate("plans") }, notify = notify) }
            composable("bookings") { BookingsScreen(onBack = back, onBrowse = { nav.goTab("classes") }, notify = notify) }

            // ---- Profile ----
            composable("edit_profile") { EditProfileScreen(session, onBack = back, notify = notify) }
            composable("change_password") { ChangePasswordScreen(session, onBack = back, notify = notify) }
            composable("settings") { SettingsScreen(session, onBack = back, onNavigate = go, onSignOut = { session.signOut() }) }

            // ---- Admin ----
            composable("admin") { AdminHomeScreen(onBack = back, onNavigate = go) }
            composable("admin/errors") { AdminErrorsScreen(onBack = back, notify = notify) }
            composable("admin/lessons") { AdminLessonsScreen(onBack = back, notify = notify) }
            composable("admin/classes") { AdminClassesScreen(onBack = back, notify = notify) }
            composable("admin/plans") { AdminPlansScreen(onBack = back, notify = notify) }
        }
          }
        }
      }
    }
}

private fun NavHostController.goTab(route: String) {
    navigate(route) {
        popUpTo("home") { saveState = true }
        launchSingleTop = true
        restoreState = true
    }
}
