package com.rhythmandflow.app.data

import com.google.gson.JsonParser

/**
 * Turns an API error response into a sentence a person can act on.
 * Understands our own `{"error": "..."}` shape and ASP.NET validation problems (`{"errors": {"Field": ["..."]}}`),
 * and falls back to a plain message for the status code.
 */
fun friendlyErrorMessage(body: String?, code: Int): String {
    if (!body.isNullOrBlank()) {
        try {
            val obj = JsonParser.parseString(body).asJsonObject
            obj.get("error")?.takeIf { !it.isJsonNull }?.asString?.let { return it }
            obj.getAsJsonObject("errors")?.entrySet()?.firstOrNull()?.value?.asJsonArray?.firstOrNull()?.asString?.let { return it }
        } catch (_: Exception) { }
    }
    return when (code) {
        401 -> "Please sign in again."
        403 -> "You don't have access to that."
        404 -> "We couldn't find that."
        429 -> "Too many attempts. Please wait a moment."
        else -> "Something went wrong (error $code)."
    }
}
