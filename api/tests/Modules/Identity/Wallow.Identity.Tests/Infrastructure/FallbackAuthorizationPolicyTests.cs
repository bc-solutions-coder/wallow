using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;
using Wallow.Identity.Infrastructure.Authorization;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Checks that the permission-policy provider returns a configured fallback and retains its requirements.
/// </summary>
public class FallbackAuthorizationPolicyTests
{
    /// <summary>
    /// Distinct scheme used to identify the configured fallback.
    /// </summary>
    private const string ConfiguredFallbackScheme = "wallow-fallback-scheme";

    [Fact]
    public async Task GetFallbackPolicyAsync_ShouldReturn_TheConfiguredFallbackPolicy()
    {
        AuthorizationOptions options = new()
        {
            FallbackPolicy = new AuthorizationPolicyBuilder(ConfiguredFallbackScheme)
                .RequireAuthenticatedUser()
                .Build()
        };

        PermissionAuthorizationPolicyProvider provider = new(Options.Create(options));

        AuthorizationPolicy? policy = await provider.GetFallbackPolicyAsync();

        policy.Should().NotBeNull();
        policy.AuthenticationSchemes.Should().Contain(
            ConfiguredFallbackScheme,
            "the provider must return the fallback policy AuthorizationOptions declares, not a " +
            "policy it builds itself. A hardcoded fallback makes the deny-by-default rule " +
            "invisible where authorization is registered, and silently discards any fallback a " +
            "fork configures — including a stricter one");
    }

    [Fact]
    public async Task GetFallbackPolicyAsync_WithNoConfiguredFallback_ShouldStillDenyAnonymous()
    {
        PermissionAuthorizationPolicyProvider provider = new(Options.Create(new AuthorizationOptions()));

        AuthorizationPolicy? policy = await provider.GetFallbackPolicyAsync();

        policy.Should().NotBeNull(
            "delegating to the configured fallback must not reintroduce the hole this closes: " +
            "AuthorizationOptions.FallbackPolicy defaults to null, and a null fallback is exactly " +
            "the 'endpoint without [Authorize] is anonymous' behaviour F13 reports");
        policy.Requirements.Should().ContainSingle()
            .Which.Should().BeOfType<DenyAnonymousAuthorizationRequirement>(
                "the last-resort fallback must require an authenticated user");
    }
}
