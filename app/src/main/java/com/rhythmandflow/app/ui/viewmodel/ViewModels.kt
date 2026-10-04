package com.rhythmandflow.app.ui.viewmodel

import androidx.compose.runtime.Composable
import androidx.compose.ui.platform.LocalContext
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.lifecycle.viewmodel.initializer
import androidx.lifecycle.viewmodel.viewModelFactory
import com.rhythmandflow.app.AppContainer
import com.rhythmandflow.app.RhythmApplication
import com.rhythmandflow.app.data.*
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

/** Generic "load something" state used by most screens. */
data class Load<T>(val loading: Boolean = true, val error: String? = null, val data: T? = null)

@Composable
fun container(): AppContainer = (LocalContext.current.applicationContext as RhythmApplication).container

@Composable
inline fun <reified VM : ViewModel> appViewModel(key: String? = null, crossinline create: (AppContainer) -> VM): VM {
    val c = container()
    return viewModel(key = key, factory = viewModelFactory { initializer { create(c) } })
}

// ============================================================ Session
sealed interface SessionState {
    data object Loading : SessionState
    data object SignedOut : SessionState
    data class SignedIn(val user: User) : SessionState
}

class SessionViewModel(private val c: AppContainer) : ViewModel() {
    private val repo = c.repository
    private val _state = MutableStateFlow<SessionState>(SessionState.Loading)
    val state: StateFlow<SessionState> = _state.asStateFlow()

    private val _subs = MutableStateFlow<List<Subscription>>(emptyList())
    val subscriptions: StateFlow<List<Subscription>> = _subs.asStateFlow()

    init {
        viewModelScope.launch { repo.unauthorized.collect { finishSignOut() } }
        viewModelScope.launch {
            if (!repo.hasToken) { _state.value = SessionState.SignedOut; return@launch }
            when (val r = repo.me()) {
                is Outcome.Ok -> { _state.value = SessionState.SignedIn(r.value); onSignedIn() }
                // A network failure should not log the user out; only an auth failure does (handled via `unauthorized`).
                is Outcome.Fail -> _state.value = SessionState.SignedOut
            }
        }
    }

    suspend fun login(identifier: String, password: String): String? =
        when (val r = repo.login(identifier.trim(), password)) {
            is Outcome.Ok -> { _state.value = SessionState.SignedIn(r.value.user); onSignedIn(); null }
            is Outcome.Fail -> r.message
        }

    suspend fun register(name: String, username: String, email: String, password: String): String? =
        when (val r = repo.register(name.trim(), username.trim(), email.trim(), password)) {
            is Outcome.Ok -> { _state.value = SessionState.SignedIn(r.value.user); onSignedIn(); null }
            is Outcome.Fail -> r.message
        }

    suspend fun updateProfile(name: String, email: String, about: String): String? =
        when (val r = repo.updateProfile(name.trim(), email.trim(), about.trim())) {
            is Outcome.Ok -> { _state.value = SessionState.SignedIn(r.value); null }
            is Outcome.Fail -> r.message
        }

    suspend fun changePassword(current: String, new: String): String? =
        when (val r = repo.changePassword(current, new)) {
            is Outcome.Ok -> { _state.value = SessionState.SignedIn(r.value.user); null }
            is Outcome.Fail -> r.message
        }

    /** The person's data as JSON text, or an error message. */
    suspend fun exportData(): Pair<String?, String?> =
        when (val r = repo.exportData()) {
            is Outcome.Ok -> r.value to null
            is Outcome.Fail -> null to r.message
        }

    /** Deletes the account; on success the person is signed out. Returns an error message or null. */
    suspend fun deleteAccount(password: String): String? =
        when (val r = repo.deleteAccount(password)) {
            is Outcome.Ok -> { signOut(); null }
            is Outcome.Fail -> r.message
        }

    /** Things to start once someone is signed in: their plans, the notification sync, and push for this phone. */
    private fun onSignedIn() {
        refreshSubscriptions()
        com.rhythmandflow.app.notifications.NotificationSync.start(c.app)
        com.rhythmandflow.app.notifications.PushRegistration.sync(c.app)
    }

    /** Tells the server first (while the login still works) so this phone stops receiving this person's pushes, then signs out. */
    fun signOut() {
        viewModelScope.launch {
            kotlinx.coroutines.withTimeoutOrNull(2500) { com.rhythmandflow.app.notifications.PushRegistration.unregister(c.app) }
            finishSignOut()
        }
    }

    private fun finishSignOut() {
        repo.signOut()
        com.rhythmandflow.app.notifications.NotificationSync.stop(c.app)
        com.rhythmandflow.app.notifications.ReminderScheduler.cancelAll(c.app, c.localPrefs)
        c.localPrefs.putInt("last_notified_id", -1)
        _subs.value = emptyList()
        _state.value = SessionState.SignedOut
    }

    fun refreshSubscriptions() {
        viewModelScope.launch {
            val r = repo.subscriptions()
            if (r is Outcome.Ok) _subs.value = r.value
        }
    }

    val hasActiveSubscription: Boolean get() = _subs.value.any { it.grantsAccess }
}

// ============================================================ Move / lessons
class MoveViewModel(private val c: AppContainer) : ViewModel() {
    private val repo = c.repository
    private val _lessons = MutableStateFlow(Load<List<Lesson>>())
    val lessons: StateFlow<Load<List<Lesson>>> = _lessons.asStateFlow()
    var category = MutableStateFlow("All"); private set
    var query = MutableStateFlow(""); private set
    private var job: Job? = null
    private val _programmes = MutableStateFlow<List<Programme>>(emptyList())
    val programmes: StateFlow<List<Programme>> = _programmes.asStateFlow()

    init { load() }

    fun setCategory(c: String) { category.value = c; load() }
    fun setQuery(q: String) { query.value = q; load(debounce = true) }

    fun load(debounce: Boolean = false) {
        job?.cancel()
        job = viewModelScope.launch {
            if (debounce) delay(350)
            else (repo.programmes() as? Outcome.Ok)?.let { _programmes.value = it.value }
            _lessons.update { it.copy(loading = it.data == null, error = null) }
            when (val r = repo.lessons(category.value, query.value)) {
                is Outcome.Ok -> _lessons.value = Load(false, null, r.value)
                is Outcome.Fail -> _lessons.value = Load(false, r.message, _lessons.value.data)
            }
        }
    }
}

class LessonViewModel(private val c: AppContainer, val lessonId: Int) : ViewModel() {
    private val repo = c.repository
    private val _lesson = MutableStateFlow(Load<Lesson>())
    val lesson: StateFlow<Load<Lesson>> = _lesson.asStateFlow()
    private val _related = MutableStateFlow<List<Lesson>>(emptyList())
    val related: StateFlow<List<Lesson>> = _related.asStateFlow()

    init { load() }

    fun load() {
        viewModelScope.launch {
            _lesson.value = Load(true)
            when (val r = repo.lesson(lessonId)) {
                is Outcome.Ok -> {
                    _lesson.value = Load(false, null, r.value)
                    val all = repo.lessons(r.value.category)
                    if (all is Outcome.Ok) _related.value = all.value.filter { it.id != lessonId }.take(5)
                }
                is Outcome.Fail -> _lesson.value = Load(false, r.message)
            }
        }
    }
}

data class PlayerState(
    val loading: Boolean = true,
    val error: String? = null,
    val url: String? = null,
    val resumeMs: Long = 0,
    val locked: Boolean = false,
)

class PlayerViewModel(private val c: AppContainer, private val lessonId: Int) : ViewModel() {
    private val repo = c.repository
    private val _state = MutableStateFlow(PlayerState())
    val state: StateFlow<PlayerState> = _state.asStateFlow()
    private var lastSent = -1

    init { load() }

    fun load() {
        viewModelScope.launch {
            _state.value = PlayerState()
            when (val r = repo.playback(lessonId)) {
                is Outcome.Ok -> _state.value = PlayerState(false, null, r.value.url, r.value.resumeSeconds * 1000L)
                is Outcome.Fail -> _state.value = PlayerState(false, r.message, locked = r.code == 403)
            }
        }
    }

    /** Reports watch time (seconds) so the server can calculate completion percentage. */
    fun report(seconds: Int, force: Boolean = false) {
        if (seconds <= 0 || (!force && seconds == lastSent)) return
        lastSent = seconds
        // viewModelScope may be cancelled when leaving the screen, so fire from an independent scope.
        kotlinx.coroutines.GlobalScope.launch { repo.updateProgress(lessonId, seconds) }
    }
}

// ============================================================ Plans & payment
data class PlansState(
    val loading: Boolean = true,
    val error: String? = null,
    val plans: List<Plan> = emptyList(),
    val checkingOut: Boolean = false,
)

class PlansViewModel(private val c: AppContainer) : ViewModel() {
    private val repo = c.repository
    private val _state = MutableStateFlow(PlansState())
    val state: StateFlow<PlansState> = _state.asStateFlow()
    private val _subs = MutableStateFlow<List<Subscription>>(emptyList())
    val subscriptions: StateFlow<List<Subscription>> = _subs.asStateFlow()

    init { load() }

    fun load() {
        viewModelScope.launch {
            _state.update { it.copy(loading = true, error = null) }
            val p = repo.plans()
            val s = repo.subscriptions()
            if (s is Outcome.Ok) _subs.value = s.value
            _state.value = when (p) {
                is Outcome.Ok -> PlansState(false, null, p.value)
                is Outcome.Fail -> PlansState(false, p.message)
            }
        }
    }

    fun refreshSubs() {
        viewModelScope.launch { (repo.subscriptions() as? Outcome.Ok)?.let { _subs.value = it.value } }
    }

    /** Creates a pending subscription and returns the PayFast checkout link (or an error message). */
    suspend fun checkout(planId: Int): Outcome<CheckoutResponse> {
        _state.update { it.copy(checkingOut = true) }
        val r = repo.checkout(planId)
        _state.update { it.copy(checkingOut = false) }
        return r
    }

    suspend fun cancel(subId: Int): Outcome<CancelResult> =
        repo.cancelSubscription(subId).also { if (it is Outcome.Ok) refreshSubs() }

    suspend fun simulatePayment(subId: Int): String? = when (val r = repo.simulatePayment(subId)) {
        is Outcome.Ok -> null
        is Outcome.Fail -> r.message
    }

    suspend fun subscription(subId: Int): Subscription? = (repo.subscriptions() as? Outcome.Ok)?.value?.firstOrNull { it.id == subId }
}

// ============================================================ Classes & bookings
data class ClassesState(
    val loading: Boolean = true,
    val error: String? = null,
    val classes: List<ClassItem> = emptyList(),
    val bookings: List<Booking> = emptyList(),
)

class ClassesViewModel(private val c: AppContainer) : ViewModel() {
    private val repo = c.repository
    private val _state = MutableStateFlow(ClassesState())
    val state: StateFlow<ClassesState> = _state.asStateFlow()

    init { load() }

    fun load() {
        viewModelScope.launch {
            _state.update { it.copy(loading = it.classes.isEmpty(), error = null) }
            val cl = repo.classes()
            val bk = repo.bookings()
            _state.value = when {
                cl is Outcome.Fail -> ClassesState(false, cl.message)
                else -> {
                    val bookings = (bk as? Outcome.Ok)?.value
                    if (bookings != null) com.rhythmandflow.app.notifications.ReminderScheduler.sync(c.app, c.localPrefs, bookings)
                    ClassesState(false, null, (cl as Outcome.Ok).value, bookings ?: emptyList())
                }
            }
        }
    }

    /** Returns an error message, or null on success. */
    suspend fun book(classId: Int): String? = when (val r = repo.book(classId)) {
        is Outcome.Ok -> { load(); null }
        is Outcome.Fail -> { load(); r.message }
    }

    suspend fun cancel(bookingId: Int): String? = when (val r = repo.cancelBooking(bookingId)) {
        is Outcome.Ok -> { load(); null }
        is Outcome.Fail -> { load(); r.message }
    }
}

// ============================================================ Home / journal / profile
data class HomeState(
    val summary: ProgressSummary? = null, val nextBooking: Booking? = null, val loading: Boolean = true, val unread: Int = 0,
    /** A practice the person started and has not finished, so Home can offer to carry on. */
    val resume: Lesson? = null,
)

class HomeViewModel(private val c: AppContainer) : ViewModel() {
    private val repo = c.repository
    private val _state = MutableStateFlow(HomeState())
    val state: StateFlow<HomeState> = _state.asStateFlow()

    init { load() }

    fun load() {
        viewModelScope.launch {
            val s = (repo.progressSummary() as? Outcome.Ok)?.value
            val allBookings = (repo.bookings() as? Outcome.Ok)?.value
            if (allBookings != null) com.rhythmandflow.app.notifications.ReminderScheduler.sync(c.app, c.localPrefs, allBookings)
            val b = allBookings?.firstOrNull()
            val unread = (repo.unreadCount() as? Outcome.Ok)?.value?.count ?: 0
            val resume = (repo.lessons() as? Outcome.Ok)?.value
                ?.filter { !it.locked && it.watchTimeSeconds > 0 && it.completionPercentage in 1.0..94.9 }
                ?.maxByOrNull { it.completionPercentage }
            _state.value = HomeState(s, b, false, unread, resume)
        }
    }

    suspend fun checkIn(mood: String): String? = when (val r = repo.addJournal("CHECKIN", mood, null)) {
        is Outcome.Ok -> null
        is Outcome.Fail -> r.message
    }
}

class JournalViewModel(private val c: AppContainer) : ViewModel() {
    private val repo = c.repository
    private val _entries = MutableStateFlow(Load<List<JournalEntry>>())
    val entries: StateFlow<Load<List<JournalEntry>>> = _entries.asStateFlow()

    init { load() }

    fun load() {
        viewModelScope.launch {
            when (val r = repo.journal()) {
                is Outcome.Ok -> _entries.value = Load(false, null, r.value)
                is Outcome.Fail -> _entries.value = Load(false, r.message, _entries.value.data)
            }
        }
    }

    suspend fun add(kind: String, mood: String?, text: String?): String? = when (val r = repo.addJournal(kind, mood, text)) {
        is Outcome.Ok -> { load(); null }
        is Outcome.Fail -> r.message
    }

    suspend fun delete(id: Int): String? = when (val r = repo.deleteJournal(id)) {
        is Outcome.Ok -> { load(); null }
        is Outcome.Fail -> r.message
    }
}

// ============================================================ Admin
data class AdminState(
    val loading: Boolean = true,
    val error: String? = null,
    val summary: AdminSummary? = null,
    val classes: List<ClassItem> = emptyList(),
    val lessons: List<AdminLesson> = emptyList(),
    val plans: List<AdminPlan> = emptyList(),
)

class AdminViewModel(private val c: AppContainer) : ViewModel() {
    private val repo = c.repository
    private val _state = MutableStateFlow(AdminState())
    val state: StateFlow<AdminState> = _state.asStateFlow()

    init { load() }

    fun load() {
        viewModelScope.launch {
            val s = repo.adminSummary()
            val cl = repo.adminClasses()
            val ls = repo.adminLessons()
            val pl = repo.adminPlans()
            _state.value = AdminState(
                loading = false,
                error = (s as? Outcome.Fail)?.message,
                summary = (s as? Outcome.Ok)?.value,
                classes = (cl as? Outcome.Ok)?.value ?: emptyList(),
                lessons = (ls as? Outcome.Ok)?.value ?: emptyList(),
                plans = (pl as? Outcome.Ok)?.value ?: emptyList(),
            )
        }
    }

    private suspend fun <T> after(r: Outcome<T>): String? = when (r) {
        is Outcome.Ok -> { load(); null }
        is Outcome.Fail -> r.message
    }

    suspend fun createClass(c: ClassUpsert) = after(repo.adminCreateClass(c))
    suspend fun cancelClass(id: Int) = after(repo.adminCancelClass(id))
    suspend fun createLesson(l: LessonUpsert) = after(repo.adminCreateLesson(l))
    suspend fun deleteLesson(id: Int) = after(repo.adminDeleteLesson(id))
    suspend fun updatePlan(id: Int, p: PlanUpsert) = after(repo.adminUpdatePlan(id, p))
    suspend fun createPlan(p: PlanUpsert) = after(repo.adminCreatePlan(p))
    suspend fun updateClass(id: Int, c: ClassUpsert) = after(repo.adminUpdateClass(id, c))
    suspend fun updateLesson(id: Int, l: LessonUpsert) = after(repo.adminUpdateLesson(id, l))
}

// ============================================================ Notifications
class NotificationsViewModel(private val c: AppContainer) : ViewModel() {
    private val repo = c.repository
    private val _items = MutableStateFlow(Load<List<AppNotification>>())
    val items: StateFlow<Load<List<AppNotification>>> = _items.asStateFlow()

    init { load() }

    fun load() {
        viewModelScope.launch {
            when (val r = repo.notifications()) {
                is Outcome.Ok -> _items.value = Load(false, null, r.value)
                is Outcome.Fail -> _items.value = Load(false, r.message, _items.value.data)
            }
        }
    }

    fun markRead(id: Int) {
        // show it as read straight away, then tell the server
        _items.update { s -> s.copy(data = s.data?.map { if (it.id == id) it.copy(read = true) else it }) }
        viewModelScope.launch { repo.markRead(listOf(id)) }
    }

    fun markAllRead() {
        _items.update { s -> s.copy(data = s.data?.map { it.copy(read = true) }) }
        viewModelScope.launch { repo.markRead(null) }
    }
}
/** One fitness programme: what it is, who it is included for, and its lessons (FR-03, FR-04). */
data class ProgrammeState(
    val loading: Boolean = true, val error: String? = null,
    val programme: Programme? = null, val lessons: List<Lesson> = emptyList(),
    /** The cheapest plan that includes this programme, e.g. "Rhythm". */
    val planName: String? = null,
)

class ProgrammeViewModel(private val c: AppContainer, val programmeId: Int) : ViewModel() {
    private val repo = c.repository
    private val _state = MutableStateFlow(ProgrammeState())
    val state: StateFlow<ProgrammeState> = _state.asStateFlow()

    init { load() }

    fun load() {
        viewModelScope.launch {
            val programmes = repo.programmes()
            val lessons = repo.lessons(programmeId = programmeId)
            val plans = (repo.plans() as? Outcome.Ok)?.value.orEmpty()
            val programme = (programmes as? Outcome.Ok)?.value?.firstOrNull { it.id == programmeId }
            _state.value = when {
                programme == null -> ProgrammeState(false, (programmes as? Outcome.Fail)?.message ?: "We couldn't find that programme.")
                else -> ProgrammeState(
                    loading = false, programme = programme,
                    lessons = (lessons as? Outcome.Ok)?.value.orEmpty(),
                    planName = plans.filter { it.tier >= programme.minTier }.minByOrNull { it.tier }?.name,
                )
            }
        }
    }
}

/** The person's workout progress (FR-13): totals, practices in progress and finished ones. */
data class ProgressScreenState(
    val loading: Boolean = true, val error: String? = null,
    val summary: ProgressSummary? = null, val lessons: List<Lesson> = emptyList(),
)

class ProgressViewModel(private val c: AppContainer) : ViewModel() {
    private val repo = c.repository
    private val _state = MutableStateFlow(ProgressScreenState())
    val state: StateFlow<ProgressScreenState> = _state.asStateFlow()

    init { load() }

    fun load() {
        viewModelScope.launch {
            val summary = repo.progressSummary()
            val lessons = repo.lessons()
            _state.value = ProgressScreenState(
                loading = false,
                error = (lessons as? Outcome.Fail)?.message,
                summary = (summary as? Outcome.Ok)?.value,
                lessons = (lessons as? Outcome.Ok)?.value.orEmpty(),
            )
        }
    }
}

class AdminProgrammesViewModel(private val c: AppContainer) : ViewModel() {
    private val repo = c.repository
    private val _items = MutableStateFlow(Load<List<AdminProgramme>>())
    val items: StateFlow<Load<List<AdminProgramme>>> = _items.asStateFlow()

    init { load() }

    fun load() {
        viewModelScope.launch {
            when (val r = repo.adminProgrammes()) {
                is Outcome.Ok -> _items.value = Load(false, null, r.value)
                is Outcome.Fail -> _items.value = Load(false, r.message, _items.value.data)
            }
        }
    }

    suspend fun create(p: ProgrammeUpsert): String? = when (val r = repo.adminCreateProgramme(p)) {
        is Outcome.Ok -> { load(); null }
        is Outcome.Fail -> r.message
    }

    suspend fun update(id: Int, p: ProgrammeUpsert): String? = when (val r = repo.adminUpdateProgramme(id, p)) {
        is Outcome.Ok -> { load(); null }
        is Outcome.Fail -> r.message
    }
}

class PaymentsViewModel(private val c: AppContainer) : ViewModel() {
    private val repo = c.repository
    private val _items = MutableStateFlow(Load<List<PaymentItem>>())
    val items: StateFlow<Load<List<PaymentItem>>> = _items.asStateFlow()

    init { load() }

    fun load() {
        viewModelScope.launch {
            when (val r = repo.payments()) {
                is Outcome.Ok -> _items.value = Load(false, null, r.value)
                is Outcome.Fail -> _items.value = Load(false, r.message, _items.value.data)
            }
        }
    }
}

class AdminUsersViewModel(private val c: AppContainer) : ViewModel() {
    private val repo = c.repository
    private val _items = MutableStateFlow(Load<List<AdminUser>>())
    val items: StateFlow<Load<List<AdminUser>>> = _items.asStateFlow()
    val query = MutableStateFlow("")
    private var searchJob: Job? = null

    init { load() }

    /** Waits a moment after the last keystroke so the server is not asked on every letter. */
    fun search(q: String) {
        query.value = q
        searchJob?.cancel()
        searchJob = viewModelScope.launch { delay(350); load() }
    }

    fun load() {
        viewModelScope.launch {
            when (val r = repo.adminUsers(query.value)) {
                is Outcome.Ok -> _items.value = Load(false, null, r.value)
                is Outcome.Fail -> _items.value = Load(false, r.message, _items.value.data)
            }
        }
    }

    suspend fun setStatus(id: String, status: String): String? = when (val r = repo.adminSetUserStatus(id, status)) {
        is Outcome.Ok -> { load(); null }
        is Outcome.Fail -> r.message
    }
}

/** Every upcoming booking across all classes (administrators). */
class AdminBookingsViewModel(private val c: AppContainer) : ViewModel() {
    private val repo = c.repository
    private val _items = MutableStateFlow(Load<List<AdminBooking>>())
    val items: StateFlow<Load<List<AdminBooking>>> = _items.asStateFlow()

    init { load() }

    fun load() {
        viewModelScope.launch {
            when (val r = repo.adminBookings()) {
                is Outcome.Ok -> _items.value = Load(false, null, r.value)
                is Outcome.Fail -> _items.value = Load(false, r.message, _items.value.data)
            }
        }
    }
}

/** Who holds an active plan (administrators). */
class AdminSubscriptionsViewModel(private val c: AppContainer) : ViewModel() {
    private val repo = c.repository
    private val _items = MutableStateFlow(Load<List<AdminSubscription>>())
    val items: StateFlow<Load<List<AdminSubscription>>> = _items.asStateFlow()

    init { load() }

    fun load() {
        viewModelScope.launch {
            when (val r = repo.adminSubscriptions()) {
                is Outcome.Ok -> _items.value = Load(false, null, r.value)
                is Outcome.Fail -> _items.value = Load(false, r.message, _items.value.data)
            }
        }
    }
}

data class AttendeesState(val cls: ClassItem? = null, val loading: Boolean = true, val error: String? = null, val people: List<AdminAttendee> = emptyList())

/** Who is booked into one class (administrators). */
class AdminAttendeesViewModel(private val c: AppContainer, val classId: Int) : ViewModel() {
    private val repo = c.repository
    private val _state = MutableStateFlow(AttendeesState())
    val state: StateFlow<AttendeesState> = _state.asStateFlow()

    init { load() }

    fun load() {
        viewModelScope.launch {
            val people = repo.adminAttendees(classId)
            val cls = (repo.adminClasses() as? Outcome.Ok)?.value?.firstOrNull { it.id == classId }
            _state.value = when (people) {
                is Outcome.Ok -> AttendeesState(cls, false, null, people.value)
                is Outcome.Fail -> AttendeesState(cls, false, people.message, _state.value.people)
            }
        }
    }
}

class AdminErrorsViewModel(private val c: AppContainer) : ViewModel() {
    private val repo = c.repository
    private val _items = MutableStateFlow(Load<List<ErrorLogItem>>())
    val items: StateFlow<Load<List<ErrorLogItem>>> = _items.asStateFlow()
    val status = MutableStateFlow("NEW")

    init { load() }

    fun setStatus(s: String) { status.value = s; load() }

    fun load() {
        viewModelScope.launch {
            when (val r = repo.adminErrors(status.value)) {
                is Outcome.Ok -> _items.value = Load(false, null, r.value)
                is Outcome.Fail -> _items.value = Load(false, r.message, _items.value.data)
            }
        }
    }

    suspend fun resolve(id: Int): String? = when (val r = repo.adminResolveError(id)) {
        is Outcome.Ok -> { load(); null }
        is Outcome.Fail -> r.message
    }

    suspend fun resolveAll(): String? = when (val r = repo.adminResolveAllErrors()) {
        is Outcome.Ok -> { load(); null }
        is Outcome.Fail -> r.message
    }
}