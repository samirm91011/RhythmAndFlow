@file:OptIn(androidx.compose.ui.text.ExperimentalTextApi::class)

package com.rhythmandflow.app.ui.theme

import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Shapes
import androidx.compose.material3.Typography
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontStyle
import androidx.compose.ui.text.font.FontVariation
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.rhythmandflow.app.R

/*
 * Rhythm & Flow brand palette (Brand Blueprint, "Cool Contemporary").
 * NOTE: the blueprint prints hex codes that do not match its own RGB values; the RGB values (and the rendered
 * swatches) are used here: Teal 78-151-139, Tangerine 227-99-61, Light Grey 218-218-218.
 */
object Brand {
    val Teal = Color(0xFF4E978B)
    /** Darker teal for filled buttons/links so white text meets contrast guidelines. */
    val TealDeep = Color(0xFF2F7A6E)
    val TealSoft = Color(0xFFE3F0ED)
    val TealMist = Color(0xFFF0F7F5)
    val Tangerine = Color(0xFFE3633D)
    val TangerineSoft = Color(0xFFFCE9E2)
    val LightGrey = Color(0xFFDADADA)
    val Black = Color(0xFF000000)
    val Ink = Color(0xFF1B1B1B)
    val Muted = Color(0xFF5F6663)          // darker than before: 6:1 on white, easier to read
    /** Very light page colour so white cards lift off the background. */
    val Canvas = Color(0xFFF7F8F6)
    val Surface = Color(0xFFF3F4F2)
    val White = Color(0xFFFFFFFF)
    val Error = Color(0xFFB3261E)
    /** Soft teal-tinted shadow colour used under cards. */
    val Shadow = Color(0xFF1B3D38)
}

private val RhythmColors = lightColorScheme(
    primary = Brand.TealDeep,
    onPrimary = Brand.White,
    primaryContainer = Brand.TealSoft,
    onPrimaryContainer = Brand.Ink,
    secondary = Brand.Tangerine,
    onSecondary = Brand.White,
    secondaryContainer = Brand.TangerineSoft,
    onSecondaryContainer = Brand.Ink,
    background = Brand.Canvas,
    onBackground = Brand.Ink,
    surface = Brand.White,
    onSurface = Brand.Ink,
    surfaceVariant = Brand.Surface,
    onSurfaceVariant = Brand.Muted,
    outline = Brand.LightGrey,
    error = Brand.Error,
)

// Brand fonts. Open Sans is the Brand Blueprint's secondary typeface. Amaris (primary) is licensed and its files were not
// supplied, so Cormorant Garamond (SIL Open Font Licence) stands in for headings; swap `HeadingFont` when the client provides it.
private fun heading(weight: Int, style: FontStyle = FontStyle.Normal) = Font(
    resId = if (style == FontStyle.Italic) R.font.cormorant_italic_variable else R.font.cormorant_variable,
    weight = FontWeight(weight), style = style,
    variationSettings = FontVariation.Settings(FontVariation.weight(weight)),
)

private fun body(weight: Int) = Font(
    resId = R.font.opensans_variable, weight = FontWeight(weight),
    variationSettings = FontVariation.Settings(FontVariation.weight(weight)),
)

val HeadingFont: FontFamily = FontFamily(
    heading(400), heading(500), heading(600), heading(700),
    heading(400, FontStyle.Italic), heading(500, FontStyle.Italic), heading(600, FontStyle.Italic),
)
val BodyFont: FontFamily = FontFamily(body(400), body(500), body(600), body(700))

private val RhythmTypography = Typography(
    displayLarge = TextStyle(fontFamily = HeadingFont, fontWeight = FontWeight.Medium, fontSize = 46.sp, lineHeight = 48.sp),
    headlineLarge = TextStyle(fontFamily = HeadingFont, fontWeight = FontWeight.SemiBold, fontSize = 36.sp, lineHeight = 40.sp),
    headlineMedium = TextStyle(fontFamily = HeadingFont, fontWeight = FontWeight.SemiBold, fontSize = 31.sp, lineHeight = 35.sp),
    headlineSmall = TextStyle(fontFamily = HeadingFont, fontWeight = FontWeight.SemiBold, fontSize = 26.sp, lineHeight = 30.sp),
    titleLarge = TextStyle(fontFamily = HeadingFont, fontWeight = FontWeight.SemiBold, fontSize = 24.sp, lineHeight = 28.sp),
    titleMedium = TextStyle(fontFamily = HeadingFont, fontWeight = FontWeight.Bold, fontSize = 20.sp, lineHeight = 24.sp),
    titleSmall = TextStyle(fontFamily = BodyFont, fontWeight = FontWeight.SemiBold, fontSize = 14.sp, lineHeight = 20.sp),
    bodyLarge = TextStyle(fontFamily = BodyFont, fontWeight = FontWeight.Normal, fontSize = 16.sp, lineHeight = 24.sp),
    bodyMedium = TextStyle(fontFamily = BodyFont, fontWeight = FontWeight.Normal, fontSize = 14.sp, lineHeight = 21.sp),
    bodySmall = TextStyle(fontFamily = BodyFont, fontWeight = FontWeight.Normal, fontSize = 12.sp, lineHeight = 17.sp),
    labelLarge = TextStyle(fontFamily = BodyFont, fontWeight = FontWeight.SemiBold, fontSize = 15.sp, letterSpacing = 0.2.sp),
    labelMedium = TextStyle(fontFamily = BodyFont, fontWeight = FontWeight.Medium, fontSize = 12.sp),
    labelSmall = TextStyle(fontFamily = BodyFont, fontWeight = FontWeight.Medium, fontSize = 11.sp),
)

private val RhythmShapes = Shapes(
    small = RoundedCornerShape(14.dp),
    medium = RoundedCornerShape(20.dp),
    large = RoundedCornerShape(28.dp),
)

@Composable
fun RhythmTheme(content: @Composable () -> Unit) {
    MaterialTheme(colorScheme = RhythmColors, typography = RhythmTypography, shapes = RhythmShapes, content = content)
}
