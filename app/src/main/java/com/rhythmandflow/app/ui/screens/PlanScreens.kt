package com.rhythmandflow.app.ui.screens

import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.rhythmandflow.app.BuildConfig
import com.rhythmandflow.app.data.Outcome
import com.rhythmandflow.app.data.Plan
import com.rhythmandflow.app.data.Subscription
import com.rhythmandflow.app.ui.components.*
import com.rhythmandflow.app.ui.theme.Brand
import com.rhythmandflow.app.ui.viewmodel.PlansViewModel
import com.rhythmandflow.app.ui.viewmodel.SessionViewModel
import com.rhythmandflow.app.ui.viewmodel.appViewModel
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

@Composable
fun PlansScreen(session: SessionViewModel, onBack: () -> Unit, onPayment: (Int) -> Unit, notify: (String) -> Unit) {
    val vm = appViewModel(key = "plans") { PlansViewModel(it) }
    val state by vm.state.collectAsState()
    val subs by vm.subscriptions.collectAsState()
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    var busyPlan by remember { mutableStateOf<Int?>(null) }
    val current = subs.firstOrNull { it.grantsAccess }

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Choose your plan", "Monthly. Cancel any time.", onBack = onBack)
        when {
            state.loading -> LoadingBox()
            state.error != null -> ErrorBox(state.error!!, onRetry = { vm.load() })
            else -> Column(Modifier.vScroll().padding(horizontal = 20.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
                if (current != null) {
                    SoftCard(Modifier.fillMaxWidth(), background = Brand.TealSoft) {
                        Text("You're on the ${current.planName} plan.", style = MaterialTheme.typography.titleMedium)
                    }
                }
                state.plans.forEachIndexed { i, plan ->
                    PlanCard(
                        plan = plan, popular = plan.tier == 2, isCurrent = current?.planId == plan.id,
                        busy = busyPlan == plan.id,
                        onSubscribe = {
                            busyPlan = plan.id
                            scope.launch {
                                when (val r = vm.checkout(plan.id)) {
                                    is Outcome.Ok -> {
                                        // Payment happens on PayFast's own hosted page, not inside this app.
                                        try {
                                            context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(r.value.paymentUrl)))
                                        } catch (e: Exception) { notify("Couldn't open the payment page.") }
                                        onPayment(r.value.subscriptionId)
                                    }
                                    is Outcome.Fail -> notify(r.message)
                                }
                                busyPlan = null
                            }
                        },
                    )
                }
                Text(
                    "Payments are processed securely by PayFast. Rhythm & Flow never sees or stores your card details.",
                    style = MaterialTheme.typography.bodySmall, color = Brand.Muted, textAlign = TextAlign.Center, modifier = Modifier.fillMaxWidth(),
                )
                VSpace(16)
            }
        }
    }
}

@Composable
private fun PlanCard(plan: Plan, popular: Boolean, isCurrent: Boolean, busy: Boolean, onSubscribe: () -> Unit) {
    SoftCard(Modifier.fillMaxWidth(), background = if (popular) Brand.TealSoft else Color.White) {
        Column(Modifier.fillMaxWidth()) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(plan.name, style = MaterialTheme.typography.titleLarge, modifier = Modifier.weight(1f))
                if (popular) InfoPill("Most popular", color = Brand.TangerineDeep, textColor = androidx.compose.ui.graphics.Color.White)
            }
            Row(verticalAlignment = Alignment.Bottom) {
                Text(formatRand(plan.price), style = MaterialTheme.typography.headlineLarge)
                Text(" / month", color = Brand.Muted, modifier = Modifier.padding(bottom = 6.dp))
            }
            Text(plan.description, color = Brand.Muted, style = MaterialTheme.typography.bodyMedium)
            VSpace(10)
            plan.features.forEach { f ->
                Row(Modifier.padding(vertical = 2.dp), verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Default.CheckCircle, null, tint = Brand.Teal, modifier = Modifier.size(18.dp))
                    HSpace(8)
                    Text(f, style = MaterialTheme.typography.bodyMedium)
                }
            }
            VSpace(14)
            if (isCurrent) SecondaryButton("Your current plan", onClick = {}, enabled = false)
            else PrimaryButton("Subscribe", onClick = onSubscribe, loading = busy)
        }
    }
}

/** Waits for PayFast to confirm the payment to our server, then tells the user. The app never decides a payment succeeded. */
@Composable
fun PaymentScreen(subId: Int, session: SessionViewModel, onDone: () -> Unit, onBack: () -> Unit, notify: (String) -> Unit) {
    val vm = appViewModel(key = "plans") { PlansViewModel(it) }
    var sub by remember { mutableStateOf<Subscription?>(null) }
    var timedOut by remember { mutableStateOf(false) }
    val scope = rememberCoroutineScope()

    LaunchedEffect(subId) {
        var tries = 0
        while (tries < 60) {
            val s = vm.subscription(subId)
            sub = s
            if (s?.status == "ACTIVE") { session.refreshSubscriptions(); return@LaunchedEffect }
            if (s?.status == "CANCELLED" || s?.status == "FAILED") return@LaunchedEffect
            delay(3000); tries++
        }
        timedOut = true
    }

    val status = sub?.status
    Column(Modifier.fillMaxSize().padding(24.dp), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center) {
        when {
            status == "ACTIVE" -> {
                Icon(Icons.Default.CheckCircle, null, tint = Brand.TealDeep, modifier = Modifier.size(72.dp))
                VSpace(12)
                Text("You're subscribed!", style = MaterialTheme.typography.headlineMedium)
                Text("Your ${sub?.planName} plan is active. Enjoy your practice.", color = Brand.Muted, textAlign = TextAlign.Center)
                VSpace(24)
                PrimaryButton("Start moving", onClick = onDone)
            }
            status == "CANCELLED" || status == "FAILED" -> {
                Text("Payment not completed", style = MaterialTheme.typography.headlineMedium)
                Text("No payment was taken. You can try again from the plans screen.", color = Brand.Muted, textAlign = TextAlign.Center)
                VSpace(24)
                PrimaryButton("Back to plans", onClick = onBack)
            }
            timedOut -> {
                Text("Still waiting for confirmation", style = MaterialTheme.typography.headlineMedium, textAlign = TextAlign.Center)
                Text("If you completed the payment, it can take a few minutes. Your subscription will activate automatically.", color = Brand.Muted, textAlign = TextAlign.Center)
                VSpace(24)
                PrimaryButton("Back to home", onClick = onDone)
            }
            else -> {
                CircularProgressIndicator(color = Brand.TealDeep)
                VSpace(16)
                Text("Complete your payment", style = MaterialTheme.typography.headlineMedium)
                Text(
                    "Finish paying on the PayFast page in your browser, then come back here. We'll activate your plan as soon as PayFast confirms it.",
                    color = Brand.Muted, textAlign = TextAlign.Center,
                )
                VSpace(24)
                SecondaryButton("Back", onClick = onBack)
            }
        }
        if (BuildConfig.SIMULATE_PAYMENT && status != "ACTIVE") {
            VSpace(24)
            HorizontalDivider(color = Brand.LightGrey)
            VSpace(12)
            Text("Developer tools (debug builds only)", style = MaterialTheme.typography.labelMedium, color = Brand.Muted)
            VSpace(8)
            AccentButton("Simulate PayFast sandbox payment", onClick = {
                scope.launch { notify(vm.simulatePayment(subId) ?: "Simulated payment sent.") }
            })
        }
    }
}

@Composable
fun SubscriptionScreen(session: SessionViewModel, onBack: () -> Unit, onPlans: () -> Unit, notify: (String) -> Unit) {
    val vm = appViewModel(key = "plans") { PlansViewModel(it) }
    val subs by vm.subscriptions.collectAsState()
    val scope = rememberCoroutineScope()
    var confirm by remember { mutableStateOf<Subscription?>(null) }
    var cancelledMessage by remember { mutableStateOf<String?>(null) }
    var cancelling by remember { mutableStateOf(false) }
    LaunchedEffect(Unit) { vm.refreshSubs() }

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("My subscription", onBack = onBack)
        Column(Modifier.vScroll().padding(horizontal = 20.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            val visible = subs.filter { it.status != "PENDING" && it.status != "FAILED" }
            if (visible.isEmpty()) {
                EmptyState("No subscription yet", "Choose a plan to unlock every programme.")
                PrimaryButton("View plans", onClick = onPlans)
            }
            visible.forEach { s ->
                SoftCard(Modifier.fillMaxWidth()) {
                    Column(Modifier.fillMaxWidth()) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Text(s.planName, style = MaterialTheme.typography.titleLarge, modifier = Modifier.weight(1f))
                            InfoPill(s.status.lowercase().replace('_', ' ').replaceFirstChar { it.uppercase() })
                        }
                        Text("${formatRand(s.price)} per month", color = Brand.Muted)
                        s.endDate?.let {
                            Text(
                                if (s.status == "CANCELLED") "Access until ${formatDate(it)}" else "Renews ${formatDate(it)}",
                                color = Brand.Muted, style = MaterialTheme.typography.bodyMedium,
                            )
                        }
                        if (s.status == "ACTIVE") { VSpace(10); SecondaryButton("Cancel subscription", onClick = { confirm = s }) }
                    }
                }
            }
            if (visible.isNotEmpty()) { SecondaryButton("Change plan", onClick = onPlans) }
        }
    }

    confirm?.let { s ->
        AlertDialog(
            onDismissRequest = { confirm = null },
            title = { Text("Cancel your subscription?") },
            text = { Text("We'll tell PayFast to stop your monthly payments. You'll keep access to your plan until ${formatDate(s.endDate)}.") },
            confirmButton = {
                TextButton(enabled = !cancelling, onClick = {
                    confirm = null
                    cancelling = true
                    scope.launch {
                        when (val r = vm.cancel(s.id)) {
                            is Outcome.Ok -> { cancelledMessage = r.value.message; session.refreshSubscriptions() }
                            is Outcome.Fail -> notify(r.message)
                        }
                        cancelling = false
                    }
                }) { Text("Cancel subscription", color = Brand.Error) }
            },
            dismissButton = { TextButton(onClick = { confirm = null }) { Text("Keep it") } },
        )
    }

    cancelledMessage?.let { msg ->
        AlertDialog(
            onDismissRequest = { cancelledMessage = null },
            title = { Text("Subscription cancelled") },
            text = { Text(msg) },
            confirmButton = { TextButton(onClick = { cancelledMessage = null }) { Text("OK") } },
        )
    }
}
