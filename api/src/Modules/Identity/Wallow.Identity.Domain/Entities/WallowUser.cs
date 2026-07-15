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

    public int MfaFailedAttempts { get; private set; }

    public DateTimeOffset? MfaLockoutEnd { get; private set; }

    public int MfaLockoutCount { get; private set; }

    public string? PendingEmail { get; private set; }

    public DateTimeOffset? PendingEmailExpiry { get; private set; }

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

    public void RecordMfaFailure(int maxAttempts, TimeProvider timeProvider)
    {
        MfaFailedAttempts++;

        if (MfaFailedAttempts >= maxAttempts)
        {
            // Exponential backoff: 15min * 2^lockoutCount, capped at 24 hours
            double multiplier = Math.Pow(2, MfaLockoutCount);
            TimeSpan duration = TimeSpan.FromMinutes(15 * multiplier);
            TimeSpan maxDuration = TimeSpan.FromHours(24);
            if (duration > maxDuration)
            {
                duration = maxDuration;
            }

            MfaLockoutEnd = timeProvider.GetUtcNow() + duration;
            MfaLockoutCount++;
        }
    }

    public void ResetMfaAttempts()
    {
        MfaFailedAttempts = 0;
        MfaLockoutEnd = null;
    }

    public bool IsMfaLockedOut(TimeProvider timeProvider)
    {
        return MfaLockoutEnd is not null && MfaLockoutEnd > timeProvider.GetUtcNow();
    }

    public void InitiateEmailChange(string newEmail, DateTimeOffset expiry, TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(newEmail))
        {
            throw new BusinessRuleException(
                "Identity.EmailRequired",
                "New email cannot be empty");
        }

        if (expiry <= timeProvider.GetUtcNow())
        {
            throw new BusinessRuleException(
                "Identity.ExpiryMustBeFuture",
                "Email change expiry must be in the future");
        }

        PendingEmail = newEmail;
        PendingEmailExpiry = expiry;
    }

    public void ConfirmEmailChange()
    {
        if (PendingEmail is null)
        {
            throw new BusinessRuleException(
                "Identity.NoPendingEmailChange",
                "No pending email change to confirm");
        }

        Email = PendingEmail;
        UserName = PendingEmail;
        PendingEmail = null;
        PendingEmailExpiry = null;
    }

    public void ClearPendingEmailChange()
    {
        PendingEmail = null;
        PendingEmailExpiry = null;
    }
}
