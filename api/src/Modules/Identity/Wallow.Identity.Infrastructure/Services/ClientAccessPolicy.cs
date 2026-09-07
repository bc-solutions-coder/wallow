using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Checks client suspension before organization archive or suspension.
/// Missing client or organization records produce no refusal here.
/// </summary>
public sealed class ClientAccessPolicy(
    IRegisteredClientRepository registeredClients,
    IOrganizationRepository organizations) : IClientAccessPolicy
{
    public async Task<ClientAccessRefusal?> EvaluateAsync(string? clientId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(clientId))
        {
            return null;
        }

        RegisteredClient? registered = await registeredClients.GetByClientIdAsync(clientId, ct);
        if (registered is null)
        {
            // First-party or unknown: neither is this policy's to refuse.
            return null;
        }

        if (registered.Status == RegisteredClientStatus.Suspended)
        {
            return new ClientAccessRefusal("client_suspended", "The client is suspended.");
        }

        if (registered.IsPlatformSuspended)
        {
            return new ClientAccessRefusal(
                "client_suspended_by_platform", "The client is suspended by the platform.");
        }

        Organization? organization = await organizations.GetByIdAsync(
            OrganizationId.Create(registered.OrganizationId), ct);
        if (organization is null)
        {
            return null;
        }

        if (!organization.IsActive)
        {
            return new ClientAccessRefusal(
                "organization_archived", "The client's organization is archived.");
        }

        if (organization.IsPlatformSuspended)
        {
            return new ClientAccessRefusal(
                "organization_suspended_by_platform",
                "The client's organization is suspended by the platform.");
        }

        return null;
    }
}
