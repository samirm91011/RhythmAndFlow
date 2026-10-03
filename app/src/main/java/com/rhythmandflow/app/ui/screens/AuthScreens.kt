package com.rhythmandflow.app.ui.screens

import android.util.Patterns
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.AccountCircle
import androidx.compose.material.icons.filled.Email
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.Person
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.rhythmandflow.app.R
import com.rhythmandflow.app.ui.components.PrimaryButton
import com.rhythmandflow.app.ui.components.RfTextField
import com.rhythmandflow.app.ui.components.SecondaryButton
import com.rhythmandflow.app.ui.components.VSpace
import com.rhythmandflow.app.ui.theme.Brand
import com.rhythmandflow.app.ui.viewmodel.SessionViewModel
import kotlinx.coroutines.launch

@Composable
fun WelcomeScreen(onStart: () -> Unit) {
    Box(
        Modifier.fillMaxSize().background(Brush.verticalGradient(listOf(Brand.Teal, Brand.TealDeep))),
    ) {
        Column(
            Modifier.fillMaxSize().statusBarsPadding().navigationBarsPadding().padding(horizontal = 28.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Column(Modifier.weight(1f), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center) {
                Image(painterResource(R.drawable.rf_logo_white), "Rhythm & Flow logo", Modifier.size(210.dp))
                VSpace(20)
                Text(
                    "A kinder,\nstronger,\nhappier you",
                    style = MaterialTheme.typography.displayLarge, color = Color.White, textAlign = TextAlign.Center,
                )
                VSpace(20)
                Text(
                    "MOVE  •  NOURISH  •  REFLECT\nRECONNECT  •  GROW",
                    style = MaterialTheme.typography.labelMedium, color = Color.White.copy(alpha = 0.85f),
                    textAlign = TextAlign.Center, letterSpacing = androidx.compose.ui.unit.TextUnit(1.5f, androidx.compose.ui.unit.TextUnitType.Sp),
                )
            }
            Button(
                onClick = onStart,
                modifier = Modifier.fillMaxWidth().height(56.dp),
                shape = RoundedCornerShape(28.dp),
                colors = ButtonDefaults.buttonColors(containerColor = Color.White, contentColor = Brand.TealDeep),
            ) { Text("Get Started", style = MaterialTheme.typography.labelLarge) }
            VSpace(12)
            Text("Your personal Rhythm & Flow space", color = Color.White.copy(alpha = 0.9f), style = MaterialTheme.typography.bodyMedium)
            VSpace(24)
        }
    }
}

@Composable
fun SignInScreen(session: SessionViewModel, onSignUp: () -> Unit, onForgot: () -> Unit, notify: (String) -> Unit) {
    var identifier by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var error by remember { mutableStateOf<String?>(null) }
    var busy by remember { mutableStateOf(false) }
    val scope = rememberCoroutineScope()

    fun submit() {
        if (identifier.isBlank() || password.isBlank()) { error = "Enter your username or email and your password."; return }
        busy = true; error = null
        scope.launch { error = session.login(identifier, password); busy = false }
    }

    AuthFrame(title = "Welcome Back!", subtitle = "Log in to continue your wellness journey.") {
        RfTextField(identifier, { identifier = it; error = null }, "Username or email", Icons.Default.Person)
        VSpace(12)
        RfTextField(password, { password = it; error = null }, "Password", Icons.Default.Lock, isPassword = true)
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
            TextButton(onClick = onForgot) { Text("Forgot Password?", color = Brand.TealDeep) }
        }
        error?.let { Text(it, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodyMedium); VSpace(8) }
        PrimaryButton("Log In", onClick = ::submit, loading = busy)
        VSpace(16)
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
            Text("Don't have an account?", style = MaterialTheme.typography.bodyMedium)
            TextButton(onClick = onSignUp) { Text("Sign up", color = Brand.TealDeep) }
        }
    }
}

@Composable
fun SignUpScreen(session: SessionViewModel, onBack: () -> Unit, onLogin: () -> Unit) {
    var name by remember { mutableStateOf("") }
    var username by remember { mutableStateOf("") }
    var email by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var submitted by remember { mutableStateOf(false) }
    var serverError by remember { mutableStateOf<String?>(null) }
    var busy by remember { mutableStateOf(false) }
    val scope = rememberCoroutineScope()

    val nameErr = if (name.trim().length < 2) "Enter your full name" else null
    val userErr = when {
        username.trim().length < 3 -> "At least 3 characters"
        username.any { it.isWhitespace() } -> "No spaces please"
        else -> null
    }
    val emailErr = if (!Patterns.EMAIL_ADDRESS.matcher(email.trim()).matches()) "Enter a valid email address" else null
    val passErr = if (password.length < 8) "At least 8 characters" else null

    AuthFrame(title = "Create Your Account", subtitle = "Join Rhythm & Flow today.", onBack = onBack) {
        RfTextField(name, { name = it }, "Full name", Icons.Default.Person, error = if (submitted) nameErr else null)
        VSpace(8)
        RfTextField(username, { username = it }, "Username", Icons.Default.AccountCircle, error = if (submitted) userErr else null)
        VSpace(8)
        RfTextField(email, { email = it }, "Email", Icons.Default.Email, keyboardType = KeyboardType.Email, error = if (submitted) emailErr else null)
        VSpace(8)
        RfTextField(password, { password = it }, "Password", Icons.Default.Lock, isPassword = true, error = if (submitted) passErr else null)
        VSpace(8)
        serverError?.let { Text(it, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodyMedium); VSpace(8) }
        PrimaryButton("Sign Up", loading = busy, onClick = {
            submitted = true
            if (listOf(nameErr, userErr, emailErr, passErr).all { it == null }) {
                busy = true; serverError = null
                scope.launch { serverError = session.register(name, username, email, password); busy = false }
            }
        })
        VSpace(12)
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
            Text("Already have an account?", style = MaterialTheme.typography.bodyMedium)
            TextButton(onClick = onLogin) { Text("Log in", color = Brand.TealDeep) }
        }
    }
}

@Composable
internal fun AuthFrame(title: String, subtitle: String, onBack: (() -> Unit)? = null, content: @Composable () -> Unit) {
    Column(
        Modifier.fillMaxSize().statusBarsPadding().navigationBarsPadding().imePadding()
            .verticalScroll(rememberScrollState()).padding(horizontal = 24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        if (onBack != null) {
            Row(Modifier.fillMaxWidth()) {
                IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Filled.ArrowBack, "Back") }
            }
        } else VSpace(32)
        Image(painterResource(R.drawable.rf_logo_black), "Rhythm & Flow logo", Modifier.size(96.dp))
        VSpace(8)
        Text(title, style = MaterialTheme.typography.headlineLarge, textAlign = TextAlign.Center)
        Text(subtitle, style = MaterialTheme.typography.bodyMedium, color = Brand.Muted, textAlign = TextAlign.Center)
        VSpace(24)
        content()
        VSpace(24)
    }
}
@Composable
private fun OrDivider() {
    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        HorizontalDivider(Modifier.weight(1f), color = Brand.LightGrey)
        Text("  OR  ", style = MaterialTheme.typography.labelMedium, color = Brand.Muted)
        HorizontalDivider(Modifier.weight(1f), color = Brand.LightGrey)
    }
}

