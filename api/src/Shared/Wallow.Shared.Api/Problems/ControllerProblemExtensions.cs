using Microsoft.AspNetCore.Mvc;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Shared.Api.Problems;

/// <summary>
/// The controller-side entry point for an inline error: <c>this.Problem(SomeErrors.Entry)</c>.
/// </summary>
public static class ControllerProblemExtensions
{
    /// <summary>
    /// Builds a <see cref="ProblemResult"/> for a catalog entry, taking the status from the entry's
    /// kind and the detail from <paramref name="detail"/> or the entry's default sentence.
    /// </summary>
    public static ProblemResult Problem(this ControllerBase controller, ErrorCatalogEntry entry, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(entry);

        return new ProblemResult(entry.Kind.ToHttpStatusCode(), entry.Code, detail ?? entry.DefaultMessage);
    }
}
