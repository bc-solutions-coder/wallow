using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Shared.Api.Problems;

/// <summary>
/// The one problem+json contract every error body honours. <see cref="Customize"/> is installed as
/// <see cref="ProblemDetailsOptions.CustomizeProblemDetails"/>, so every problem the framework
/// writes (the problem-details service, MVC's factory, status-code pages, automatic 400s) passes
/// through it exactly once, right before serialisation.
/// </summary>
/// <remarks>
/// Members: <c>type</c> is always <c>about:blank</c>; <c>title</c> is the status reason phrase;
/// <c>status</c>, <c>code</c> and <c>traceId</c> are always present; <c>detail</c> is a user-safe
/// sentence on 4xx and one fixed generic sentence on 5xx in every environment; <c>errors</c> appears
/// only on validation problems as camelCase, dot-preserving <c>{ field: [messages] }</c>; the
/// Development-only <c>exception</c> extension is added when a 5xx carries an exception. The
/// <c>instance</c>, <c>api</c> and <c>version</c> members are removed. The customisation is
/// idempotent, so a problem passing through twice serialises identically.
/// </remarks>
public static class ProblemContract
{
    /// <summary>The media type every error body is written as.</summary>
    public const string ContentType = "application/problem+json";

    /// <summary>The only <c>type</c> value the contract emits.</summary>
    public const string BlankType = "about:blank";

    /// <summary>Extension member carrying the catalog code.</summary>
    public const string CodeMember = "code";

    /// <summary>Extension member carrying the W3C trace id (or the request's trace identifier).</summary>
    public const string TraceIdMember = "traceId";

    /// <summary>Development-only extension member carrying the exception text on 5xx.</summary>
    public const string ExceptionMember = "exception";

    /// <summary>The non-standard status nginx popularised for a request the client abandoned.</summary>
    public const int ClientClosedRequest = 499;

    /// <summary>Members every problem body carries, in the order the OpenAPI document lists them.</summary>
    public static readonly IReadOnlyList<string> AlwaysPresentMembers =
        ["type", "title", "status", CodeMember, TraceIdMember];

    private static readonly string[] _removedMembers = ["api", "version"];

    /// <summary>
    /// Applies the contract to <paramref name="context"/>. Safe to invoke more than once.
    /// </summary>
    public static void Customize(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HttpContext httpContext = context.HttpContext;
        ProblemDetails problem = context.ProblemDetails;
        int statusCode = ResolveStatus(problem, httpContext);

        problem.Status = statusCode;
        problem.Type = BlankType;
        problem.Title = TitleFor(statusCode);
        problem.Instance = null;

        foreach (string member in _removedMembers)
        {
            problem.Extensions.Remove(member);
        }

        ErrorCatalogEntry generic = GenericEntryFor(statusCode);

        if (problem is HttpValidationProblemDetails validation)
        {
            problem.Extensions[CodeMember] = SharedErrors.ValidationFailed.Code;
            problem.Detail = SharedErrors.ValidationFailed.DefaultMessage;
            NormalizeValidationKeys(validation);
        }
        else
        {
            if (!HasCode(problem))
            {
                problem.Extensions[CodeMember] = generic.Code;
            }

            if (statusCode >= 500)
            {
                problem.Detail = SharedErrors.ServerError.DefaultMessage;
            }
            else if (string.IsNullOrWhiteSpace(problem.Detail))
            {
                problem.Detail = generic.DefaultMessage;
            }
        }

        if (!problem.Extensions.TryGetValue(TraceIdMember, out object? traceId) || traceId is not string { Length: > 0 })
        {
            problem.Extensions[TraceIdMember] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        }

        if (statusCode >= 500 && context.Exception is not null && IsDevelopment(httpContext))
        {
            problem.Extensions[ExceptionMember] = context.Exception.ToString();
        }
    }

    /// <summary>
    /// The <c>title</c> for a status code: the HTTP reason phrase, or <c>Error</c> for an unknown status.
    /// </summary>
    public static string TitleFor(int statusCode)
    {
        if (statusCode == ClientClosedRequest)
        {
            return "Client Closed Request";
        }

        string phrase = ReasonPhrases.GetReasonPhrase(statusCode);
        return phrase.Length > 0 ? phrase : "Error";
    }

    /// <summary>
    /// The status-generic catalog entry that supplies <c>code</c> (and a 4xx <c>detail</c>) when a
    /// writer sets none.
    /// </summary>
    public static ErrorCatalogEntry GenericEntryFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => SharedErrors.ValidationFailed,
        StatusCodes.Status401Unauthorized => SharedErrors.Unauthenticated,
        StatusCodes.Status403Forbidden => SharedErrors.Forbidden,
        StatusCodes.Status404NotFound => SharedErrors.NotFound,
        StatusCodes.Status405MethodNotAllowed => SharedErrors.MethodNotAllowed,
        StatusCodes.Status429TooManyRequests => SharedErrors.RateLimitExceeded,
        >= StatusCodes.Status500InternalServerError => SharedErrors.ServerError,
        _ => SharedErrors.ClientError,
    };

    /// <summary>
    /// Normalises a validation dictionary key to the wire shape: each dot-separated segment is
    /// camelCased, the dots are preserved (<c>Branding.DisplayName</c> becomes
    /// <c>branding.displayName</c>; an empty model-level key stays empty).
    /// </summary>
    public static string NormalizeValidationKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length == 0 || !key.Contains('.', StringComparison.Ordinal))
        {
            return CamelCase(key);
        }

        string[] segments = key.Split('.');
        for (int i = 0; i < segments.Length; i++)
        {
            segments[i] = CamelCase(segments[i]);
        }

        return string.Join('.', segments);
    }

    private static string CamelCase(string segment) =>
        segment.Length == 0 ? segment : JsonNamingPolicy.CamelCase.ConvertName(segment);

    private static int ResolveStatus(ProblemDetails problem, HttpContext httpContext)
    {
        if (problem.Status is int declared && declared >= StatusCodes.Status400BadRequest)
        {
            return declared;
        }

        int responseStatus = httpContext.Response.StatusCode;
        return responseStatus >= StatusCodes.Status400BadRequest
            ? responseStatus
            : StatusCodes.Status500InternalServerError;
    }

    private static bool HasCode(ProblemDetails problem) =>
        problem.Extensions.TryGetValue(CodeMember, out object? code) && code is string { Length: > 0 };

    private static bool IsDevelopment(HttpContext httpContext) =>
        httpContext.RequestServices?.GetService<IHostEnvironment>()?.IsDevelopment() == true;

    private static void NormalizeValidationKeys(HttpValidationProblemDetails problem)
    {
        IDictionary<string, string[]> errors = problem.Errors;
        if (errors.Count == 0)
        {
            return;
        }

        KeyValuePair<string, string[]>[] entries = [.. errors];
        errors.Clear();

        foreach ((string key, string[] messages) in entries)
        {
            string normalized = NormalizeValidationKey(key);
            errors[normalized] = errors.TryGetValue(normalized, out string[]? existing)
                ? [.. existing, .. messages]
                : messages;
        }
    }
}
