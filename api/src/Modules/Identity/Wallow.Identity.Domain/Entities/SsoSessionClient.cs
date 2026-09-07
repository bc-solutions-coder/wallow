using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Domain.Entities;

/// <summary>
/// Client participation in an SSO session, unique by (sid, client). Authorization
/// records participation; logout uses it to notify clients. No tenant filter applies
/// because one session can span organizations and logout may have no tenant context.
/// </summary>
public sealed class SsoSessionClient : Entity<SsoSessionClientId>
{
    public string Sid { get; private set; } = string.Empty;
    public string ClientId { get; private set; } = string.Empty;
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private SsoSessionClient() { } // EF Core

    public static SsoSessionClient Create(string sid, string clientId, Guid userId, TimeProvider timeProvider)
    {
        return new SsoSessionClient
        {
            Id = SsoSessionClientId.New(),
            Sid = sid,
            ClientId = clientId,
            UserId = userId,
            CreatedAt = timeProvider.GetUtcNow()
        };
    }
}
