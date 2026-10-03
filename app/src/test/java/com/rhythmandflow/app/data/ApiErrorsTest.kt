package com.rhythmandflow.app.data

import org.junit.Assert.assertEquals
import org.junit.Test

class ApiErrorsTest {
    @Test fun `uses the server's own error sentence`() {
        assertEquals("That email is already registered.", friendlyErrorMessage("""{"error":"That email is already registered."}""", 409))
    }

    @Test fun `reads the first validation problem from ASP NET`() {
        val body = """{"title":"One or more validation errors occurred.","errors":{"NewPassword":["The field NewPassword must be a string with a minimum length of 8."]}}"""
        assertEquals("The field NewPassword must be a string with a minimum length of 8.", friendlyErrorMessage(body, 400))
    }

    @Test fun `falls back to a plain sentence for known status codes`() {
        assertEquals("Please sign in again.", friendlyErrorMessage(null, 401))
        assertEquals("You don't have access to that.", friendlyErrorMessage("", 403))
        assertEquals("We couldn't find that.", friendlyErrorMessage(null, 404))
        assertEquals("Too many attempts. Please wait a moment.", friendlyErrorMessage(null, 429))
    }

    @Test fun `unknown status codes include the code so support can ask for it`() {
        assertEquals("Something went wrong (error 502).", friendlyErrorMessage(null, 502))
    }

    @Test fun `a body that is not JSON does not crash`() {
        assertEquals("Something went wrong (error 500).", friendlyErrorMessage("<html>Bad gateway</html>", 500))
    }

    @Test fun `a null error field falls through to the status message`() {
        assertEquals("We couldn't find that.", friendlyErrorMessage("""{"error":null}""", 404))
    }
}
