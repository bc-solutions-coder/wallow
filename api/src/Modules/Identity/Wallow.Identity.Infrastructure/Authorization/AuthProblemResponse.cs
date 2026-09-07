using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Identity.Infrastructure.Authorization;

/// <summary>
/// Submits authentication and authorization problem responses through
/// <see cref="IProblemDetailsService"/> so the host can apply its problem contract.
/// </summary>
public static class AuthProblemResponse
{
    public static Task WriteAsync(HttpContext httpContext, int statusCode)
    {
        string detail = statusCode == StatusCodes.Status403Forbidden
            ? "The authenticated identity lacks the permission this resource requires."
            : "This resource requires an authenticated session or bearer token.";
        return WriteAsync(httpContext, statusCode, detail);
    }

    public static async Task WriteAsync(HttpContext httpContext, int statusCode, string detail)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.StatusCode = statusCode;

        ErrorCatalogEntry entry = statusCode == StatusCodes.Status403Forbidden
            ? SharedErrors.Forbidden
            : SharedErrors.Unauthenticated;

        ProblemDetails problem = new()
        {
            Status = statusCode,
            Detail = detail,
        };
        problem.Extensions["code"] = entry.Code;

        IProblemDetailsService problemDetailsService =
            httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem });
    }
}
