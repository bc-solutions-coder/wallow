using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Copies the <see cref="ApiScope"/> catalog and protocol scope descriptions into OpenIddict for consent display.
/// </summary>
public sealed partial class OpenIddictScopeSyncService(
    IdentityDbContext dbContext,
    IOpenIddictScopeManager scopeManager,
    ILogger<OpenIddictScopeSyncService> logger)
{
    private static readonly (string Name, string DisplayName, string Description)[] _protocolScopes =
    [
        ("openid", "Sign you in", "Confirm your identity to this application"),
        ("profile", "Your profile", "Your name and profile details"),
        ("email", "Your email address", "The email address on your account"),
        ("roles", "Your roles", "The roles you hold in this organization"),
        ("offline_access", "Stay signed in",
            "Keep you signed in to this application without asking again"),
    ];

    public async Task SyncAsync(CancellationToken ct = default)
    {
        int written = 0;

        foreach ((string name, string displayName, string description) in _protocolScopes)
        {
            written += await UpsertAsync(name, displayName, description, ct) ? 1 : 0;
        }

        List<ApiScope> apiScopes = await dbContext.ApiScopes.ToListAsync(ct);
        foreach (ApiScope scope in apiScopes)
        {
            written += await UpsertAsync(scope.Code, scope.DisplayName, scope.Description, ct) ? 1 : 0;
        }

        LogScopesSynced(written, apiScopes.Count + _protocolScopes.Length);
    }

    /// <summary>
    /// Updates changed descriptions so catalog edits reach consent prompts on the next seed run.
    /// </summary>
    private async Task<bool> UpsertAsync(
        string name, string displayName, string? description, CancellationToken ct)
    {
        object? existing = await scopeManager.FindByNameAsync(name, ct);

        if (existing is null)
        {
            await scopeManager.CreateAsync(
                new OpenIddictScopeDescriptor
                {
                    Name = name,
                    DisplayName = displayName,
                    Description = description
                },
                ct);

            LogScopeCreated(name);
            return true;
        }

        OpenIddictScopeDescriptor descriptor = new();
        await scopeManager.PopulateAsync(descriptor, existing, ct);

        if (string.Equals(descriptor.DisplayName, displayName, StringComparison.Ordinal)
            && string.Equals(descriptor.Description, description, StringComparison.Ordinal))
        {
            return false;
        }

        descriptor.DisplayName = displayName;
        descriptor.Description = description;
        await scopeManager.UpdateAsync(existing, descriptor, ct);

        LogScopeUpdated(name);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created OpenIddict scope {ScopeName}")]
    private partial void LogScopeCreated(string scopeName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated OpenIddict scope {ScopeName}")]
    private partial void LogScopeUpdated(string scopeName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Synced {WrittenCount} of {TotalCount} OpenIddict scopes")]
    private partial void LogScopesSynced(int writtenCount, int totalCount);
}
