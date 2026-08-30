using System.Text.Json;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;

namespace Wallow.Identity.Infrastructure.Services;

public sealed class OpenIddictDeveloperAppService(
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictScopeManager scopeManager) : IDeveloperAppService
{
    public async Task<ConsentInfoDto?> GetConsentInfoAsync(
        string clientId,
        IReadOnlyCollection<string> requestedScopes,
        CancellationToken cancellationToken = default)
    {
        object? application = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        string? displayName = await applicationManager.GetDisplayNameAsync(application, cancellationToken);

        OpenIddictApplicationDescriptor descriptor = new();
        await applicationManager.PopulateAsync(descriptor, application, cancellationToken);

        string? logoUrl = null;
        if (descriptor.Properties.TryGetValue("logoUrl", out JsonElement logoElement))
        {
            logoUrl = logoElement.Deserialize<string>();
        }

        List<ConsentScopeDto> scopeDtos = [];
        foreach (string scopeName in requestedScopes)
        {
            object? scope = await scopeManager.FindByNameAsync(scopeName, cancellationToken);
            string? description = scope is not null
                ? await scopeManager.GetDescriptionAsync(scope, cancellationToken)
                : null;
            scopeDtos.Add(new ConsentScopeDto(scopeName, description));
        }

        return new ConsentInfoDto(clientId, displayName, logoUrl, scopeDtos);
    }
}
