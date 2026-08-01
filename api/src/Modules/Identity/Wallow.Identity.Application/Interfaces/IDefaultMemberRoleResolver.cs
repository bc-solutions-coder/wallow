namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// The role a new member of an organization starts with, however they arrived — self-enrollment,
/// an approved access request, or an accepted invitation.
/// </summary>
/// <remarks>
/// One resolver rather than a literal per join path: the organization's configured
/// <c>DefaultRoleId</c> only governs joining if every path asks for it, and an organization that
/// has never configured one still has to admit people.
/// </remarks>
public interface IDefaultMemberRoleResolver
{
    /// <summary>
    /// Resolves the organization's default member role, falling back to the platform's baseline
    /// member role when it has configured none.
    /// </summary>
    Task<Guid> ResolveAsync(Guid organizationId, CancellationToken ct = default);
}
