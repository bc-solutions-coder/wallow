using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Shared.Api.Problems;

/// <summary>
/// The middleware-side entry points into the problem writer. Each sets the response status and
/// hands a problem to <see cref="IProblemDetailsService.TryWriteAsync"/>; the contract fills the
/// remaining members.
/// </summary>
public static class ProblemDetailsServiceExtensions
{
    /// <summary>Writes a problem for a catalog entry, taking the status from the entry's kind.</summary>
    public static ValueTask<bool> TryWriteProblemAsync(
        this IProblemDetailsService service,
        HttpContext httpContext,
        ErrorCatalogEntry entry,
        string? detail = null,
        Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return service.TryWriteProblemAsync(
            httpContext,
            entry.Kind.ToHttpStatusCode(),
            entry.Code,
            detail ?? entry.DefaultMessage,
            exception);
    }

    /// <summary>Writes a problem for a status; a null <paramref name="code"/> takes the status-generic code.</summary>
    public static ValueTask<bool> TryWriteProblemAsync(
        this IProblemDetailsService service,
        HttpContext httpContext,
        int statusCode,
        string? code = null,
        string? detail = null,
        Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.StatusCode = statusCode;

        ProblemDetails problem = new()
        {
            Status = statusCode,
            Detail = detail,
        };

        if (!string.IsNullOrWhiteSpace(code))
        {
            problem.Extensions[ProblemContract.CodeMember] = code;
        }

        return service.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    /// <summary>Writes a 400 validation problem carrying <paramref name="errors"/>.</summary>
    public static ValueTask<bool> TryWriteValidationProblemAsync(
        this IProblemDetailsService service,
        HttpContext httpContext,
        IDictionary<string, string[]> errors,
        Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(errors);

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return service.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new HttpValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest },
            Exception = exception,
        });
    }
}
