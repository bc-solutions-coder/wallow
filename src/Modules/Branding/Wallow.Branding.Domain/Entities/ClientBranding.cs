using Wallow.Branding.Domain.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Branding.Domain.Entities;

public sealed class ClientBranding : Entity<ClientBrandingId>, ITenantScoped
{
    public string ClientId { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Tagline { get; private set; }
    public string? LogoStorageKey { get; private set; }
    public string? ThemeJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public TenantId TenantId { get; init; }

    // ReSharper disable once UnusedMember.Local
    private ClientBranding() { } // EF Core

    private ClientBranding(
        string clientId,
        string displayName,
        string? tagline,
        string? logoStorageKey,
        string? themeJson)
    {
        Id = ClientBrandingId.New();
        ClientId = clientId;
        DisplayName = displayName;
        Tagline = tagline;
        LogoStorageKey = logoStorageKey;
        ThemeJson = themeJson;
        CreatedAt = DateTime.UtcNow;
    }

    public static ClientBranding Create(
        string clientId,
        string displayName,
        string? tagline = null,
        string? logoStorageKey = null,
        string? themeJson = null)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new BusinessRuleException(
                "Branding.ClientBrandingClientIdRequired",
                "Client ID cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new BusinessRuleException(
                "Branding.ClientBrandingDisplayNameRequired",
                "Display name cannot be empty");
        }

        return new ClientBranding(clientId, displayName, tagline, logoStorageKey, themeJson);
    }

    public void Update(
        string displayName,
        string? tagline,
        string? logoStorageKey,
        string? themeJson)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new BusinessRuleException(
                "Branding.ClientBrandingDisplayNameRequired",
                "Display name cannot be empty");
        }

        DisplayName = displayName;
        Tagline = tagline;
        LogoStorageKey = logoStorageKey;
        ThemeJson = themeJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearLogo()
    {
        LogoStorageKey = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
