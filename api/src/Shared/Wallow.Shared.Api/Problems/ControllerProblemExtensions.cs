using Microsoft.AspNetCore.Mvc;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Shared.Api.Problems;

/// <summary>
/// Creates controller errors with <c>this.Problem(SomeErrors.Entry)</c>.
/// </summary>
public static class ControllerProblemExtensions
{
    /// <summary>
    /// Uses the catalog entry's status and default detail unless <paramref name="detail"/> is supplied.
    /// <paramref name="retryAfter"/> sets the <c>Retry-After</c> header.
    /// </summary>
    public static ProblemResult Problem(
        this ControllerBase controller,
        ErrorCatalogEntry entry,
        string? detail = null,
        TimeSpan? retryAfter = null)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(entry);

        return new ProblemResult(entry.Kind.ToHttpStatusCode(), entry.Code, detail ?? entry.DefaultMessage, retryAfter);
    }
}
