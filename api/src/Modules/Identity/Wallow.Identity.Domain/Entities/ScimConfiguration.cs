using System.Security.Cryptography;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Domain.ValueObjects;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Domain.Entities;

public sealed class ScimConfiguration : AggregateRoot<ScimConfigurationId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public bool IsEnabled { get; private set; }
    public string BearerToken { get; private set; } = string.Empty;
    public string TokenPrefix { get; private set; } = string.Empty;
    public DateTime TokenExpiresAt { get; private set; }
    public DateTime? LastSyncAt { get; private set; }
    public bool AutoActivateUsers { get; private set; }
    public string? DefaultRole { get; private set; }
    public bool DeprovisionOnDelete { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private ScimConfiguration() { } // EF Core

    private ScimConfiguration(
        TenantId tenantId,
        string bearerToken,
        string tokenPrefix,
        DateTime tokenExpiresAt,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        Id = ScimConfigurationId.New();
        TenantId = tenantId;
        IsEnabled = false;
        BearerToken = bearerToken;
        TokenPrefix = tokenPrefix;
        TokenExpiresAt = tokenExpiresAt;
        AutoActivateUsers = true;
        DeprovisionOnDelete = false;
        SetCreated(timeProvider.GetUtcNow(), createdByUserId);
    }

    public static (ScimConfiguration Config, string PlainTextToken) Create(
        TenantId tenantId,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        (string token, string prefix) = GenerateTokenAndPrefix();
        string plainTextToken = token;
        DateTime expiresAt = timeProvider.GetUtcNow().UtcDateTime.AddYears(1);

        ScimConfiguration config = new(
            tenantId,
            TokenHash.Compute(token),
            prefix,
            expiresAt,
            createdByUserId,
            timeProvider);

        return (config, plainTextToken);
    }

    public string RegenerateToken(Guid updatedByUserId, TimeProvider timeProvider)
    {
        (string token, string prefix) = GenerateTokenAndPrefix();
        string plainTextToken = token;

        BearerToken = TokenHash.Compute(token);
        TokenPrefix = prefix;
        TokenExpiresAt = timeProvider.GetUtcNow().UtcDateTime.AddYears(1);
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);

        return plainTextToken;
    }

    public void Enable(Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (IsEnabled)
        {
            throw new BusinessRuleException(
                "Identity.ScimAlreadyEnabled",
                "SCIM configuration is already enabled");
        }

        IsEnabled = true;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public void Disable(Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (!IsEnabled)
        {
            throw new BusinessRuleException(
                "Identity.ScimAlreadyDisabled",
                "SCIM configuration is already disabled");
        }

        IsEnabled = false;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public void UpdateSettings(
        bool autoActivateUsers,
        string? defaultRole,
        bool deprovisionOnDelete,
        Guid updatedByUserId,
        TimeProvider timeProvider)
    {
        AutoActivateUsers = autoActivateUsers;
        DefaultRole = defaultRole;
        DeprovisionOnDelete = deprovisionOnDelete;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public void RecordSync(Guid updatedByUserId, TimeProvider timeProvider)
    {
        LastSyncAt = timeProvider.GetUtcNow().UtcDateTime;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public bool IsTokenValid(TimeProvider timeProvider)
    {
        return IsEnabled && TokenExpiresAt > timeProvider.GetUtcNow().UtcDateTime;
    }

    private static (string Token, string Prefix) GenerateTokenAndPrefix()
    {
        byte[] tokenBytes = new byte[32];
        RandomNumberGenerator.Fill(tokenBytes);
        string token = Convert.ToBase64String(tokenBytes);
        string prefix = token[..8];

        return (token, prefix);
    }

}
