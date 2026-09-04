using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Shared.Api.Problems;

/// <summary>
/// An error action result carrying a status, a catalog code and a user-safe detail. The response
/// body is built through MVC's <see cref="ProblemDetailsFactory"/> when the result executes, so it
/// passes through <see cref="ProblemContract.Customize"/> like every other problem; <see cref="ObjectResult.Value"/>
/// holds an equivalent preliminary <see cref="ProblemDetails"/> so unit tests can inspect the
/// result without executing it.
/// </summary>
public sealed class ProblemResult : ObjectResult
{
    /// <summary>Initialises the result.</summary>
    /// <param name="statusCode">The HTTP status (400 or above).</param>
    /// <param name="code">The catalog code.</param>
    /// <param name="detail">The user-safe detail; the status-generic sentence when null.</param>
    public ProblemResult(int statusCode, string code, string? detail)
        : base(null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        StatusCode = statusCode;
        Code = code;
        Detail = detail;
        Value = Preliminary(statusCode, code, detail);
    }

    /// <summary>The catalog code written as the <c>code</c> member.</summary>
    public string Code { get; }

    /// <summary>The detail passed in (before the contract fills or overrides it).</summary>
    public string? Detail { get; }

    /// <inheritdoc/>
    public override Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

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
