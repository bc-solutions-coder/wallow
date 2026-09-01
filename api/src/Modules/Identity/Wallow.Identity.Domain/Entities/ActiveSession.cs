using System.Globalization;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Domain.Entities;

/// <summary>
/// Ledger row for one interactive SSO sign-in. The row's <see cref="Entity{TId}.Id"/> doubles
/// as the OIDC <c>sid</c> (its Guid rendered in "N" format) stamped on the identity cookie and
/// id_tokens — the sessions API and token revocation both key off that equivalence.
/// </summary>
public sealed class ActiveSession : Entity<ActiveSessionId>
{
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public string SessionToken { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastActivityAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }

    /// <summary>The OIDC <c>sid</c> this row answers for — the one rendering of the equivalence.</summary>
    public string Sid => Id.Value.ToString("N", CultureInfo.InvariantCulture);

    // ReSharper disable once UnusedMember.Local
    private ActiveSession() { } // EF Core

    public static ActiveSession Create(Guid userId, Guid tenantId, TimeSpan sessionDuration, TimeProvider timeProvider)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        return new ActiveSession
        {
            Id = ActiveSessionId.New(),
            UserId = userId,
            TenantId = tenantId,
            SessionToken = Guid.NewGuid().ToString("N"),
            CreatedAt = now,
            LastActivityAt = now,
            ExpiresAt = now + sessionDuration,
            IsRevoked = false
        };
    }

    public bool IsExpired(TimeProvider timeProvider)
    {
        return ExpiresAt < timeProvider.GetUtcNow();
    }

    public void Touch(TimeProvider timeProvider)
    {
        LastActivityAt = timeProvider.GetUtcNow();
    }

    public void Revoke()
    {
        IsRevoked = true;
    }
}
