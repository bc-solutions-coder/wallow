using System.Security.Cryptography;
using Foundry.Identity.Domain.Identity;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Identity.Domain.Entities;

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

    private ScimConfiguration() { }

    private ScimConfiguration(
        TenantId tenantId,
        string bearerToken,
        string tokenPrefix,
        DateTime tokenExpiresAt,
        Guid createdByUserId)
    {
        Id = ScimConfigurationId.New();
        TenantId = tenantId;
        IsEnabled = false;
        BearerToken = bearerToken;
        TokenPrefix = tokenPrefix;
        TokenExpiresAt = tokenExpiresAt;
        AutoActivateUsers = true;
        DeprovisionOnDelete = false;
        SetCreated(DateTimeOffset.UtcNow, createdByUserId);
    }

    public static (ScimConfiguration Config, string PlainTextToken) Create(
        TenantId tenantId,
        Guid createdByUserId)
    {
        (string token, string prefix) = GenerateTokenAndPrefix();
        string plainTextToken = token;
        DateTime expiresAt = DateTime.UtcNow.AddYears(1);

        ScimConfiguration config = new ScimConfiguration(
            tenantId,
            HashToken(token),
            prefix,
            expiresAt,
            createdByUserId);

        return (config, plainTextToken);
    }

    public string RegenerateToken(Guid updatedByUserId)
    {
        (string token, string prefix) = GenerateTokenAndPrefix();
        string plainTextToken = token;

        BearerToken = HashToken(token);
        TokenPrefix = prefix;
        TokenExpiresAt = DateTime.UtcNow.AddYears(1);
        SetUpdated(DateTimeOffset.UtcNow, updatedByUserId);

        return plainTextToken;
    }

    public void Enable(Guid updatedByUserId)
    {
        if (IsEnabled)
        {
            throw new BusinessRuleException(
                "Identity.ScimAlreadyEnabled",
                "SCIM configuration is already enabled");
        }

        IsEnabled = true;
        SetUpdated(DateTimeOffset.UtcNow, updatedByUserId);
    }

    public void Disable(Guid updatedByUserId)
    {
        if (!IsEnabled)
        {
            throw new BusinessRuleException(
                "Identity.ScimAlreadyDisabled",
                "SCIM configuration is already disabled");
        }

        IsEnabled = false;
        SetUpdated(DateTimeOffset.UtcNow, updatedByUserId);
    }

    public void UpdateSettings(
        bool autoActivateUsers,
        string? defaultRole,
        bool deprovisionOnDelete,
        Guid updatedByUserId)
    {
        AutoActivateUsers = autoActivateUsers;
        DefaultRole = defaultRole;
        DeprovisionOnDelete = deprovisionOnDelete;
        SetUpdated(DateTimeOffset.UtcNow, updatedByUserId);
    }

    public void RecordSync(Guid updatedByUserId)
    {
        LastSyncAt = DateTime.UtcNow;
        SetUpdated(DateTimeOffset.UtcNow, updatedByUserId);
    }

    public bool IsTokenValid()
    {
        return IsEnabled && TokenExpiresAt > DateTime.UtcNow;
    }

    private static (string Token, string Prefix) GenerateTokenAndPrefix()
    {
        byte[] tokenBytes = new byte[32];
        RandomNumberGenerator.Fill(tokenBytes);
        string token = Convert.ToBase64String(tokenBytes);
        string prefix = token[..8];

        return (token, prefix);
    }

    private static string HashToken(string token)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(token);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
