using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;
using Wallow.Identity.Infrastructure.Authorization;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Deny-by-default function-level authorization (bead Wallow-pu6a.6.5, closing finding F13 and
/// guardrail R23 of the SDK review, which are the same finding: "No FallbackPolicy — a new
/// controller without [Authorize] is silently anonymous").
///
/// <para><see cref="PermissionAuthorizationPolicyProvider"/> today hardcodes its fallback policy
/// instead of returning the one <c>AuthorizationOptions.FallbackPolicy</c> declares, so the
/// deny-by-default rule is invisible at the registration site and a fork that configures a
/// stricter fallback (extra scheme, extra requirement) silently gets the hardcoded one instead.
/// The remedy is to delegate to the configured fallback and keep the deny-anonymous policy only
/// as the last-resort default when nothing is configured.</para>
///
/// <para><see cref="PermissionAuthorizationPolicyProviderTests"/> already covers the
/// permission-policy lookup and the no-configuration default; this file covers only the
/// delegation contract the fix adds. The companion assertion that the application actually
/// configures a fallback lives in <c>Wallow.Architecture.Tests.DenyByDefaultAuthorizationTests</c>,
/// because the wiring is inside a private registration method behind an OpenIddict/EF/Redis
/// composition root that cannot be built in a unit test.</para>
/// </summary>
public class FallbackAuthorizationPolicyTests
{
    /// <summary>A scheme name no default policy would produce, so the delegation is unambiguous.</summary>
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
