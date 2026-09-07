using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Evaluates self-service enrollment and existing membership state for an organization.
/// </summary>
public interface IUserEnrollmentService
{
    /// <summary>
    /// Returns the existing membership outcome without adding another row. A spent denial
    /// is reevaluated under current policy and may reuse the row for enrollment or a request.
    /// </summary>
    Task<EnrollmentOutcome> EnrollAsync(Guid userId, Guid organizationId, CancellationToken ct = default);
}
