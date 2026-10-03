package com.rhythmandflow.app.ui.components

import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.Spring
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.spring
import androidx.compose.animation.core.tween
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.foundation.background
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.composed
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Shape
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.unit.dp
import com.rhythmandflow.app.ui.theme.Brand

/** Gentle "pressed" feedback: the element shrinks a touch while a finger is on it. */
fun Modifier.pressScale(interaction: MutableInteractionSource, pressedScale: Float = 0.97f): Modifier = composed {
    val pressed by interaction.collectIsPressedAsState()
    val scale by animateFloatAsState(
        targetValue = if (pressed) pressedScale else 1f,
        animationSpec = spring(dampingRatio = Spring.DampingRatioMediumBouncy, stiffness = Spring.StiffnessMedium),
        label = "pressScale",
    )
    this.graphicsLayer { scaleX = scale; scaleY = scale }
}

/** A softly moving grey block shown instead of a spinner while a list loads. */
@Composable
fun ShimmerBlock(modifier: Modifier = Modifier, shape: Shape = RoundedCornerShape(12.dp)) {
    val transition = rememberInfiniteTransition(label = "shimmer")
    val shift by transition.animateFloat(
        initialValue = 0f, targetValue = 1f,
        animationSpec = infiniteRepeatable(tween(1300, easing = LinearEasing)), label = "shift",
    )
    val brush = Brush.linearGradient(
        colors = listOf(Brand.LightGrey.copy(alpha = 0.30f), Brand.LightGrey.copy(alpha = 0.65f), Brand.LightGrey.copy(alpha = 0.30f)),
        start = Offset(shift * 1400f - 500f, 0f), end = Offset(shift * 1400f, 250f),
    )
    Box(modifier.clip(shape).background(brush))
}

/** Placeholder rows that look like the real list while data is loading. Hidden from screen readers (the real content announces itself). */
@Composable
fun ListSkeleton(count: Int = 4, modifier: Modifier = Modifier) {
    Column(
        modifier.fillMaxWidth().padding(horizontal = 20.dp, vertical = 8.dp).clearAndSetSemantics { },
        verticalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        repeat(count) {
            Row(horizontalArrangement = Arrangement.spacedBy(14.dp)) {
                ShimmerBlock(Modifier.size(88.dp), RoundedCornerShape(20.dp))
                Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    ShimmerBlock(Modifier.fillMaxWidth(0.6f).height(18.dp))
                    ShimmerBlock(Modifier.fillMaxWidth().height(12.dp))
                    ShimmerBlock(Modifier.fillMaxWidth(0.4f).height(12.dp))
                }
            }
        }
    }
}
