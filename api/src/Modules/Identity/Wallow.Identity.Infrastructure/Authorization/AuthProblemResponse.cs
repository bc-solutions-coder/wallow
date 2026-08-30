using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Identity.Infrastructure.Authorization;

/// <summary>
/// Writes the RFC 7807 body an authentication or authorization refusal owes. The body is
/// load-bearing: under the nosniff header an empty 401/403 renders as a blank page in a
/// browser, so every refusal the module issues carries a problem document.
/// </summary>
public static class AuthProblemResponse
{
    public static Task WriteAsync(HttpContext httpContext, int statusCode)
    {
        (string title, string detail) = statusCode == StatusCodes.Status403Forbidden
            ? ("Forbidden.", "The authenticated identity lacks the permission this resource requires.")
            : ("Authentication required.", "This resource requires an authenticated session or bearer token.");
        return WriteAsync(httpContext, statusCode, title, detail);
    }

    public static async Task WriteAsync(HttpContext httpContext, int statusCode, string title, string detail)
    {
        httpContext.Response.StatusCode = statusCode;

        ProblemDetails problem = new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
        };

        // Resolved lazily: this module can be hosted without problem-details services, and the
        // caller must still answer with a body either way.
        IProblemDetailsService? problemDetailsService =
            httpContext.RequestServices?.GetService<IProblemDetailsService>();
        if (problemDetailsService is not null
            && await problemDetailsService.TryWriteAsync(
                new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem }))
        {
            return;
        }

        // The default writer refuses an Accept header that admits no JSON (browsers send */*
        // and pass); the body is still owed, so write the same document directly.
        await httpContext.Response.WriteAsJsonAsync(
            problem, options: null, contentType: "application/problem+json", httpContext.RequestAborted);
    }
}
