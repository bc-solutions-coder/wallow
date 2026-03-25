using System.Collections.Concurrent;
using Wallow.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

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

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
}
