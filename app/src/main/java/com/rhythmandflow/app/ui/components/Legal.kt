package com.rhythmandflow.app.ui.components

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.LinkAnnotation
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.TextLinkStyles
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.text.withLink
import com.rhythmandflow.app.ui.theme.Brand

/** The client's own legal pages. They live on the Rhythm & Flow website, so there is one version to keep up to date. */
object LegalLinks {
    const val TERMS = "https://rhythmandflow.co.za/terms-of-use/"
    const val PRIVACY = "https://rhythmandflow.co.za/privacy-policy/"
}

/** Says where the Terms of Use and Privacy Policy can be read, with tappable links to the website. */
@Composable
fun LegalNotice(modifier: Modifier = Modifier, lead: String = "By creating an account you agree to our ") {
    val link = SpanStyle(color = Brand.TealDeep, fontWeight = FontWeight.SemiBold, textDecoration = TextDecoration.Underline)
    val text = buildAnnotatedString {
        append(lead)
        withLink(LinkAnnotation.Url(LegalLinks.TERMS, TextLinkStyles(link))) { append("Terms of Use") }
        append(" and ")
        withLink(LinkAnnotation.Url(LegalLinks.PRIVACY, TextLinkStyles(link))) { append("Privacy Policy") }
        append(". You can read both on our website, rhythmandflow.co.za.")
    }
    Text(text, modifier = modifier, style = MaterialTheme.typography.bodySmall, color = Brand.Muted, textAlign = TextAlign.Center)
}
