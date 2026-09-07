using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Shared.Api.Problems;

/// <summary>
/// Builds errors through MVC's <see cref="ProblemDetailsFactory"/> at execution.
/// <see cref="ObjectResult.Value"/> initially holds a preliminary problem for inspection.
/// <see cref="RetryAfter"/> sets the <c>Retry-After</c> header in seconds, rounded up.
/// </summary>
public sealed class ProblemResult : ObjectResult
{
    /// <param name="statusCode">The HTTP status (400 or above).</param>
    /// <param name="code">The catalog code.</param>
    /// <param name="detail">The user-safe detail; the status-generic sentence when null.</param>
    /// <param name="retryAfter">How long the caller should wait before retrying, when the failure is temporary.</param>
    public ProblemResult(int statusCode, string code, string? detail, TimeSpan? retryAfter = null)
        : base(null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        StatusCode = statusCode;
        Code = code;
        Detail = detail;
        RetryAfter = retryAfter;
        Value = Preliminary(statusCode, code, detail);
    }

    /// <summary>The catalog code written as the <c>code</c> member.</summary>
    public string Code { get; }

    /// <summary>The detail passed in (before the contract fills or overrides it).</summary>
    public string? Detail { get; }

    /// <summary>The wait written as the <c>Retry-After</c> header, or null for no header.</summary>
    public TimeSpan? RetryAfter { get; }

    /// <inheritdoc/>
    public override Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (RetryAfter is { } wait)
        {
            long seconds = (long)Math.Ceiling(wait.TotalSeconds);
            context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        ProblemDetailsFactory factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        ProblemDetails problem = factory.CreateProblemDetails(
            context.HttpContext,
            statusCode: StatusCode,
            detail: Detail);
        problem.Extensions[ProblemContract.CodeMember] = Code;
        Value = problem;

        return base.ExecuteResultAsync(context);
    }

    private static ProblemDetails Preliminary(int statusCode, string code, string? detail)
    {
        ProblemDetails problem = new()
        {
            Status = statusCode,
            Type = ProblemContract.BlankType,
            Title = ProblemContract.TitleFor(statusCode),
            Detail = statusCode >= 500
                ? Kernel.Errors.SharedErrors.ServerError.DefaultMessage
                : detail ?? ProblemContract.GenericEntryFor(statusCode).DefaultMessage,
        };
        problem.Extensions[ProblemContract.CodeMember] = code;
        return problem;
    }
}
