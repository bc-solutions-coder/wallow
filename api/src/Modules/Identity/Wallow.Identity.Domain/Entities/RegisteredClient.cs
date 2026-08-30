using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Domain.Entities;

/// <summary>
/// Wallow's own record of a client an organization registered: which organization owns it, what
/// kind it is, its lifecycle status and provenance. The OpenIddict application holds the OAuth
/// configuration (secret, redirect URIs, granted scopes); this row holds what OpenIddict has no
/// place for. Deliberately NOT tenant-scoped — an organization admin may address another
/// organization's clients through the access policy, which the tenant filter would defeat.
/// </summary>
public sealed class RegisteredClient : Entity<RegisteredClientId>
{
    public string ClientId { get; private set; } = string.Empty;
    public Guid OrganizationId { get; private set; }
    public RegisteredClientKind Kind { get; private set; }
    public RegisteredClientStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private RegisteredClient() { } // EF Core

    public static RegisteredClient Create(
        string clientId,
        Guid organizationId,
        RegisteredClientKind kind,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new BusinessRuleException("Identity.ClientIdRequired", "Client id cannot be empty");
        }

        if (organizationId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Identity.ClientOrganizationRequired",
                "A registered client must belong to an organization");
        }

        return new RegisteredClient
        {
            Id = RegisteredClientId.New(),
            ClientId = clientId,
            OrganizationId = organizationId,
            Kind = kind,
            Status = RegisteredClientStatus.Active,
            CreatedByUserId = createdByUserId,
            CreatedAt = timeProvider.GetUtcNow(),
        };
    }

    public void MarkUsed(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        LastUsedAt = timeProvider.GetUtcNow();
    }
}
