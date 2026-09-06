using System.Diagnostics;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Diagnostics;
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Api.Middleware;

/// <summary>
/// Global exception handler that maps an unhandled exception to a status and a catalog code and
/// writes the problem through <see cref="IProblemDetailsService"/>, so the body follows
/// <see cref="ProblemContract"/> like every other error. The exception rides along on the
/// <see cref="ProblemDetailsContext"/>; the contract exposes it only in Development, only on 5xx.
/// </summary>
internal partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        string traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        string path = httpContext.Request.Path;
        IProblemDetailsService problemDetailsService =
            httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

        // The exception-handler middleware answers a cancellation whose client has already gone
        // on its own; this branch only sees the rare late abort. Nobody is left to read a body,
        // and writing one against the aborted token would throw, so the status is the response.
        // A cancellation whose client is still connected is a server-side fault and falls
        // through to the 500 below.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            LogRequestCancelled(traceId, path);

            // Do not mark the span as error for cancellations
            Activity.Current?.SetStatus(ActivityStatusCode.Ok);

            httpContext.Response.StatusCode = ProblemContract.ClientClosedRequest;
            return true;
        }

        LogUnhandledException(exception, traceId, path);

        if (exception is ValidationException validation)
        {
            IDictionary<string, string[]> errors = new ValidationResult(validation.Errors).ToDictionary();
            return await problemDetailsService.TryWriteValidationProblemAsync(httpContext, errors, exception);
        }

        (int statusCode, string code, string? detail) = exception switch
        {
            DomainException domain => (domain.Kind.ToHttpStatusCode(), domain.Code, domain.Message),
            BadHttpRequestException bad when bad.StatusCode < StatusCodes.Status500InternalServerError =>
                (bad.StatusCode, SharedErrors.ClientError.Code, SharedErrors.ClientError.DefaultMessage),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, SharedErrors.Unauthenticated.Code, null),
            ArgumentException => (StatusCodes.Status400BadRequest, SharedErrors.ClientError.Code, SharedErrors.ClientError.DefaultMessage),
            _ => (StatusCodes.Status500InternalServerError, SharedErrors.ServerError.Code, null),
        };

        return await problemDetailsService.TryWriteProblemAsync(httpContext, statusCode, code, detail, exception);
    }
}

internal partial class GlobalExceptionHandler
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception occurred. TraceId: {TraceId}, Path: {Path}")]
    private partial void LogUnhandledException(Exception ex, string traceId, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Request cancelled by client. TraceId: {TraceId}, Path: {Path}")]
    private partial void LogRequestCancelled(string traceId, string path);
}
