using Microsoft.AspNetCore.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Domain.Entities;

public sealed class WallowUser : IdentityUser<Guid>
{
    public Guid TenantId { get; set; }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public bool MfaEnabled { get; private set; }

    public string? MfaMethod { get; private set; }

    public string? TotpSecretEncrypted { get; private set; }

    public string? BackupCodesHash { get; private set; }

    public bool HasPassword { get; private set; } = true;

    public DateTimeOffset? MfaGraceDeadline { get; private set; }

    private WallowUser() { } // EF Core

    public static WallowUser Create(
        Guid tenantId,
        string firstName,
        string lastName,
        string email,
        TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new BusinessRuleException(
                "Identity.FirstNameRequired",
                "First name cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new BusinessRuleException(
                "Identity.LastNameRequired",
                "Last name cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new BusinessRuleException(
                "Identity.EmailRequired",
                "Email cannot be empty");
        }

        WallowUser user = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow()
        };

        return user;
    }

    public void EnableMfa(string method, string encryptedSecret)
    {
        if (method != "totp")
        {
            throw new ArgumentException("Only 'totp' is supported as an MFA method.", nameof(method));
        }

        if (string.IsNullOrWhiteSpace(encryptedSecret))
        {
            throw new ArgumentException("Encrypted secret cannot be null or empty.", nameof(encryptedSecret));
        }

        MfaEnabled = true;
        MfaMethod = method;
        TotpSecretEncrypted = encryptedSecret;
    }

    public void DisableMfa()
    {
        MfaEnabled = false;
        MfaMethod = null;
        TotpSecretEncrypted = null;
        BackupCodesHash = null;
    }

    public void SetBackupCodes(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new ArgumentException("Backup codes hash cannot be null or empty.", nameof(hash));
        }

        BackupCodesHash = hash;
    }

    public void SetPasswordless()
    {
        HasPassword = false;
    }

    public void SetMfaGraceDeadline(DateTimeOffset deadline)
    {
        if (deadline <= DateTimeOffset.UtcNow)
        {
            throw new BusinessRuleException(
                "Identity.MfaGraceDeadlineMustBeFuture",
                "MFA grace deadline must be in the future");
        }

        MfaGraceDeadline = deadline;
    }

    public void UpdateName(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new BusinessRuleException(
                "Identity.FirstNameRequired",
                "First name cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new BusinessRuleException(
                "Identity.LastNameRequired",
                "Last name cannot be empty");
        }

        FirstName = firstName;
        LastName = lastName;
    }
}
