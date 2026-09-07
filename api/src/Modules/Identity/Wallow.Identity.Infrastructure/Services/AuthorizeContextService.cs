using OpenIddict.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Contracts.Branding;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Resolves branding and scope descriptions after redirect-URI validation and the client
/// state policy. Unknown clients, invalid redirects and policy refusals return null.
/// A registered redirect URI does not prove a live authorization transaction.
/// </summary>
public sealed class AuthorizeContextService(
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictScopeManager scopeManager,
    IClientAccessPolicy clientAccessPolicy,
    IClientTenantResolver clientTenantResolver,
    IClientBrandingProvider brandingProvider) : IAuthorizeContextService
{
    public async Task<AuthorizeContextDto?> ResolveAsync(
        string clientId,
        string redirectUri,
        IReadOnlyCollection<string> requestedScopes,
        CancellationToken cancellationToken = default)
    {
        object? application = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        if (!await applicationManager.ValidateRedirectUriAsync(application, redirectUri, cancellationToken))
        {
            return null;
        }

        ClientAccessRefusal? refusal = await clientAccessPolicy.EvaluateAsync(clientId, cancellationToken);
        if (refusal is not null)
        {
            return null;
        }

        bool firstParty = string.Equals(
            await applicationManager.GetConsentTypeAsync(application, cancellationToken),
            ConsentTypes.Implicit,
            StringComparison.Ordinal);

        PublicClientBranding? branding = await brandingProvider.FindAsync(clientId, cancellationToken);
        string displayName = branding?.DisplayName
            ?? await applicationManager.GetDisplayNameAsync(application, cancellationToken)
            ?? clientId;

        ClientTenantInfo? tenantInfo = await clientTenantResolver.ResolveAsync(clientId, cancellationToken);

        List<ConsentScopeDto> scopes = [];
        foreach (string scopeName in requestedScopes)
        {
            object? scope = await scopeManager.FindByNameAsync(scopeName, cancellationToken);
            string? description = scope is not null
                ? await scopeManager.GetDescriptionAsync(scope, cancellationToken)
                : null;
            scopes.Add(new ConsentScopeDto(scopeName, description));
        }

        return new AuthorizeContextDto(
            clientId,
            displayName,
            branding?.Tagline,
            branding?.LogoUrl,
            branding?.ThemeJson,
            tenantInfo?.TenantName,
            firstParty,
            scopes);
    }
}
