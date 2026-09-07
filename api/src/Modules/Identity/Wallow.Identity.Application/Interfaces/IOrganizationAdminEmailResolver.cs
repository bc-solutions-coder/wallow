namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Resolves email addresses for active owners and active members with the admin role.
/// No recipients is a valid empty result; query failures may still propagate.
/// </summary>
public interface IOrganizationAdminEmailResolver
{
    Task<IReadOnlyList<string>> ResolveAsync(Guid organizationId, CancellationToken ct = default);
}
