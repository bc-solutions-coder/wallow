using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Identity's answer to "does client X belong to organization Y", exposed through
/// <see cref="IOrganizationClientDirectory"/> so a module hanging a sub-resource off the
/// org-scoped client surface (Branding) never reaches OpenIddict or Identity's persistence.
/// A missing client and a foreign client are the same null on purpose.
/// </summary>
public sealed class OrganizationClientDirectory(
    IRegisteredClientRepository registeredClients,
    IOrganizationAccessPolicy accessPolicy)
    : IOrganizationClientDirectory
{
    public async Task<OrganizationClientInfo?> FindAsync(
        Guid organizationId, string clientId, CancellationToken ct = default)
    {
        RegisteredClient? record = await registeredClients.GetByClientIdAsync(clientId, ct);
        if (record is null || record.OrganizationId != organizationId)
        {
            return null;
        }

        return new OrganizationClientInfo(
            record.ClientId,
            record.OrganizationId,
            record.Kind == RegisteredClientKind.Application
                ? OrganizationClientKind.Application
                : OrganizationClientKind.ServiceAccount);
    }

    public Task<bool> CanManageClientsAsync(
        Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        return accessPolicy.HasPermissionInOrganizationAsync(
            organizationId, userId, PermissionType.OrganizationClientsManage, ct);
    }
}
