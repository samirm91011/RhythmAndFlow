package com.rhythmandflow.app.ui.screens

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.StrokeJoin
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.graphics.vector.path
import androidx.compose.ui.unit.dp

/**
 * The Stretch & Mobility icon the client asked for: a simple line drawing of a person bending sideways with one arm arching
 * overhead. Drawn as strokes, so it takes whatever colour the icon is given.
 */
val StretchFigureIcon: ImageVector by lazy {
    ImageVector.Builder("StretchFigure", 24.dp, 24.dp, 24f, 24f).apply {
        val line = SolidColor(Color.Black)
        fun stroke(block: androidx.compose.ui.graphics.vector.PathBuilder.() -> Unit) =
            path(stroke = line, strokeLineWidth = 1.5f, strokeLineCap = StrokeCap.Round, strokeLineJoin = StrokeJoin.Round, pathBuilder = block)

        // head
        stroke {
            moveTo(9.1f, 8.4f)
            arcToRelative(2.3f, 2.3f, 0f, true, true, -4.6f, 0f)
            arcToRelative(2.3f, 2.3f, 0f, true, true, 4.6f, 0f)
        }
        // the arm arching overhead, then down the back to the right foot
        stroke {
            moveTo(3.8f, 2.9f)
            curveTo(9.5f, 0.6f, 18.2f, 2.4f, 19.8f, 11.0f)
            curveTo(20.4f, 14.6f, 19.0f, 18.4f, 17.2f, 21.2f)
        }
        // the bent arm at the chest
        stroke {
            moveTo(6.2f, 12.6f)
            lineTo(8.6f, 14.0f)
            lineTo(11.6f, 13.2f)
        }
        // the legs: an upside-down V under the hips
        stroke {
            moveTo(7.8f, 21.2f)
            lineTo(11.6f, 14.4f)
            lineTo(14.8f, 21.2f)
        }
        // the ground
        stroke {
            moveTo(2.2f, 22.0f)
            lineTo(21.8f, 22.0f)
        }
    }.build()
}