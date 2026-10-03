package com.rhythmandflow.app.ui.components

import androidx.compose.foundation.BorderStroke
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.draw.drawBehind
import androidx.compose.material.icons.filled.Spa
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.LocalIndication
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.Visibility
import androidx.compose.material.icons.filled.VisibilityOff
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilterChipDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.rememberUpdatedState
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.rhythmandflow.app.ui.theme.Brand

@Composable
fun PrimaryButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    loading: Boolean = false,
) = GradientButton(text, onClick, modifier, enabled, loading, listOf(Color(0xFF3B8C7F), Brand.TealDeep), Brand.TealDeep)

@Composable
fun AccentButton(text: String, onClick: () -> Unit, modifier: Modifier = Modifier, enabled: Boolean = true) =
    GradientButton(text, onClick, modifier, enabled, false, listOf(Color(0xFFC9502B), Brand.TangerineDeep), Brand.TangerineDeep)

/** Pill button with a soft gradient and glow that dips slightly when pressed. Grows taller with large text sizes. */
@Composable
private fun GradientButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier,
    enabled: Boolean,
    loading: Boolean,
    colors: List<Color>,
    glow: Color,
) {
    val interaction = remember { MutableInteractionSource() }
    val indication = LocalIndication.current
    val shape = RoundedCornerShape(28.dp)
    val active = enabled && !loading
    Box(
        modifier
            .fillMaxWidth()
            .heightIn(min = 54.dp)
            .pressScale(interaction)
            .shadow(if (active) 7.dp else 0.dp, shape, ambientColor = glow.copy(alpha = 0.25f), spotColor = glow.copy(alpha = 0.45f))
            .clip(shape)
            .background(Brush.horizontalGradient(if (enabled) colors else listOf(Brand.LightGrey, Brand.LightGrey)))
            .clickable(interactionSource = interaction, indication = indication, enabled = active, role = Role.Button, onClick = onClick)
            .padding(horizontal = 24.dp, vertical = 14.dp),
        contentAlignment = Alignment.Center,
    ) {
        if (loading) CircularProgressIndicator(Modifier.size(22.dp).semantics { contentDescription = "Please wait" }, color = Color.White, strokeWidth = 2.dp)
        else Text(text, style = MaterialTheme.typography.labelLarge, color = if (enabled) Color.White else Brand.Muted, textAlign = TextAlign.Center)
    }
}

@Composable
fun SecondaryButton(text: String, onClick: () -> Unit, modifier: Modifier = Modifier, enabled: Boolean = true) {
    val interaction = remember { MutableInteractionSource() }
    val indication = LocalIndication.current
    val shape = RoundedCornerShape(28.dp)
    Box(
        modifier
            .fillMaxWidth()
            .heightIn(min = 54.dp)
            .pressScale(interaction)
            .clip(shape)
            .background(Color.White)
            .border(1.dp, Brand.LightGrey, shape)
            .clickable(interactionSource = interaction, indication = indication, enabled = enabled, role = Role.Button, onClick = onClick)
            .padding(horizontal = 24.dp, vertical = 14.dp),
        contentAlignment = Alignment.Center,
    ) { Text(text, style = MaterialTheme.typography.labelLarge, color = if (enabled) Brand.Ink else Brand.Muted, textAlign = TextAlign.Center) }
}
@Composable
fun RfTextField(
    value: String,
    onValueChange: (String) -> Unit,
    label: String,
    icon: ImageVector? = null,
    modifier: Modifier = Modifier,
    isPassword: Boolean = false,
    keyboardType: KeyboardType = KeyboardType.Text,
    singleLine: Boolean = true,
    minLines: Int = 1,
    error: String? = null,
) {
    var visible by remember { mutableStateOf(false) }
    OutlinedTextField(
        value = value,
        onValueChange = onValueChange,
        label = { Text(label) },
        leadingIcon = icon?.let { { Icon(it, null, tint = Brand.Teal) } },
        trailingIcon = if (isPassword) {
            {
                IconButton(onClick = { visible = !visible }) {
                    Icon(if (visible) Icons.Default.VisibilityOff else Icons.Default.Visibility, if (visible) "Hide password" else "Show password", tint = Brand.Teal)
                }
            }
        } else null,
        visualTransformation = if (isPassword && !visible) PasswordVisualTransformation() else VisualTransformation.None,
        keyboardOptions = KeyboardOptions(keyboardType = if (isPassword) KeyboardType.Password else keyboardType),
        singleLine = singleLine,
        minLines = minLines,
        isError = error != null,
        supportingText = error?.let { { Text(it) } },
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = OutlinedTextFieldDefaults.colors(
            focusedBorderColor = Brand.TealDeep, unfocusedBorderColor = Brand.LightGrey,
            focusedLabelColor = Brand.TealDeep, cursorColor = Brand.TealDeep,
        ),
    )
}

@Composable
fun ScreenHeader(
    title: String,
    subtitle: String? = null,
    onBack: (() -> Unit)? = null,
    action: (@Composable () -> Unit)? = null,
) {
    Row(Modifier.fillMaxWidth().padding(horizontal = 20.dp, vertical = 12.dp), verticalAlignment = Alignment.CenterVertically) {
        if (onBack != null) {
            IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Filled.ArrowBack, "Back") }
        }
        Column(Modifier.weight(1f)) {
            Text(title, style = MaterialTheme.typography.headlineMedium, modifier = Modifier.semantics { heading() })
            if (subtitle != null) Text(subtitle, style = MaterialTheme.typography.bodyMedium, color = Brand.Muted)
        }
        action?.invoke()
    }
}

@Composable
fun SelectChip(text: String, selected: Boolean, onClick: () -> Unit, modifier: Modifier = Modifier) {
    FilterChip(
        selected = selected,
        onClick = onClick,
        label = { Text(text) },
        modifier = modifier,
        shape = RoundedCornerShape(20.dp),
        colors = FilterChipDefaults.filterChipColors(
            selectedContainerColor = Brand.TealDeep, selectedLabelColor = Color.White,
            containerColor = Color.White, labelColor = Brand.Ink,
        ),
        border = FilterChipDefaults.filterChipBorder(
            enabled = true, selected = selected, borderColor = Brand.LightGrey, selectedBorderColor = Brand.TealDeep,
        ),
    )
}

/** Soft rounded white card with a gentle, teal-tinted shadow. Used for most list rows. */
@Composable
fun SoftCard(
    modifier: Modifier = Modifier,
    onClick: (() -> Unit)? = null,
    background: Color = Color.White,
    content: @Composable () -> Unit,
) {
    val shape = RoundedCornerShape(22.dp)
    val interaction = remember { MutableInteractionSource() }
    val indication = LocalIndication.current
    Box(
        modifier
            .let { if (onClick != null) it.pressScale(interaction, 0.985f) else it }
            .shadow(5.dp, shape, ambientColor = Brand.Shadow.copy(alpha = 0.10f), spotColor = Brand.Shadow.copy(alpha = 0.18f))
            .clip(shape)
            .background(background)
            .let { if (onClick != null) it.clickable(interactionSource = interaction, indication = indication, role = Role.Button, onClick = onClick) else it }
            .padding(16.dp),
    ) { content() }
}
/** Gradient stand-in for a thumbnail/hero image (the client's photography is not in the app yet), with soft decorative circles. */
@Composable
fun GradientBox(modifier: Modifier = Modifier, colors: List<Color> = listOf(Brand.Teal, Brand.TealDeep), content: @Composable () -> Unit = {}) {
    Box(
        modifier
            .background(Brush.linearGradient(colors))
            .drawBehind {
                drawCircle(Color.White.copy(alpha = 0.10f), radius = size.minDimension * 0.55f, center = Offset(size.width * 0.95f, size.height * 0.05f))
                drawCircle(Color.White.copy(alpha = 0.07f), radius = size.minDimension * 0.40f, center = Offset(size.width * 0.05f, size.height * 0.95f))
            },
        contentAlignment = Alignment.Center,
    ) { content() }
}
@Composable
fun LockBadge(modifier: Modifier = Modifier) {
    Box(
        modifier.size(26.dp).clip(CircleShape).background(Brand.Black.copy(alpha = 0.55f)),
        contentAlignment = Alignment.Center,
    ) { Icon(Icons.Default.Lock, "Locked", tint = Color.White, modifier = Modifier.size(14.dp)) }
}

@Composable
fun LoadingBox(modifier: Modifier = Modifier) {
    Box(modifier.fillMaxSize().semantics { contentDescription = "Loading" }) { ListSkeleton(4, Modifier.padding(top = 8.dp)) }
}

@Composable
fun ErrorBox(message: String, onRetry: (() -> Unit)?, modifier: Modifier = Modifier) {
    Column(modifier.fillMaxWidth().padding(32.dp), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text(message, textAlign = TextAlign.Center, color = Brand.Muted)
        if (onRetry != null) TextButton(onClick = onRetry) { Text("Try again", color = Brand.TealDeep) }
    }
}

@Composable
fun EmptyState(title: String, body: String, modifier: Modifier = Modifier) {
    Column(modifier.fillMaxWidth().padding(32.dp), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Box(Modifier.size(64.dp).clip(CircleShape).background(Brand.TealSoft), contentAlignment = Alignment.Center) {
            Icon(Icons.Default.Spa, null, tint = Brand.TealDeep, modifier = Modifier.size(30.dp))
        }
        Text(title, style = MaterialTheme.typography.titleMedium, textAlign = TextAlign.Center)
        Text(body, style = MaterialTheme.typography.bodyMedium, color = Brand.Muted, textAlign = TextAlign.Center)
    }
}
@Composable
fun InfoPill(text: String, modifier: Modifier = Modifier, color: Color = Brand.TealSoft, textColor: Color = Brand.TealDeep) {
    Box(modifier.clip(RoundedCornerShape(50)).background(color).padding(horizontal = 10.dp, vertical = 4.dp)) {
        Text(text, style = MaterialTheme.typography.labelMedium, color = textColor, maxLines = 1, softWrap = false)
    }
}

@Composable
fun SectionTitle(text: String, modifier: Modifier = Modifier, action: String? = null, onAction: (() -> Unit)? = null) {
    Row(modifier.fillMaxWidth().padding(horizontal = 20.dp), verticalAlignment = Alignment.CenterVertically) {
        Text(text, style = MaterialTheme.typography.titleLarge, modifier = Modifier.weight(1f).semantics { heading() })
        if (action != null && onAction != null) TextButton(onClick = onAction) { Text(action, color = Brand.TealDeep) }
    }
}

/**
 * Caps text scaling for small fixed-size controls (such as bottom-bar labels) so words never break in the middle.
 * Everything else in the app still follows the person's text-size setting in full.
 */
@Composable
fun CappedFontScale(max: Float = 1.3f, content: @Composable () -> Unit) {
    val d = androidx.compose.ui.platform.LocalDensity.current
    androidx.compose.runtime.CompositionLocalProvider(
        androidx.compose.ui.platform.LocalDensity provides androidx.compose.ui.unit.Density(d.density, minOf(d.fontScale, max)),
    ) { content() }
}

/** Scrollable page with standard side padding. */
@Composable
fun PageColumn(modifier: Modifier = Modifier, content: @Composable () -> Unit) {
    Column(modifier.fillMaxSize().verticalScroll(rememberScrollState())) { content() }
}

@Composable
fun VSpace(h: Int) = Spacer(Modifier.height(h.dp))

@Composable
fun HSpace(w: Int) = Spacer(Modifier.width(w.dp))

fun Modifier.outlined(color: Color = Brand.LightGrey, radius: Int = 16): Modifier =
    this.border(1.dp, color, RoundedCornerShape(radius.dp))

val ScreenPadding = PaddingValues(horizontal = 20.dp)

@Composable
fun Modifier.vScroll(): Modifier = this.verticalScroll(rememberScrollState())


/** Runs [block] each time the screen comes back to the foreground (used to refresh data after subscribing, booking, etc.). */
@Composable
fun OnResume(block: () -> Unit) {
    val owner = LocalLifecycleOwner.current
    val current = rememberUpdatedState(block)
    DisposableEffect(owner) {
        val observer = LifecycleEventObserver { _, e -> if (e == Lifecycle.Event.ON_RESUME) current.value() }
        owner.lifecycle.addObserver(observer)
        onDispose { owner.lifecycle.removeObserver(observer) }
    }
}