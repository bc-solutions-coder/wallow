using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// The one place an organization's <c>EnrollmentPolicy</c> is applied to a person who is not a
/// member of it yet.
/// </summary>
/// <remarks>
/// Deliberately not a branch inside a controller: the OIDC authorize path and registration both
/// reach it, and the preconditions it enforces — a verified email, a default role rather than an
/// inherited one, an existing membership decided by its own status — are security rules that must
/// not be able to diverge between the two.
/// </remarks>
public interface IUserEnrollmentService
{
    /// <summary>
    /// Applies the organization's enrollment policy to <paramref name="userId" /> and records
    /// whatever that policy admits. Idempotent: a person who already holds a membership gets the
    /// outcome their existing row describes, and no second row is written.
    /// </summary>
    Task<EnrollmentOutcome> EnrollAsync(Guid userId, Guid organizationId, CancellationToken ct = default);
}
