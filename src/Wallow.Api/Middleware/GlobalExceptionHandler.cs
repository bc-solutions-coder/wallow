using FluentValidation;
using Wallow.Shared.Kernel.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Wallow.Api.Middleware;

/// <summary>
/// Global exception handler that converts exceptions to Problem Details responses.
/// Implements RFC 7807 for consistent error responses across the API.
/// </summary>
internal partial class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        string traceId = System.Diagnostics.Activity.Current?.Id ?? httpContext.TraceIdentifier;

        if (exception is OperationCanceledException)
        {
            string path = httpContext.Request.Path;
            LogRequestCancelled(traceId, path);

            // Do not mark the span as error for cancellations
            System.Diagnostics.Activity.Current?.SetStatus(System.Diagnostics.ActivityStatusCode.Ok);

            ProblemDetails cancelledProblem = new()
            {
                Status = 499,
                Title = "Client Closed Request",
                Type = "https://httpstatuses.com/499",
                Instance = $"/errors/{traceId}",
                Detail = "The request was cancelled by the client.",
                Extensions = { ["traceId"] = traceId }
            };

            httpContext.Response.StatusCode = 499;
            await httpContext.Response.WriteAsJsonAsync(cancelledProblem, cancellationToken);
            return true;
        }

        LogUnhandledException(exception, traceId, httpContext.Request.Path);

        ProblemDetails problemDetails = CreateProblemDetails(exception, traceId);

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private ProblemDetails CreateProblemDetails(Exception exception, string traceId)
    {
        (int statusCode, string title, string type) = exception switch
        {
            EntityNotFoundException => (
                StatusCodes.Status404NotFound,
                "Resource Not Found",
                "https://tools.ietf.org/html/rfc7231#section-6.5.4"),

            BusinessRuleException => (
                StatusCodes.Status422UnprocessableEntity,
                "Business Rule Violation",
                "https://tools.ietf.org/html/rfc4918#section-11.2"),

            ValidationException => (
                StatusCodes.Status400BadRequest,
                "Validation Error",
                "https://tools.ietf.org/html/rfc7231#section-6.5.1"),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "https://tools.ietf.org/html/rfc7235#section-3.1"),

            ForbiddenAccessException => (
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "https://tools.ietf.org/html/rfc7231#section-6.5.3"),

            ArgumentException or ArgumentNullException => (
                StatusCodes.Status400BadRequest,
                "Bad Request",
                "https://tools.ietf.org/html/rfc7231#section-6.5.1"),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "https://tools.ietf.org/html/rfc7231#section-6.6.1")
        };

        ProblemDetails problemDetails = new()
        {
            Status = statusCode,
            Title = title,
            Type = type,
            Instance = $"/errors/{traceId}",
            Extensions =
            {
                ["traceId"] = traceId
            }
        };

        // Add error code for domain exceptions
        if (exception is DomainException domainException)
        {
            problemDetails.Extensions["code"] = domainException.Code;
            problemDetails.Detail = exception.Message;
        }
        else if (exception is ValidationException validationException)
        {
            problemDetails.Detail = string.Join("; ", validationException.Errors.Select(e => e.ErrorMessage));
            problemDetails.Extensions["errors"] = validationException.Errors
                .Select(e => new { field = e.PropertyName, message = e.ErrorMessage })
                .ToArray();
        }
        else if (environment.IsDevelopment())
        {
            // Only expose exception details in development
            problemDetails.Detail = exception.Message;
            problemDetails.Extensions["exception"] = exception.ToString();
        }
        else
        {
            problemDetails.Detail = "An unexpected error occurred. Please try again later.";
        }

        return problemDetails;
    }
}

internal partial class GlobalExceptionHandler
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception occurred. TraceId: {TraceId}, Path: {Path}")]
    private partial void LogUnhandledException(Exception ex, string traceId, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Request cancelled by client. TraceId: {TraceId}, Path: {Path}")]
    private partial void LogRequestCancelled(string traceId, string path);
}
