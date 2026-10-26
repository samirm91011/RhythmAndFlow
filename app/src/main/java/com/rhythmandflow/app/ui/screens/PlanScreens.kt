package com.rhythmandflow.app.ui.screens

import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.clickable
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
import com.rhythmandflow.app.data.PlanChangePreview
import com.rhythmandflow.app.data.Policy
import com.rhythmandflow.app.data.Subscription
import com.rhythmandflow.app.ui.components.*
import com.rhythmandflow.app.ui.theme.Brand
import com.rhythmandflow.app.ui.viewmodel.PlansViewModel
import com.rhythmandflow.app.ui.viewmodel.SessionViewModel
import com.rhythmandflow.app.ui.viewmodel.appViewModel
import java.time.Instant
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

/** Statuses that mean the person has held a plan before, so the launch price and free trial are no longer for them. */
private val heldBefore = setOf("ACTIVE", "CANCELLED", "EXPIRED", "PAST_DUE")

private fun isFuture(iso: String?): Boolean = try { iso != null && Instant.parse(iso).isAfter(Instant.now()) } catch (_: Exception) { false }

private fun billingWord(billing: String) = if (billing == "ANNUAL") "year" else "month"

private fun priceLine(price: Double, billing: String) = "${formatRand(price)} per ${billingWord(billing)}"

/** A plan change that is waiting for the customer to confirm it. */
private data class PendingChange(val plan: Plan, val billing: String, val preview: PlanChangePreview)

@Composable
fun PlansScreen(
    session: SessionViewModel, onBack: () -> Unit, onPayment: (Int) -> Unit, onPolicies: () -> Unit, notify: (String) -> Unit,
) {
    val vm = appViewModel(key = "plans") { PlansViewModel(it) }
    val state by vm.state.collectAsState()
    val subs by vm.subscriptions.collectAsState()
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    var busyPlan by remember { mutableStateOf<Int?>(null) }
    var yearly by remember { mutableStateOf(false) }
    var pending by remember { mutableStateOf<PendingChange?>(null) }
    var confirming by remember { mutableStateOf(false) }

    val current = subs.firstOrNull { it.status == "ACTIVE" && it.grantsAccess }
    // A change that was already confirmed: the new plan is active but has not started yet.
    val waiting = subs.firstOrNull { it.status == "ACTIVE" && !it.grantsAccess && isFuture(it.startDate) }
    val isNew = subs.none { it.status in heldBefore }
    val anyYearly = state.plans.any { it.annualPrice != null }

    fun openPayFast(url: String, subId: Int) {
        // Payment happens on PayFast's own hosted page, not inside this app.
        try {
            context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)))
        } catch (e: Exception) { notify("Couldn't open the payment page.") }
        onPayment(subId)
    }

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Choose your plan", if (current != null) "Change your plan any time." else "Cancel any time.", onBack = onBack)
        when {
            state.loading -> LoadingBox()
            state.error != null -> ErrorBox(state.error!!, onRetry = { vm.load() })
            else -> Column(Modifier.vScroll().padding(horizontal = 20.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
                if (current != null) {
                    SoftCard(Modifier.fillMaxWidth(), background = Brand.TealSoft) {
                        Text("You're on the ${current.planName} plan (${priceLine(current.price, current.billing)}).", style = MaterialTheme.typography.titleMedium)
                    }
                }
                if (waiting != null) {
                    SoftCard(Modifier.fillMaxWidth(), background = Brand.TealSoft) {
                        Text("Your change to ${waiting.planName} starts on ${formatDate(waiting.startDate)}.", style = MaterialTheme.typography.titleMedium)
                    }
                }
                if (anyYearly) {
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        SelectChip("Monthly", !yearly, onClick = { yearly = false })
                        SelectChip("Yearly", yearly, onClick = { yearly = true })
                    }
                }
                state.plans.forEach { plan ->
                    val billing = if (yearly && plan.annualPrice != null) "ANNUAL" else "MONTHLY"
                    val sameAsCurrent = current != null && current.planId == plan.id && current.billing == billing
                    val label = when {
                        sameAsCurrent -> "Your current plan"
                        current != null && current.planId == plan.id -> if (billing == "ANNUAL") "Switch to yearly" else "Switch to monthly"
                        current != null -> "Change to this plan"
                        isNew && plan.trialDays > 0 -> "Start ${plan.trialDays}-day free trial"
                        else -> "Subscribe"
                    }
                    PlanCard(
                        plan = plan, billing = billing, isNew = isNew,
                        buttonLabel = label, enabled = !sameAsCurrent && waiting == null,
                        busy = busyPlan == plan.id,
                        onClick = {
                            busyPlan = plan.id
                            scope.launch {
                                if (current == null) {
                                    when (val r = vm.checkout(plan.id, billing)) {
                                        is Outcome.Ok -> openPayFast(r.value.paymentUrl, r.value.subscriptionId)
                                        is Outcome.Fail -> notify(r.message)
                                    }
                                } else {
                                    when (val r = vm.changePreview(current.id, plan.id, billing)) {
                                        is Outcome.Ok -> pending = PendingChange(plan, billing, r.value)
                                        is Outcome.Fail -> notify(r.message)
                                    }
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
                Text(
                    "Cancellation and renewal policies",
                    style = MaterialTheme.typography.bodyMedium, color = Brand.TealDeep, textAlign = TextAlign.Center,
                    modifier = Modifier.fillMaxWidth().clickable(onClick = onPolicies).padding(vertical = 8.dp),
                )
                VSpace(16)
            }
        }
    }

    pending?.let { change ->
        val p = change.preview
        AlertDialog(
            onDismissRequest = { if (!confirming) pending = null },
            title = { Text("Change your plan?") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("From: ${p.currentPlan}, ${priceLine(p.currentPrice, p.currentBilling)}")
                    Text("To: ${p.newPlan}, ${priceLine(p.newPrice, p.newBilling)}")
                    val diff = p.priceDifference
                    Text(
                        when {
                            diff > 0 -> "Price difference: ${formatRand(diff)} more per month"
                            diff < 0 -> "Price difference: ${formatRand(-diff)} less per month"
                            else -> "Price difference: none per month"
                        },
                    )
                    Text("Starts on: ${formatDate(p.effectiveDate)}")
                    Text(p.accessImpact, color = Brand.Muted, style = MaterialTheme.typography.bodyMedium)
                    Text(
                        "You'll finish setting up the new plan on the PayFast page. Your current plan stops renewing, and you keep it until ${formatDate(p.effectiveDate)}.",
                        color = Brand.Muted, style = MaterialTheme.typography.bodySmall,
                    )
                }
            },
            confirmButton = {
                TextButton(enabled = !confirming, onClick = {
                    confirming = true
                    scope.launch {
                        val from = current
                        if (from != null) {
                            when (val r = vm.changePlan(from.id, change.plan.id, change.billing)) {
                                is Outcome.Ok -> { pending = null; openPayFast(r.value.paymentUrl, r.value.subscriptionId) }
                                is Outcome.Fail -> { pending = null; notify(r.message) }
                            }
                        }
                        confirming = false
                    }
                }) { Text(if (confirming) "Working…" else "Confirm change") }
            },
            dismissButton = { TextButton(enabled = !confirming, onClick = { pending = null }) { Text("Not now") } },
        )
    }
}

@Composable
private fun PlanCard(plan: Plan, billing: String, isNew: Boolean, buttonLabel: String, enabled: Boolean, busy: Boolean, onClick: () -> Unit) {
    val yearly = billing == "ANNUAL" && plan.annualPrice != null
    val launch = if (!yearly && isNew) plan.launchPrice else null
    val trial = isNew && plan.trialDays > 0
    SoftCard(Modifier.fillMaxWidth(), background = Color.White) {
        Column(Modifier.fillMaxWidth()) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(plan.name, style = MaterialTheme.typography.titleLarge, modifier = Modifier.weight(1f))
                if (trial) InfoPill("${plan.trialDays}-day free trial", color = Brand.TangerineDeep, textColor = Color.White)
            }
            when {
                yearly -> {
                    val year = plan.annualPrice!!
                    Row(verticalAlignment = Alignment.Bottom) {
                        Text(formatRand(year), style = MaterialTheme.typography.headlineLarge)
                        Text(" / year", color = Brand.Muted, modifier = Modifier.padding(bottom = 6.dp))
                    }
                    Text("That's ${formatRand(Math.round(year / 12.0).toDouble())} a month, paid once a year.", color = Brand.Muted, style = MaterialTheme.typography.bodyMedium)
                    val saving = plan.price * 12 - year
                    if (saving > 0) Text("You save ${formatRand(saving)} compared with paying monthly.", color = Brand.TealDeep, style = MaterialTheme.typography.bodyMedium)
                }
                launch != null -> {
                    Row(verticalAlignment = Alignment.Bottom) {
                        Text(formatRand(launch), style = MaterialTheme.typography.headlineLarge)
                        Text(" first month", color = Brand.Muted, modifier = Modifier.padding(bottom = 6.dp))
                    }
                    Text("Then ${formatRand(plan.price)} per month. Launch price ends ${formatDate(plan.launchEndsAt)}.", color = Brand.Muted, style = MaterialTheme.typography.bodyMedium)
                }
                else -> Row(verticalAlignment = Alignment.Bottom) {
                    Text(formatRand(plan.price), style = MaterialTheme.typography.headlineLarge)
                    Text(" / month", color = Brand.Muted, modifier = Modifier.padding(bottom = 6.dp))
                }
            }
            if (trial) Text("Free for ${plan.trialDays} days, then ${priceLine(if (yearly) plan.annualPrice!! else plan.price, billing)}. Cancel before then and you pay nothing.", color = Brand.Muted, style = MaterialTheme.typography.bodyMedium)
            VSpace(6)
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
            if (!enabled) SecondaryButton(buttonLabel, onClick = {}, enabled = false)
            else PrimaryButton(buttonLabel, onClick = onClick, loading = busy)
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
    val startsLater = status == "ACTIVE" && isFuture(sub?.startDate)
    Column(Modifier.fillMaxSize().padding(24.dp), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center) {
        when {
            status == "ACTIVE" -> {
                Icon(Icons.Default.CheckCircle, null, tint = Brand.TealDeep, modifier = Modifier.size(72.dp))
                VSpace(12)
                Text(
                    when { startsLater -> "Your new plan is set up"; sub?.inTrial == true -> "Your free trial has started"; else -> "You're subscribed!" },
                    style = MaterialTheme.typography.headlineMedium, textAlign = TextAlign.Center,
                )
                Text(
                    when {
                        startsLater -> "${sub?.planName} starts on ${formatDate(sub?.startDate)}. Until then you keep your current plan."
                        sub?.inTrial == true -> "Enjoy ${sub?.planName} free until ${formatDate(sub?.endDate)}. After that ${priceLine(sub!!.price, sub!!.billing)} is charged unless you cancel."
                        else -> "Your ${sub?.planName} plan is active. Enjoy your practice."
                    },
                    color = Brand.Muted, textAlign = TextAlign.Center,
                )
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
fun SubscriptionScreen(
    session: SessionViewModel, onBack: () -> Unit, onPlans: () -> Unit, onPayments: () -> Unit, onPolicies: () -> Unit, notify: (String) -> Unit,
) {
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
                val startsLater = s.status == "ACTIVE" && isFuture(s.startDate)
                SoftCard(Modifier.fillMaxWidth()) {
                    Column(Modifier.fillMaxWidth()) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Text(s.planName, style = MaterialTheme.typography.titleLarge, modifier = Modifier.weight(1f))
                            InfoPill(
                                when { s.inTrial -> "Free trial"; startsLater -> "Starts soon"; else -> s.status.lowercase().replace('_', ' ').replaceFirstChar { it.uppercase() } },
                            )
                        }
                        Text(priceLine(s.price, s.billing), color = Brand.Muted)
                        val line = when {
                            s.inTrial -> "Free until ${formatDate(s.endDate)}, then ${priceLine(s.price, s.billing)}."
                            startsLater -> "Starts ${formatDate(s.startDate)}."
                            s.status == "CANCELLED" && s.endDate != null -> "Access until ${formatDate(s.endDate)}"
                            s.renewing && s.endDate != null -> "Next payment of ${formatRand(s.price)} on ${formatDate(s.endDate)}"
                            s.endDate != null -> "Renews ${formatDate(s.endDate)}"
                            else -> null
                        }
                        line?.let { Text(it, color = Brand.Muted, style = MaterialTheme.typography.bodyMedium) }
                        if (s.status == "ACTIVE" && !startsLater) { VSpace(10); SecondaryButton("Cancel subscription", onClick = { confirm = s }) }
                    }
                }
            }
            val renewing = visible.firstOrNull { it.status == "ACTIVE" && it.grantsAccess }
            if (renewing != null) {
                SecondaryButton("Change plan", onClick = onPlans)
                Text(
                    "Changing plan shows you the price difference and the start day before anything is confirmed. You keep your current plan until the end of the period you've paid for.",
                    color = Brand.Muted, style = MaterialTheme.typography.bodyMedium,
                )
            } else if (visible.isNotEmpty() && visible.none { it.status == "ACTIVE" }) {
                PrimaryButton("Choose a new plan", onClick = onPlans)
            }
            if (visible.isNotEmpty()) { SecondaryButton("Payment history", onClick = onPayments) }
            Text(
                "Cancellation and renewal policies",
                style = MaterialTheme.typography.bodyMedium, color = Brand.TealDeep, textAlign = TextAlign.Center,
                modifier = Modifier.fillMaxWidth().clickable(onClick = onPolicies).padding(vertical = 8.dp),
            )
        }
    }

    confirm?.let { s ->
        AlertDialog(
            onDismissRequest = { confirm = null },
            title = { Text("Cancel your subscription?") },
            text = { Text("We'll tell PayFast to stop your ${if (s.billing == "ANNUAL") "yearly" else "monthly"} payments. You'll keep access to your plan until ${formatDate(s.endDate)}.") },
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

/** The client's own policy wording, read from the server so the app, the web version and the emails all say the same thing. */
@Composable
fun PoliciesScreen(onBack: () -> Unit, only: Set<String>? = null) {
    val vm = appViewModel(key = "plans") { PlansViewModel(it) }
    var policies by remember { mutableStateOf<List<Policy>?>(null) }
    var error by remember { mutableStateOf<String?>(null) }
    var open by remember { mutableStateOf<String?>(null) }

    LaunchedEffect(Unit) {
        when (val r = vm.policies()) {
            is Outcome.Ok -> policies = r.value.filter { only == null || it.id in only }
            is Outcome.Fail -> error = r.message
        }
    }

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Policies", "Cancellation, renewal and events.", onBack = onBack)
        when {
            error != null -> ErrorBox(error!!, onRetry = null)
            policies == null -> LoadingBox()
            else -> Column(Modifier.vScroll().padding(horizontal = 20.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                policies!!.forEach { p ->
                    SoftCard(Modifier.fillMaxWidth(), onClick = { open = if (open == p.id) null else p.id }) {
                        Column(Modifier.fillMaxWidth()) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Text(p.title, style = MaterialTheme.typography.titleMedium, modifier = Modifier.weight(1f))
                                Text(if (open == p.id) "Hide" else "Read", color = Brand.TealDeep)
                            }
                            if (open == p.id) {
                                VSpace(8)
                                Text(p.body, style = MaterialTheme.typography.bodyMedium, color = Brand.Muted)
                            }
                        }
                    }
                }
                VSpace(16)
            }
        }
    }
}
