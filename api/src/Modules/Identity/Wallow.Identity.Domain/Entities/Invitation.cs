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

        return new Invitation(tenantId, email, expiresAt, createdByUserId, timeProvider);
    }

    public void Accept(Guid userId, TimeProvider timeProvider)
    {
        if (Status != InvitationStatus.Pending)
        {
            throw new BusinessRuleException(
                "Identity.InvitationNotPending",
                $"Cannot accept invitation with status '{Status}'");
        }

        Status = InvitationStatus.Accepted;
        AcceptedByUserId = userId;
        SetUpdated(timeProvider.GetUtcNow(), userId);
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
