using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Infrastructure.Authorization;

public class PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider = new(options);
    private readonly ConcurrentDictionary<string, AuthorizationPolicy> _policyCache = new();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (PermissionType.All.Contains(policyName))
        {
            AuthorizationPolicy policy = _policyCache.GetOrAdd(policyName, name =>
                new AuthorizationPolicyBuilder()
                    .AddRequirements(new PermissionRequirement(name))
                    .Build());

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallbackProvider.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _fallbackProvider.GetDefaultPolicyAsync();

    /// <summary>
    /// Returns the fallback policy <see cref="AuthorizationOptions.FallbackPolicy"/> declares, so a
    /// fork configuring a stricter one is honoured. Falls back to deny-anonymous when nothing is
    /// configured: <see cref="AuthorizationOptions.FallbackPolicy"/> defaults to null, and a null
    /// fallback means an endpoint without authorization metadata is served anonymously.
    /// </summary>
    public async Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        await _fallbackProvider.GetFallbackPolicyAsync()
            ?? new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
}
