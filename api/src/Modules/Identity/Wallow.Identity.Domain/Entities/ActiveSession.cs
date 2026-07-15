using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Domain.Entities;

public sealed class ActiveSession : Entity<ActiveSessionId>
{
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public string SessionToken { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastActivityAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }

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
