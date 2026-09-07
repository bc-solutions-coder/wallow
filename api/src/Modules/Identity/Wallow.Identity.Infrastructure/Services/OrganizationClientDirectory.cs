using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Exposes organization-client ownership and management permission checks through
/// <see cref="IOrganizationClientDirectory"/>. Missing and foreign clients both return null.
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
