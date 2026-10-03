package com.rhythmandflow.app.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Email
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.Pin
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.rhythmandflow.app.data.Outcome
import com.rhythmandflow.app.ui.components.PrimaryButton
import com.rhythmandflow.app.ui.components.RfTextField
import com.rhythmandflow.app.ui.components.ScreenHeader
import com.rhythmandflow.app.ui.components.VSpace
import com.rhythmandflow.app.ui.components.vScroll
import com.rhythmandflow.app.ui.theme.Brand
import com.rhythmandflow.app.ui.viewmodel.SessionViewModel
import com.rhythmandflow.app.ui.viewmodel.container
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

private const val MIN_PASSWORD = 8

/** Two steps: ask for a code by email, then enter the code with a new password. */
@Composable
fun ForgotPasswordScreen(onBack: () -> Unit, onDone: (String) -> Unit) {
    val repo = container().repository
    val scope = rememberCoroutineScope()
    var step by rememberSaveable { mutableIntStateOf(1) }
    var email by rememberSaveable { mutableStateOf("") }
    var code by rememberSaveable { mutableStateOf("") }
    var password by rememberSaveable { mutableStateOf("") }
    var confirm by rememberSaveable { mutableStateOf("") }
    var busy by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<String?>(null) }
    var submitted by remember { mutableStateOf(false) }
    var cooldown by remember { mutableIntStateOf(0) }

    // Count down before another code can be requested (the server also limits requests).
    LaunchedEffect(cooldown) { if (cooldown > 0) { delay(1000); cooldown-- } }

    fun sendCode() {
        if (!android.util.Patterns.EMAIL_ADDRESS.matcher(email.trim()).matches()) { error = "Enter the email address you signed up with."; return }
        busy = true; error = null
        scope.launch {
            when (val r = repo.forgotPassword(email)) {
                is Outcome.Ok -> { step = 2; cooldown = 60 }
                is Outcome.Fail -> error = r.message
            }
            busy = false
        }
    }

    val codeErr = if (code.trim().length != 6) "Enter the 6-digit code from the email" else null
    val passErr = if (password.length < MIN_PASSWORD) "At least $MIN_PASSWORD characters" else null
    val matchErr = if (confirm != password) "The passwords don't match" else null

    AuthFrame(
        title = if (step == 1) "Forgot your password?" else "Check your email",
        subtitle = if (step == 1) "Enter your email and we'll send you a 6-digit code." else "We sent a code to ${email.trim()}. It expires in 15 minutes.",
        onBack = { if (step == 2) { step = 1; error = null } else onBack() },
    ) {
        if (step == 1) {
            RfTextField(email, { email = it; error = null }, "Email", Icons.Default.Email, keyboardType = KeyboardType.Email)
            error?.let { VSpace(8); Text(it, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodyMedium) }
            VSpace(16)
            PrimaryButton("Send code", onClick = ::sendCode, loading = busy)
        } else {
            RfTextField(code, { if (it.length <= 6 && it.all(Char::isDigit)) code = it }, "6-digit code", Icons.Default.Pin,
                keyboardType = KeyboardType.Number, error = if (submitted) codeErr else null)
            VSpace(8)
            RfTextField(password, { password = it }, "New password", Icons.Default.Lock, isPassword = true, error = if (submitted) passErr else null)
            VSpace(8)
            RfTextField(confirm, { confirm = it }, "Confirm new password", Icons.Default.Lock, isPassword = true, error = if (submitted) matchErr else null)
            error?.let { VSpace(8); Text(it, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodyMedium) }
            VSpace(16)
            PrimaryButton("Change password", loading = busy, onClick = {
                submitted = true
                if (codeErr == null && passErr == null && matchErr == null) {
                    busy = true; error = null
                    scope.launch {
                        when (val r = repo.resetPassword(email, code, password)) {
                            is Outcome.Ok -> onDone(r.value.message ?: "Your password has been changed. You can log in now.")
                            is Outcome.Fail -> error = r.message
                        }
                        busy = false
                    }
                }
            })
            VSpace(8)
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
                TextButton(enabled = cooldown == 0 && !busy, onClick = ::sendCode) {
                    Text(if (cooldown > 0) "Send a new code in ${cooldown}s" else "Send a new code", color = if (cooldown == 0) Brand.TealDeep else Brand.Muted)
                }
            }
        }
    }
}

/** For a signed-in customer. Other devices are signed out when it succeeds. */
@Composable
fun ChangePasswordScreen(session: SessionViewModel, onBack: () -> Unit, notify: (String) -> Unit) {
    val scope = rememberCoroutineScope()
    var current by rememberSaveable { mutableStateOf("") }
    var password by rememberSaveable { mutableStateOf("") }
    var confirm by rememberSaveable { mutableStateOf("") }
    var busy by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<String?>(null) }
    var submitted by remember { mutableStateOf(false) }

    val currentErr = if (current.isEmpty()) "Enter your current password" else null
    val passErr = when {
        password.length < MIN_PASSWORD -> "At least $MIN_PASSWORD characters"
        password == current -> "Choose a different password from the current one"
        else -> null
    }
    val matchErr = if (confirm != password) "The passwords don't match" else null

    Column(Modifier.fillMaxSize()) {
        ScreenHeader("Change password", "Choose something others can't guess.", onBack = onBack)
        Column(Modifier.vScroll().padding(horizontal = 20.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            RfTextField(current, { current = it; error = null }, "Current password", Icons.Default.Lock, isPassword = true, error = if (submitted) currentErr else null)
            RfTextField(password, { password = it }, "New password", Icons.Default.Lock, isPassword = true, error = if (submitted) passErr else null)
            RfTextField(confirm, { confirm = it }, "Confirm new password", Icons.Default.Lock, isPassword = true, error = if (submitted) matchErr else null)
            Text(
                "For your safety you'll be signed out of Rhythm & Flow on your other devices.",
                style = MaterialTheme.typography.bodySmall, color = Brand.Muted, textAlign = TextAlign.Start,
            )
            error?.let { Text(it, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodyMedium) }
            PrimaryButton("Change password", loading = busy, onClick = {
                submitted = true
                if (currentErr == null && passErr == null && matchErr == null) {
                    busy = true; error = null
                    scope.launch {
                        val err = session.changePassword(current, password)
                        busy = false
                        if (err == null) { notify("Password changed."); onBack() } else error = err
                    }
                }
            })
            VSpace(16)
        }
    }
}
