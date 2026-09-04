using Microsoft.AspNetCore.Http;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Wallow.Shared.Api.Problems;

/// <summary>
/// The single <see cref="IProblemDetailsWriter"/> behind <see cref="IProblemDetailsService"/>.
/// Unlike the framework writers it never declines a request (an error is a problem regardless of the
/// <c>Accept</c> header) and it serialises the problem's runtime type, so a
/// <see cref="HttpValidationProblemDetails"/> keeps its <c>errors</c> dictionary.
/// </summary>
internal sealed class WallowProblemDetailsWriter(
    IOptions<ProblemDetailsOptions> problemOptions,
    IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> jsonOptions) : IProblemDetailsWriter
{
    public bool CanWrite(ProblemDetailsContext context) => true;

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HttpContext httpContext = context.HttpContext;
        ProblemDetails problem = context.ProblemDetails;

        problemOptions.Value.CustomizeProblemDetails?.Invoke(context);

        int statusCode = problem.Status ?? httpContext.Response.StatusCode;
        problem.Status = statusCode;
        httpContext.Response.StatusCode = statusCode;

        return new ValueTask(httpContext.Response.WriteAsJsonAsync(
            problem,
            problem.GetType(),
            jsonOptions.Value.SerializerOptions,
            ProblemContract.ContentType,
            httpContext.RequestAborted));
    }
}
