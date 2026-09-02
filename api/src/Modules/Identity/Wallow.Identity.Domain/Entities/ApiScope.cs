using Wallow.Identity.Domain.Errors;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Domain.Entities;

/// <summary>
/// System-defined API scope that can be assigned to service accounts.
/// Scopes map to permissions for OAuth2 client credentials flow.
/// </summary>
public sealed class ApiScope : Entity<ApiScopeId>
{
    /// <summary>
    /// Unique scope code (e.g., "users.read", "storage.write").
    /// </summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>
    /// Human-readable display name (e.g., "Read Users").
    /// </summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// Category for grouping scopes in UI (e.g., "Identity", "Storage").
    /// </summary>
    public string Category { get; private set; } = string.Empty;

    /// <summary>
    /// Optional description of what this scope grants access to.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// If true, this scope is included by default when creating new service accounts.
    /// </summary>
    public bool IsDefault { get; private set; }

    /// <summary>
    /// If true, only the platform's own (first-party) clients may hold this scope; a developer
    /// application registered by an organization cannot be granted it.
    /// </summary>
    public bool PlatformOnly { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private ApiScope() { } // EF Core

    private ApiScope(
        string code,
        string displayName,
        string category,
        string? description,
        bool isDefault,
        bool platformOnly)
    {
        Id = ApiScopeId.New();
        Code = code;
        DisplayName = displayName;
        Category = category;
        Description = description;
        IsDefault = isDefault;
        PlatformOnly = platformOnly;
    }

    public static ApiScope Create(
        string code,
        string displayName,
        string category,
        string? description = null,
        bool isDefault = false,
        bool platformOnly = false)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BusinessRuleException(IdentityErrors.ScopeCodeRequired);
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new BusinessRuleException(IdentityErrors.ScopeDisplayNameRequired);
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new BusinessRuleException(IdentityErrors.ScopeCategoryRequired);
        }

        return new ApiScope(code, displayName, category, description, isDefault, platformOnly);
    }
}
