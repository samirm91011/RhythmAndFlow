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
        viewModelScope.launch { repo.unauthorized.collect { signOut() } }
        viewModelScope.launch {
            if (!repo.hasToken) { _state.value = SessionState.SignedOut; return@launch }
            when (val r = repo.me()) {
                is Outcome.Ok -> { _state.value = SessionState.SignedIn(r.value); refreshSubscriptions(); com.rhythmandflow.app.notifications.NotificationSync.start(c.app) }
                // A network failure should not log the user out; only an auth failure does (handled via `unauthorized`).
                is Outcome.Fail -> _state.value = SessionState.SignedOut
            }
        }
    }

    suspend fun login(identifier: String, password: String): String? =
        when (val r = repo.login(identifier.trim(), password)) {
            is Outcome.Ok -> { _state.value = SessionState.SignedIn(r.value.user); refreshSubscriptions(); com.rhythmandflow.app.notifications.NotificationSync.start(c.app); null }
            is Outcome.Fail -> r.message
        }

    suspend fun register(name: String, username: String, email: String, password: String): String? =
        when (val r = repo.register(name.trim(), username.trim(), email.trim(), password)) {
            is Outcome.Ok -> { _state.value = SessionState.SignedIn(r.value.user); refreshSubscriptions(); null }
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

    fun signOut() {
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

    init { load() }

    fun setCategory(c: String) { category.value = c; load() }
    fun setQuery(q: String) { query.value = q; load(debounce = true) }

    fun load(debounce: Boolean = false) {
        job?.cancel()
        job = viewModelScope.launch {
            if (debounce) delay(350)
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
data class HomeState(val summary: ProgressSummary? = null, val nextBooking: Booking? = null, val loading: Boolean = true, val unread: Int = 0)

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
            _state.value = HomeState(s, b, false, unread)
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
    val lessons: List<Lesson> = emptyList(),
    val plans: List<Plan> = emptyList(),
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
            val ls = repo.lessons()
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