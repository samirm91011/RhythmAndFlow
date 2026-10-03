using System.Security.Claims;

namespace RhythmFlow.Api.Services;

/// <summary>
/// Catches any exception nobody else handled, records it for the administrators (with an alert), and returns a safe
/// message with a reference number. Stack traces and internal details are never sent to the app.
/// </summary>
public class ErrorHandlingMiddleware(RequestDelegate next, ErrorLogService errors, ILogger<ErrorHandlingMiddleware> log)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            // The app went away mid-request (closed the screen, lost signal). Not an error.
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unhandled exception on {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            var id = await errors.LogAsync(new ErrorReport(
                "API",
                $"{ex.GetType().Name}: {ex.Message}",
                ex.ToString(),
                $"{ctx.Request.Method} {ctx.Request.Path}",
                Guid.TryParse(ctx.User.FindFirstValue("sub"), out var u) ? u : null,
                ctx.User.FindFirstValue("email"),
                null, null,
                FingerprintBasis: $"{ex.GetType().Name}|{ctx.Request.Method} {ctx.Request.Path}|{ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}"));

            if (ctx.Response.HasStarted) throw;
            ctx.Response.Clear();
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "Something went wrong on our side. The Rhythm & Flow team has been told.",
                referenceId = id,
            });
        }
    }
}
