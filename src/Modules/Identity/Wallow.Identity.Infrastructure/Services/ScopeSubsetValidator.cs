using System.Collections.Immutable;
using Wallow.Shared.Contracts.Identity;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Services;

public sealed class ScopeSubsetValidator(IOpenIddictApplicationManager applicationManager) : IScopeSubsetValidator
{
    public async Task<ScopeValidationResult> ValidateAsync(string serviceAccountId, IEnumerable<string> requestedScopes, CancellationToken ct)
    {
        object? application = await applicationManager.FindByClientIdAsync(serviceAccountId, ct);
        if (application is null)
        {
            return ScopeValidationResult.Failure($"Service account '{serviceAccountId}' not found");
        }

        ImmutableArray<string> permissions = await applicationManager.GetPermissionsAsync(application, ct);
        HashSet<string> permittedScopes = new(StringComparer.Ordinal);
        foreach (string permission in permissions)
        {
            if (permission.StartsWith(Permissions.Prefixes.Scope, StringComparison.Ordinal))
            {
                permittedScopes.Add(permission[Permissions.Prefixes.Scope.Length..]);
            }
        }

        List<string> disallowedScopes = requestedScopes
            .Where(scope => !permittedScopes.Contains(scope))
            .ToList();

        if (disallowedScopes.Count > 0)
        {
            return ScopeValidationResult.Failure(
                $"The following scopes are not permitted for this service account: {string.Join(", ", disallowedScopes)}");
        }

        return ScopeValidationResult.Success();
    }
}
