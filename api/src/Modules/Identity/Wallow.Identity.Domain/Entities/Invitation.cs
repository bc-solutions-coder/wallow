using System.Security.Cryptography;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Domain.Entities;

public sealed class Invitation : AggregateRoot<InvitationId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public string Email { get; private set; } = string.Empty;
    public string Token { get; private set; } = string.Empty;
    public InvitationStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private Invitation() { } // EF Core

    private Invitation(
        TenantId tenantId,
        string email,
        DateTimeOffset expiresAt,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        Id = InvitationId.New();
        TenantId = tenantId;
        Email = email;
        Token = GenerateToken();
        Status = InvitationStatus.Pending;
        ExpiresAt = expiresAt;
        SetCreated(timeProvider.GetUtcNow(), createdByUserId);
    }

    public static Invitation Create(
        TenantId tenantId,
        string email,
        DateTimeOffset expiresAt,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new BusinessRuleException(
                "Identity.InvitationEmailRequired",
                "Invitation email cannot be empty");
        }

        return new Invitation(
            TenantScope.Require(tenantId, nameof(Invitation)),
            email,
            expiresAt,
            createdByUserId,
            timeProvider);
    }

    /// <summary>
    /// Settles the invitation onto the accepting user. Expiry is checked here rather than left to
    /// the sweep: between an invitation lapsing and the next sweep run, Pending is not the same
    /// thing as live, and the window is as long as the job's interval.
    /// </summary>
    public void Accept(Guid userId, TimeProvider timeProvider)
    {
        if (Status != InvitationStatus.Pending)
        {
            throw new BusinessRuleException(
                "Identity.InvitationNotPending",
                $"Cannot accept invitation with status '{Status}'");
        }

        if (timeProvider.GetUtcNow() >= ExpiresAt)
        {
            Status = InvitationStatus.Expired;

            throw new BusinessRuleException(
                "Identity.InvitationExpired",
                "This invitation has expired");
        }

        Status = InvitationStatus.Accepted;
        AcceptedByUserId = userId;
        SetUpdated(timeProvider.GetUtcNow(), userId);
    }

    /// <summary>
    /// Pushes the expiry out on the invitation already outstanding for this address. Re-inviting
    /// refreshes the one live token rather than minting a second: <see cref="Revoke"/> acts on a
    /// single invitation by id, so a second token is one the admin cannot see to revoke.
    /// </summary>
    public void Renew(DateTimeOffset expiresAt, Guid actorId, TimeProvider timeProvider)
    {
        if (Status != InvitationStatus.Pending)
        {
            throw new BusinessRuleException(
                "Identity.InvitationNotPending",
                $"Cannot renew invitation with status '{Status}'");
        }

        ExpiresAt = expiresAt;
        SetUpdated(timeProvider.GetUtcNow(), actorId);
    }

    public void Revoke(Guid actorId, TimeProvider timeProvider)
    {
        if (Status != InvitationStatus.Pending)
        {
            throw new BusinessRuleException(
                "Identity.InvitationNotPending",
                $"Cannot revoke invitation with status '{Status}'");
        }

        Status = InvitationStatus.Revoked;
        SetUpdated(timeProvider.GetUtcNow(), actorId);
    }

    public void MarkExpired()
    {
        if (Status != InvitationStatus.Pending)
        {
            throw new BusinessRuleException(
                "Identity.InvitationNotPending",
                $"Cannot expire invitation with status '{Status}'");
        }

        Status = InvitationStatus.Expired;
    }

    // Generates a 32-byte base64url-encoded token
    private static string GenerateToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
