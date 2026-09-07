namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Resolves the role used for self-enrollment, approval, and invitation acceptance.
/// </summary>
public interface IDefaultMemberRoleResolver
{
    /// <summary>
    /// Uses the configured role when it exists, otherwise the baseline user role.
    /// Throws when the baseline role is also missing.
    /// </summary>
    Task<Guid> ResolveAsync(Guid organizationId, CancellationToken ct = default);
}
