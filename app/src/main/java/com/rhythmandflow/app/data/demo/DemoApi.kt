package com.rhythmandflow.app.data.demo

import com.rhythmandflow.app.data.*
import java.time.Duration
import java.time.Instant
import kotlinx.coroutines.delay
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import retrofit2.HttpException
import retrofit2.Response

/**
 * Built-in sample data so the whole app can run with no server (switch on with  gradlew assembleDebug -PdemoMode=true).
 * Used for screen-by-screen visual testing and as an emergency fallback for a presentation. Off in every normal build.
 * Any password works; a username containing "admin" signs in as an administrator. Reset code: 123456.
 */
class DemoApi : Api {
    private var user = User("demo-user", "Alex Demo", "alex", "alex@rhythmandflow.test", "CUSTOMER", "A work in progress, and that's enough.")
    private var tier = 0
    private var sub: Subscription? = null
    private var nextSub: Subscription? = null   // a plan change waiting for the current period to end
    private var pendingBilling = "MONTHLY"
    private var checkoutPolls = 0
    private var nextId = 100

    private val plans = mutableListOf(
        Plan(1, "Move & Breathe", "Your space to move, pause and reconnect with yourself, at your own pace.", 149.0, "MONTHLY", 1, listOf("Full on-demand video library", "Yoga, barre, dance, mobility and stretching", "Meditation, breathwork and strength classes", "Beginner, intermediate and advanced options"), annualPrice = 1490.0, launchPrice = 99.0, launchEndsAt = Instant.now().plus(Duration.ofDays(10)).toString()),
        Plan(2, "Move, Breathe & Grow", "Build a more intentional practice through movement, mindful breathing and guided programmes.", 249.0, "MONTHLY", 2, listOf("Everything in Move & Breathe", "Access to all structured programmes", "Guided journeys combining movement, mobility, relaxation and wellbeing", "A more structured way to build consistency"), annualPrice = 2490.0, trialDays = 7),
    )

    private data class L(val id: Int, val prog: Int, val progName: String, val minTier: Int, val title: String, val desc: String, val cat: String, val secs: Int, val preview: Boolean)
    private val lessonsData = mutableListOf(
        L(1, 1, "Move & Release", 1, "Full Body Flow", "A gentle flow to move, breathe and reconnect with your body.", "Yoga", 600, true),
        L(2, 1, "Move & Release", 1, "Move & Release", "Release tension and feel lighter.", "Stretch & Mobility", 900, false),
        L(3, 2, "Dance with Joy", 1, "Dance with Joy", "Feel-good movement for your mood.", "Dance", 1500, true),
        L(4, 2, "Dance with Joy", 1, "Barre Basics", "Build strength and grace at the barre.", "Barre & Pilates", 1200, false),
        L(5, 3, "Mindful Mobility", 1, "Gentle Mobility", "Ease into stretching and mobility work.", "Stretch & Mobility", 900, false),
        L(6, 3, "Mindful Mobility", 1, "6-Minute Reset", "A short guided meditation to breathe and reset.", "Meditation", 360, false),
        L(7, 4, "7-Day Flow Plan", 2, "Day 1: Sunrise Flow", "A longer morning flow to start your day.", "Yoga", 2100, false),
        L(8, 4, "7-Day Flow Plan", 2, "Day 2: Dance Cardio Burn", "High energy dance for a full body workout.", "Dance", 1800, false),
        L(9, 3, "Mindful Mobility", 1, "Strength Basics", "Build strength and stability with simple at-home moves.", "Strength & Conditioning", 1200, false),
        L(10, 3, "Mindful Mobility", 1, "Box Breathing", "A short breathing practice for focus and calm.", "Breathwork", 300, false),
    )
    private val watched = mutableMapOf(1 to 240)   // Alex is part-way through the first lesson, so Home shows "continue"

    private val classes = mutableListOf(
        mkClass(1, "Morning Flow", 1, 7, 20, 3), mkClass(2, "Dance with Joy", 2, 12, 20, 12), mkClass(3, "Barre & Stretch", 3, 17, 20, 0),
        mkClass(7, "Breathwork Workshop", 8, 18, 12, 12).copy(
            description = "A guided evening of breathing practices for focus, relaxation and an intentional pause.", kind = "WORKSHOP", format = "ONLINE",
            location = "Online", price = 250.0, waitlistEnabled = true, joinPlatform = "Zoom",
        ),
        mkClass(4, "Yoga Reset", 4, 8, 15, 15), mkClass(5, "Weekend Wind-Down", 5, 9, 20, 18), mkClass(6, "Mobility for Life", 6, 10, 20, 20),
    )
    private val booked = mutableMapOf<Int, Int>()          // bookingId -> classId
    private val paidTotal = mutableMapOf<Int, Double>()    // bookingId -> what was paid for a ticket
    private val waiting = mutableSetOf<Int>()              // class ids I am on the waiting list for
    private var credit = 0.0
    private val demoRequests = mutableListOf(
        AdminEventRequest(901, "RF-E000901", "Thandi Mokoena", "thandi@example.com", "Breathwork Workshop", Instant.now().plus(Duration.ofHours(20)).toString(), 250.0, 125.0, "REFUND", ago(90)),
    )
    private val demoRefunds = mutableListOf(
        AdminRefund(902, "RF-E000902", "Alex Demo", "alex@rhythmandflow.test", "Sunrise Retreat", 1800.0, "PF-1234567"),
    )
    private val journal = mutableListOf(
        JournalEntry(1, "REFLECTION", "Calmer", "Felt tight this morning, a slow flow helped a lot.", ago(26 * 60)),
        JournalEntry(2, "GRATITUDE", "", "My body for carrying me through the week.", ago(50 * 60)),
    )
    private val notifications = mutableListOf(
        AppNotification(3, "CLASS", "Class cancelled", "Sorry, Barre & Stretch on Fri 5 Oct has been cancelled.", "classes", ago(12), false),
        AppNotification(2, "PAYMENT", "Welcome to Rhythm & Flow", "Subscribe to unlock every programme.", "plans", ago(180), true),
    )
    private val errors = mutableListOf(
        ErrorLogItem(3, "APP", "[CRASH] NullPointerException: lesson was null", "java.lang.NullPointerException\n  at com.rhythmandflow.app.Player.play(Player.kt:42)\n  at ...", "app crash", "alex@rhythmandflow.test", "Pixel 8 (Android 15)", "1.0", 2, ago(240), ago(35), "NEW"),
        ErrorLogItem(2, "API", "Server error 500 on /api/lessons/9/playback", "System.InvalidOperationException: Sequence contains no elements", "GET /api/lessons/9/playback", null, null, null, 1, ago(600), ago(600), "NEW"),
        ErrorLogItem(1, "APP", "SocketTimeoutException: timeout", "okhttp3 ...", "home", "sam@example.com", "Galaxy A14 (Android 14)", "1.0", 5, ago(2000), ago(900), "RESOLVED"),
    )

    // ---- helpers
    private suspend fun pause() = delay(180)
    private fun ago(minutes: Long) = Instant.now().minus(Duration.ofMinutes(minutes)).toString()
    private fun fail(code: Int, message: String): Nothing =
        throw HttpException(Response.error<Any>(code, "{\"error\":\"$message\"}".toResponseBody("application/json".toMediaType())))
    private fun ok() = Response.success(Unit)
    private fun mkClass(id: Int, name: String, days: Long, hour: Long, capacity: Int, left: Int): ClassItem {
        val start = Instant.now().plus(Duration.ofDays(days)).atZone(java.time.ZoneId.of("Africa/Johannesburg")).withHour(hour.toInt()).withMinute(0).withSecond(0).withNano(0).toInstant()
        return ClassItem(id, name, "All levels welcome. Bring water and a mat.", "Deni", "Rhythm & Flow Studio", start.toString(), start.plus(Duration.ofHours(1)).toString(), capacity, left, false, "SCHEDULED")
    }
    private fun lessonDto(l: L): Lesson {
        val locked = user.role != "ADMIN" && !(l.preview || tier >= l.minTier)
        val w = watched[l.id] ?: 0
        return Lesson(l.id, l.prog, l.progName, l.title, l.desc, l.cat, "All levels", l.secs, null, l.preview, locked, (w * 100.0 / l.secs).coerceAtMost(100.0), w)
    }
    private fun session(admin: Boolean, name: String? = null, email: String? = null, username: String? = null): AuthResponse {
        user = User("demo-user", name ?: if (admin) "Rhythm Admin" else "Alex Demo", username ?: if (admin) "admin" else "alex",
            email ?: if (admin) "admin@rhythmandflow.test" else "alex@rhythmandflow.test", if (admin) "ADMIN" else "CUSTOMER", user.about)
        return AuthResponse("demo-token", user)
    }
    private fun periodPrice(plan: Plan, billing: String) = if (billing == "ANNUAL") plan.annualPrice ?: plan.price * 12 else plan.price
    private fun activate(plan: Plan) {
        tier = plan.tier
        val days = if (pendingBilling == "ANNUAL") 365L else 30L
        val end = Instant.now().plus(Duration.ofDays(days)).toString()
        sub = Subscription(1, plan.id, plan.name, plan.tier, periodPrice(plan, pendingBilling), "ACTIVE", Instant.now().toString(), end, true, pendingBilling, true)
        notifications.add(0, AppNotification(++nextId, "PAYMENT", "You're subscribed!", "Your ${plan.name} plan is active. Enjoy your practice.", "subscription", ago(0), false))
    }

    // ---- auth
    override suspend fun register(body: RegisterRequest): AuthResponse { pause(); return session(false, body.fullName, body.email, body.username) }
    override suspend fun login(body: LoginRequest): AuthResponse {
        pause()
        if (body.password == "wrong") fail(401, "Incorrect username/email or password.")
        return session(body.identifier.contains("admin", ignoreCase = true))
    }
    override suspend fun me(): User { pause(); return user }
    override suspend fun updateProfile(body: UpdateProfileRequest): User { pause(); user = user.copy(fullName = body.fullName, email = body.email, about = body.about ?: ""); return user }
    override suspend fun forgotPassword(body: ForgotPasswordRequest): MessageResponse { pause(); return MessageResponse("If that email has an account, we've sent a 6-digit code. It expires in 15 minutes.") }
    override suspend fun resetPassword(body: ResetPasswordRequest): MessageResponse {
        pause()
        if (body.code != "123456") fail(400, "That code isn't valid or has expired. Please request a new one.")
        return MessageResponse("Your password has been changed. You can log in now.")
    }
    override suspend fun changePassword(body: ChangePasswordRequest): AuthResponse {
        pause()
        if (body.currentPassword == "wrong") fail(400, "Your current password isn't right.")
        return AuthResponse("demo-token", user)
    }

    override suspend fun registerDeviceToken(body: DeviceTokenRequest): MessageResponse = MessageResponse("Registered.")
    override suspend fun removeDeviceToken(body: DeviceTokenRequest): MessageResponse = MessageResponse("Removed.")
    override suspend fun payments(): List<PaymentItem> {
        pause()
        val s = sub?.takeIf { it.status == "ACTIVE" } ?: return emptyList()
        return listOf(
            PaymentItem(3, s.planName, s.price, "COMPLETE", ago(0), "RF-000003"),
            PaymentItem(2, s.planName, s.price, "COMPLETE", ago(60L * 24 * 30), "RF-000002"),
            PaymentItem(1, s.planName, s.price, "FAILED", ago(60L * 24 * 45), "RF-000001"),
        )
    }
    private val demoUsers = mutableListOf(
        AdminUser("u1", "Alex Demo", "alex", "alex@rhythmandflow.test", "CUSTOMER", "ACTIVE", ago(60L * 24 * 20), "Flow"),
        AdminUser("u2", "Thandi Mokoena", "thandi", "thandi@example.com", "CUSTOMER", "ACTIVE", ago(60L * 24 * 9), "Rhythm"),
        AdminUser("u3", "Sam Naidoo", "sam", "sam@example.com", "CUSTOMER", "ACTIVE", ago(60L * 24 * 3), null),
        AdminUser("u4", "Rhythm Admin", "admin", "admin@rhythmandflow.test", "ADMIN", "ACTIVE", ago(60L * 24 * 60), null),
    )
    override suspend fun adminUsers(search: String?): List<AdminUser> {
        pause()
        val q = search?.trim()?.lowercase().orEmpty()
        return demoUsers.filter { q.isEmpty() || it.fullName.lowercase().contains(q) || it.email.lowercase().contains(q) || it.username.lowercase().contains(q) }
    }
    override suspend fun adminSetUserStatus(id: String, body: SetUserStatusRequest): Response<Unit> {
        pause(); demoUsers.replaceAll { if (it.id == id) it.copy(status = body.status) else it }; return ok()
    }
    override suspend fun exportData(): okhttp3.ResponseBody {
        pause()
        val json = """{"exportedAt":"demo","profile":{"fullName":"${user.fullName}","email":"${user.email}"},"note":"Demo data only."}"""
        return json.toResponseBody("application/json".toMediaType())
    }
    override suspend fun deleteAccount(body: DeleteAccountRequest): MessageResponse {
        pause()
        if (body.password == "wrong") fail(400, "That password isn't right.")
        return MessageResponse("Your account and personal data have been deleted.")
    }

    // ---- plans & subscriptions
    override suspend fun plans(): List<Plan> { pause(); return plans.filter { it.id !in hiddenPlans }.sortedBy { it.price } }
    override suspend fun subscriptions(): List<Subscription> {
        pause()
        sub?.let { s -> if (s.status == "PENDING" && ++checkoutPolls >= 2) activate(plans.first { it.id == s.planId }) }
        return listOfNotNull(sub, nextSub)
    }
    override suspend fun checkout(body: CheckoutRequest): CheckoutResponse {
        pause(); checkoutPolls = 0
        val p = plans.first { it.id == body.planId }
        pendingBilling = if (body.billing.equals("ANNUAL", true) && p.annualPrice != null) "ANNUAL" else "MONTHLY"
        sub = Subscription(1, p.id, p.name, p.tier, periodPrice(p, pendingBilling), "PENDING", null, null, false, pendingBilling, false)
        return CheckoutResponse(1, "https://sandbox.payfast.co.za/eng/process?demo=true")
    }
    override suspend fun changePreview(id: Int, planId: Int, billing: String?): PlanChangePreview {
        pause()
        val cur = sub?.takeIf { it.status == "ACTIVE" } ?: fail(400, "You need an active plan to change it.")
        val p = plans.first { it.id == planId }
        val b = if (billing.equals("ANNUAL", true) && p.annualPrice != null) "ANNUAL" else "MONTHLY"
        if (p.id == cur.planId && b == cur.billing) fail(400, "That is the plan you already have.")
        val newPrice = periodPrice(p, b)
        val diff = (if (b == "ANNUAL") newPrice / 12 else newPrice) - (if (cur.billing == "ANNUAL") cur.price / 12 else cur.price)
        val end = cur.endDate.orEmpty()
        val impact = when {
            p.tier > cur.tier -> "You keep ${cur.planName} until ${end.take(10)}. From then you move up to ${p.name} and get everything it includes, including its extra programmes."
            p.tier < cur.tier -> "You keep ${cur.planName} until ${end.take(10)}. From then you move to ${p.name}, so programmes that need the higher plan will be locked."
            else -> "You keep ${cur.planName} until ${end.take(10)}. From then you are billed ${if (b == "ANNUAL") "yearly" else "monthly"} for ${p.name}. Your access stays the same."
        }
        return PlanChangePreview(cur.planName, cur.price, cur.billing, p.name, newPrice, b, Math.round(diff * 100) / 100.0, end, impact)
    }
    override suspend fun changePlan(id: Int, body: CheckoutRequest): CheckoutResponse {
        pause()
        val cur = sub?.takeIf { it.status == "ACTIVE" } ?: fail(400, "You need an active plan to change it.")
        val p = plans.first { it.id == body.planId }
        val b = if (body.billing.equals("ANNUAL", true) && p.annualPrice != null) "ANNUAL" else "MONTHLY"
        sub = cur.copy(status = "CANCELLED")
        val days = if (b == "ANNUAL") 365L else 30L
        val start = Instant.parse(cur.endDate)
        nextSub = Subscription(2, p.id, p.name, p.tier, periodPrice(p, b), "ACTIVE", start.toString(), start.plus(Duration.ofDays(days)).toString(), false, b, true)
        return CheckoutResponse(2, "https://sandbox.payfast.co.za/eng/process?demo=true")
    }
    override suspend fun policies(): List<Policy> {
        pause()
        return listOf(
            Policy("subscription-cancellation", "Subscription Cancellation Policy", "We understand that your needs and circumstances may change. Our subscription cancellation process is designed to be straightforward, transparent, and fair.\n\n1. How to cancel your subscription\nYou may request cancellation through your account settings in the Rhythm & Flow app, where available."),
            Policy("subscription-renewal", "Subscription Renewal Policy", "Monthly subscriptions are billed in advance and ordinarily renew each month on the relevant billing date until cancelled."),
            Policy("event-cancellation", "Event Cancellation Policy", "For cancellation requests received at least 48 hours before the event, the customer can choose a refund or account credit for the full event fee."),
            Policy("online-technical", "Online Classes: Technical Issues and Disruptions", "Rhythm & Flow will make reasonable efforts to ensure that online classes are delivered as advertised."),
        )
    }
    override suspend fun cancelSubscription(id: Int): CancelResult {
        pause()
        val s = sub ?: fail(409, "No active subscription found.")
        sub = s.copy(status = "CANCELLED", renewing = false)
        return CancelResult("Your subscription is cancelled and PayFast won't charge you again. You keep access until ${s.endDate?.take(10)}.", true)
    }
    override suspend fun simulatePayment(id: Int): Response<Unit> { pause(); sub?.let { activate(plans.first { p -> p.id == it.planId }) }; return ok() }

    // ---- content
    private val demoProgrammes = mutableListOf(
        AdminProgramme(1, "Move & Release", "Gentle practices to release tension and feel lighter.", 1, true, 2),
        AdminProgramme(2, "Dance with Joy", "Feel-good dance movement for every level.", 1, true, 2),
        AdminProgramme(3, "Mindful Mobility", "Stretch, restore and move well for life.", 1, true, 4),
        AdminProgramme(4, "7-Day Flow Plan", "A guided journey combining movement, breathing and rest.", 2, true, 2),
    )
    override suspend fun programmes(): List<Programme> {
        pause()
        return demoProgrammes.filter { it.active }.map { p -> Programme(p.id, p.name, p.description, p.minTier, user.role != "ADMIN" && tier < p.minTier, lessonsData.count { it.prog == p.id }) }
    }
    override suspend fun adminProgrammes(): List<AdminProgramme> { pause(); return demoProgrammes.map { p -> p.copy(lessonCount = lessonsData.count { it.prog == p.id }) } }
    override suspend fun adminCreateProgramme(body: ProgrammeUpsert): Int {
        pause()
        if (demoProgrammes.any { it.name.equals(body.name.trim(), true) }) fail(400, "There is already a programme with that name.")
        val id = demoProgrammes.maxOf { it.id } + 1
        demoProgrammes.add(AdminProgramme(id, body.name.trim(), body.description.orEmpty(), body.minTier, body.active, 0))
        return id
    }
    override suspend fun adminUpdateProgramme(id: Int, body: ProgrammeUpsert): Response<Unit> {
        pause()
        if (demoProgrammes.any { it.id != id && it.name.equals(body.name.trim(), true) }) fail(400, "There is already a programme with that name.")
        demoProgrammes.replaceAll { if (it.id == id) it.copy(name = body.name.trim(), description = body.description.orEmpty(), minTier = body.minTier, active = body.active) else it }
        return ok()
    }
    override suspend fun lessons(category: String?, query: String?, programmeId: Int?): List<Lesson> {
        pause()
        val hidden = demoProgrammes.filter { !it.active }.map { it.id }
        return lessonsData.filter {
            it.prog !in hidden && (programmeId == null || it.prog == programmeId) && (category == null || it.cat == category) &&
                (query.isNullOrBlank() || it.title.contains(query, true) || it.desc.contains(query, true))
        }.map(::lessonDto)
    }
    override suspend fun lesson(id: Int): Lesson { pause(); return lessonDto(lessonsData.first { it.id == id }) }
    override suspend fun playback(id: Int): Playback {
        pause()
        if (lessonDto(lessonsData.first { it.id == id }).locked) fail(403, "An active subscription is required to watch this lesson.")
        return Playback("https://storage.googleapis.com/exoplayer-test-media-1/mp4/android-screens-10s.mp4", Instant.now().plus(Duration.ofHours(1)).toString(), 0)
    }
    override suspend fun updateProgress(body: ProgressUpdate): ProgressResult {
        val l = lessonsData.first { it.id == body.lessonId }
        val w = maxOf(watched[l.id] ?: 0, body.watchTimeSeconds).coerceAtMost(l.secs); watched[l.id] = w
        val pct = w * 100.0 / l.secs
        return ProgressResult(l.id, w, pct, pct >= 95)
    }
    override suspend fun progressSummary(): ProgressSummary {
        pause()
        val done = lessonsData.count { (watched[it.id] ?: 0) * 100.0 / it.secs >= 95 }
        return ProgressSummary(done + 3, watched.size - done, 8 + watched.size, 42 + watched.values.sum() / 60)
    }

    // ---- classes & bookings
    private fun classDto(c: ClassItem): ClassItem = c.copy(bookedByMe = booked.containsValue(c.id), onWaitlist = c.id in waiting,
        canBuyNow = c.price > 0 && c.spotsLeft > 0 && c.id !in waiting,
        buyBlockedReason = if (c.price > 0 && c.spotsLeft == 0) "This event is sold out." else null)
    override suspend fun classes(): List<ClassItem> { pause(); return classes.map(::classDto) }
    private fun bookingDto(id: Int, classId: Int): Booking {
        val c = classes.first { it.id == classId }
        val paid = paidTotal[id] ?: 0.0
        val hoursLeft = Duration.between(Instant.now(), Instant.parse(c.startTime)).toHours()
        return Booking(
            id, c.id, c.name, c.coachName, c.location, c.startTime, c.endTime, "BOOKED", true,
            reference = if (paid > 0) "RF-E%06d".format(id) else "", kind = c.kind, format = c.format, totalPaid = paid,
            joinPlatform = if (c.format == "ONLINE") c.joinPlatform else null, joinLink = if (c.format == "ONLINE" && paid > 0) "https://zoom.us/j/0000000000" else null,
            freeCancellation = paid > 0 && hoursLeft >= 48,
        )
    }
    override suspend fun bookings(): List<Booking> { pause(); return booked.map { (id, cid) -> bookingDto(id, cid) } }
    override suspend fun buyTicket(id: Int, body: TicketRequest): TicketResponse {
        pause()
        val c = classes.first { it.id == id }
        if (booked.containsValue(id)) fail(409, "You already have a ticket for this event.")
        if (c.spotsLeft == 0) fail(409, "This event is sold out.")
        val useCredit = if (body.useCredit) minOf(credit, c.price) else 0.0
        credit -= useCredit
        classes[classes.indexOf(c)] = c.copy(spotsLeft = c.spotsLeft - 1)
        val bid = ++nextId; booked[bid] = id; paidTotal[bid] = c.price
        notifications.add(0, AppNotification(++nextId, "BOOKING", "Booking confirmed", "You're booked for ${c.name}. Reference RF-E%06d.".format(bid), "bookings", ago(0), false))
        return TicketResponse(bid, "RF-E%06d".format(bid), true, null, c.price - useCredit, useCredit, "You're booked. We've emailed your confirmation.")
    }
    override suspend fun ticketQuote(id: Int): TicketQuote {
        pause()
        val c = classes.first { it.id == id }
        val use = minOf(credit, c.price)
        return TicketQuote(c.price, credit, use, c.price - use, true, null)
    }
    override suspend fun joinWaitlist(id: Int): Response<Unit> { pause(); waiting.add(id); return ok() }
    override suspend fun cancelTicket(id: Int, body: CancelRequestBody): ActionResult {
        pause()
        val cid = booked.remove(id) ?: fail(409, "Booking not found.")
        val c = classes.first { it.id == cid }; classes[classes.indexOf(c)] = c.copy(spotsLeft = c.spotsLeft + 1)
        val paid = paidTotal.remove(id) ?: 0.0
        if (body.choice == "CREDIT") credit += paid
        return ActionResult(if (body.choice == "CREDIT") "Cancelled. ${formatDemoRand(paid)} has been added to your account credit." else "Cancelled. We'll refund ${formatDemoRand(paid)} to your original payment method.", "CANCELLED")
    }
    override suspend fun chooseAfterChange(id: Int, body: ChoiceBody): ActionResult { pause(); return ActionResult("Done.", "BOOKED") }
    override suspend fun credits(): Credits {
        pause()
        return Credits(credit, if (credit > 0) listOf(CreditItem(1, credit, credit, ago(10), Instant.now().plus(Duration.ofDays(365)).toString(), "Cancelled at least 48 hours before the event")) else emptyList())
    }
    private fun formatDemoRand(v: Double) = if (v % 1.0 == 0.0) "R${v.toInt()}" else "R${"%.2f".format(java.util.Locale.US, v)}"
    override suspend fun adminEventRequests(): List<AdminEventRequest> { pause(); return demoRequests.toList() }
    override suspend fun adminResolveRequest(id: Int, body: ResolveRequestBody): Response<Unit> { pause(); demoRequests.removeAll { it.bookingId == id }; return ok() }
    override suspend fun adminRefunds(): List<AdminRefund> { pause(); return demoRefunds.toList() }
    override suspend fun adminMarkRefunded(id: Int): Response<Unit> { pause(); demoRefunds.removeAll { it.bookingId == id }; return ok() }
    override suspend fun adminSendLinks(id: Int): Response<Unit> { pause(); return ok() }
    override suspend fun book(id: Int): Booking {
        pause()
        val c = classes.first { it.id == id }
        if (booked.containsValue(id)) fail(409, "You have already booked this class.")
        if (c.spotsLeft == 0) fail(409, "This class is full.")
        classes[classes.indexOf(c)] = c.copy(spotsLeft = c.spotsLeft - 1)
        val bid = ++nextId; booked[bid] = id
        return bookingDto(bid, id)
    }
    override suspend fun cancelBooking(id: Int): Response<Unit> {
        pause()
        val cid = booked.remove(id) ?: return Response.error(409, "{\"error\":\"Booking not found.\"}".toResponseBody("application/json".toMediaType()))
        val c = classes.first { it.id == cid }; classes[classes.indexOf(c)] = c.copy(spotsLeft = c.spotsLeft + 1)
        return ok()
    }

    // ---- notifications
    override suspend fun notifications(afterId: Int?, unreadOnly: Boolean): List<AppNotification> {
        pause(); return notifications.filter { (afterId == null || it.id > afterId) && (!unreadOnly || !it.read) }.sortedByDescending { it.id }
    }
    override suspend fun unreadCount(): UnreadCount { pause(); return UnreadCount(notifications.count { !it.read }) }
    override suspend fun markRead(body: MarkReadRequest): Response<Unit> {
        pause()
        notifications.replaceAll { if (body.ids == null || it.id in body.ids) it.copy(read = true) else it }
        return ok()
    }

    // ---- journal
    override suspend fun journal(): List<JournalEntry> { pause(); return journal.sortedByDescending { it.id } }
    override suspend fun addJournal(body: JournalRequest): JournalEntry {
        pause(); val e = JournalEntry(++nextId, body.kind, body.mood ?: "", body.text ?: "", ago(0)); journal.add(e); return e
    }
    override suspend fun deleteJournal(id: Int): Response<Unit> { pause(); journal.removeAll { it.id == id }; return ok() }

    // ---- error reporting
    override suspend fun reportError(body: ErrorReportBody): Response<Unit> = ok()

    // ---- admin
    override suspend fun adminSummary(): AdminSummary { pause(); return AdminSummary(128, 74, classes.size, booked.size + 41, 74 * 180.0, errors.count { it.status == "NEW" }) }
    override suspend fun adminCreateLesson(body: LessonUpsert): Int { pause(); return ++nextId }
    override suspend fun adminDeleteLesson(id: Int): Response<Unit> { pause(); return ok() }
    override suspend fun adminClasses(): List<ClassItem> { pause(); return classes.map(::classDto) }

    private val demoPeople = listOf("Thandi Mokoena", "Sam Naidoo", "Priya Pillay", "Lerato Dlamini", "Megan Botha", "Zanele Khumalo", "Aisha Patel", "Nomsa Sithole", "Chloe Jacobs", "Ruan Venter")
    private fun attendeesOf(c: ClassItem): List<AdminAttendee> =
        (0 until (c.capacity - c.spotsLeft).coerceAtMost(demoPeople.size)).map { i ->
            val n = demoPeople[i]
            AdminAttendee(c.id * 100 + i, "p$i", n, n.lowercase().substringBefore(' ') + "@example.com", ago(60L * (i + 2)), "BOOKED")
        }
    override suspend fun adminAttendees(id: Int): List<AdminAttendee> { pause(); return attendeesOf(classes.first { it.id == id }) }
    override suspend fun adminBookings(): List<AdminBooking> {
        pause()
        return classes.sortedBy { it.startTime }.flatMap { c ->
            attendeesOf(c).map { AdminBooking(it.bookingId, c.id, c.name, c.startTime, c.location, it.userId, it.fullName, it.email, it.bookedAt, it.status) }
        }
    }
    override suspend fun adminSubscriptions(): List<AdminSubscription> {
        pause()
        return listOf(
            AdminSubscription(11, "u1", "Alex Demo", "alex@rhythmandflow.test", "Flow", 99.0, "ACTIVE", ago(60L * 24 * 12), Instant.now().plus(Duration.ofDays(18)).toString(), true),
            AdminSubscription(12, "u2", "Thandi Mokoena", "thandi@example.com", "Rhythm", 199.0, "ACTIVE", ago(60L * 24 * 5), Instant.now().plus(Duration.ofDays(25)).toString(), true),
        )
    }
    private val videoRefs = mutableMapOf<Int, String>()
    override suspend fun adminLessons(): List<AdminLesson> {
        pause()
        return lessonsData.map {
            AdminLesson(it.id, it.prog, it.progName, it.title, it.desc, it.cat, "All levels", it.secs, "Remote",
                videoRefs[it.id] ?: "https://storage.googleapis.com/exoplayer-test-media-1/mp4/android-screens-10s.mp4", it.preview)
        }
    }
    override suspend fun adminUpdateLesson(id: Int, body: LessonUpsert): Response<Unit> {
        pause()
        val i = lessonsData.indexOfFirst { it.id == id }
        if (i < 0) return Response.error(404, "{}".toResponseBody("application/json".toMediaType()))
        val progName = lessonsData.firstOrNull { it.prog == body.programmeId }?.progName ?: fail(400, "Unknown programme.")
        lessonsData[i] = lessonsData[i].copy(prog = body.programmeId, progName = progName, title = body.title, desc = body.description.orEmpty(),
            cat = body.category, secs = body.durationSeconds, preview = body.isPreview)
        videoRefs[id] = body.videoReference
        return ok()
    }
    override suspend fun adminUpdateClass(id: Int, body: ClassUpsert): Response<Unit> {
        pause()
        val i = classes.indexOfFirst { it.id == id }
        if (i < 0) return Response.error(404, "{}".toResponseBody("application/json".toMediaType()))
        val c = classes[i]
        val taken = c.capacity - c.spotsLeft
        if (Instant.parse(body.endTime) <= Instant.parse(body.startTime)) fail(400, "End time must be after start time.")
        if (body.capacity < taken) fail(400, "$taken people are already booked, so the capacity can't be lower than $taken.")
        classes[i] = c.copy(name = body.name, description = body.description.orEmpty(), coachName = body.coachName, location = body.location,
            startTime = body.startTime, endTime = body.endTime, capacity = body.capacity, spotsLeft = body.capacity - taken,
            kind = body.kind ?: "CLASS", format = body.format ?: "IN_PERSON", price = body.price, posterUrl = body.posterUrl,
            salesOpenAt = body.salesOpenAt, memberEarlyHours = body.memberEarlyHours, waitlistEnabled = body.waitlistEnabled,
            joinPlatform = body.joinPlatform, joinLink = body.joinLink, practicalNote = body.practicalNote, salesCloseHours = body.salesCloseHours)
        return ok()
    }
    override suspend fun adminCreateClass(body: ClassUpsert): Int { pause(); return ++nextId }
    override suspend fun adminCancelClass(id: Int): Response<Unit> { pause(); return ok() }
    private val hiddenPlans = mutableSetOf<Int>()
    private fun splitFeatures(s: String?) = s.orEmpty().split('|', '\n').map { it.trim() }.filter { it.isNotEmpty() }
    private fun checkPlan(id: Int?, b: PlanUpsert) {
        if (b.name.isBlank()) fail(400, "Enter a plan name.")
        if (b.price < 1) fail(400, "The price must be at least R1.")
        if (b.annualPrice != null && b.annualPrice < 5) fail(400, "The yearly price must be at least R5, or left empty for a monthly-only plan.")
        if (b.launchPrice != null && b.launchPrice < 5) fail(400, "The launch price must be at least R5, or left empty for no launch offer.")
        if (b.launchPrice != null && b.launchPrice >= b.price) fail(400, "The launch price must be lower than the normal monthly price.")
        if (b.launchPrice != null && b.launchEndsAt.isNullOrBlank()) fail(400, "Choose the day the launch price ends.")
        if (plans.any { it.id != id && it.name.equals(b.name.trim(), true) }) fail(400, "A plan with that name already exists.")
    }
    override suspend fun adminPlans(): List<AdminPlan> {
        pause()
        return plans.sortedBy { it.price }.map { AdminPlan(it.id, it.name, it.description, it.price, it.billingFrequency, it.tier, it.features, if (it.id in hiddenPlans) "INACTIVE" else "ACTIVE", it.annualPrice, it.launchPrice, it.launchEndsAt, it.trialDays) }
    }
    override suspend fun adminCreatePlan(body: PlanUpsert): Int {
        pause(); checkPlan(null, body)
        val id = ++nextId
        plans.add(Plan(id, body.name.trim(), body.description.orEmpty(), body.price, "MONTHLY", body.tier, splitFeatures(body.features), body.annualPrice, body.launchPrice, body.launchEndsAt, body.trialDays))
        if (body.status == "INACTIVE") hiddenPlans.add(id)
        return id
    }
    override suspend fun adminUpdatePlan(id: Int, body: PlanUpsert): Response<Unit> {
        pause(); checkPlan(id, body)
        val i = plans.indexOfFirst { it.id == id }
        if (i >= 0) plans[i] = plans[i].copy(name = body.name.trim(), description = body.description.orEmpty(), price = body.price, tier = body.tier, features = splitFeatures(body.features), annualPrice = body.annualPrice, launchPrice = body.launchPrice, launchEndsAt = body.launchEndsAt, trialDays = body.trialDays)
        if (body.status == "INACTIVE") hiddenPlans.add(id) else hiddenPlans.remove(id)
        return ok()
    }
    override suspend fun adminErrors(status: String): List<ErrorLogItem> { pause(); return errors.filter { status == "ALL" || it.status == status } }
    override suspend fun adminResolveError(id: Int): Response<Unit> { pause(); errors.replaceAll { if (it.id == id) it.copy(status = "RESOLVED") else it }; return ok() }
    override suspend fun adminResolveAllErrors(): Response<Unit> { pause(); errors.replaceAll { it.copy(status = "RESOLVED") }; return ok() }
}
