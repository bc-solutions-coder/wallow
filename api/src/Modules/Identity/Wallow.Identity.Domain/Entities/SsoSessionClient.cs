using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Domain.Entities;

/// <summary>
/// Records that a relying party joined an SSO session: one row per (session id, client). The
/// authorize endpoint writes a row when it issues a code to a client, and the end-session page
/// reads the session's rows to know which relying parties to notify via front-channel logout.
/// Deliberately NOT tenant-scoped — one SSO session can span clients from different
/// organizations, and logout runs with no tenant context.
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
