using Foundry.Identity.Infrastructure.Authorization;
using Foundry.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;

namespace Foundry.Identity.Tests.Infrastructure;

public class PermissionAuthorizationPolicyProviderTests
{
    private readonly PermissionAuthorizationPolicyProvider _provider;

    public PermissionAuthorizationPolicyProviderTests()
    {
        IOptions<AuthorizationOptions> options = Options.Create(new AuthorizationOptions());
        _provider = new PermissionAuthorizationPolicyProvider(options);
    }

    [Fact]
    public async Task GetPolicyAsync_WithPermissionTypeName_ReturnsPolicyWithRequirement()
    {
        string policyName = PermissionType.UsersRead.ToString();

        AuthorizationPolicy? result = await _provider.GetPolicyAsync(policyName);

        result.Should().NotBeNull();
        result.Requirements.Should().ContainSingle()
            .Which.Should().BeOfType<PermissionRequirement>()
            .Which.Permission.Should().Be(policyName);
    }

    [Fact]
    public async Task GetPolicyAsync_WithNonPermissionName_DelegatesToFallback()
    {
        string policyName = "SomeOtherPolicy";

        AuthorizationPolicy? result = await _provider.GetPolicyAsync(policyName);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDefaultPolicyAsync_ReturnsFallbackDefault()
    {
        AuthorizationPolicy result = await _provider.GetDefaultPolicyAsync();

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFallbackPolicyAsync_ReturnsFallbackPolicy()
    {
        AuthorizationPolicy? result = await _provider.GetFallbackPolicyAsync();

        result.Should().NotBeNull();
        result.Requirements.Should().ContainSingle()
            .Which.Should().BeOfType<DenyAnonymousAuthorizationRequirement>();
    }
}
