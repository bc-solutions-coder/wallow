using OpenIddict.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Extensions;

namespace Wallow.Identity.Infrastructure.Services;

public sealed class ClientTenantResolver(
    IOpenIddictApplicationManager applicationManager,
    IOrganizationService organizationService) : IClientTenantResolver
{
    public async Task<ClientTenantInfo?> ResolveAsync(string clientId, CancellationToken ct = default)
    {
        object? application = await applicationManager.FindByClientIdAsync(clientId, ct);
        if (application is null)
        {
            return null;
        }

        OpenIddictApplicationDescriptor descriptor = new();
        await applicationManager.PopulateAsync(descriptor, application, ct);

        string? tenantIdString = descriptor.GetTenantId();
        if (tenantIdString is null || !Guid.TryParse(tenantIdString, out Guid tenantId))
        {
            // Client exists but has no tenant association — return empty tenant
            return new ClientTenantInfo(Guid.Empty, null);
        }

        OrganizationDto? organization = await organizationService.GetOrganizationByIdAsync(tenantId, ct);

        return new ClientTenantInfo(tenantId, organization?.Name);
    }
}
