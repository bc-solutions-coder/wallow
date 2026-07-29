using System.Security.Claims;
using Wallow.Shared.Kernel.Extensions;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Global admin is a distinct, NON-assignable claim, never a role: holding the ordinary
/// tenant "admin" role must never make a caller a global admin. Reading the flag has to go
/// through ClaimsPrincipalExtensions (CONVENTIONS.md forbids raw FindFirst at call sites),
/// and only the literal value "true" may grant it, mirroring the is_operator flag.
/// </summary>
public sealed class GlobalAdminClaimTests
{
    [Fact]
    public void GlobalAdminClaimType_IsTheSnakeCaseIsGlobalAdminClaim()
    {
        ClaimsPrincipalExtensions.GlobalAdminClaimType.Should().Be(
            "is_global_admin",
            "the custom claims in this repo are snake_case and unprefixed (org_id, is_operator, ...)");
    }

    [Fact]
    public void IsGlobalAdmin_ClaimIsTrue_ReturnsTrue()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim(ClaimsPrincipalExtensions.GlobalAdminClaimType, "true"));

        principal.IsGlobalAdmin().Should().BeTrue();
    }

    [Theory]
    [InlineData("false")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("admin")]
    [InlineData("")]
    public void IsGlobalAdmin_ClaimIsNotLiteralTrue_ReturnsFalse(string value)
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim(ClaimsPrincipalExtensions.GlobalAdminClaimType, value));

        principal.IsGlobalAdmin().Should().BeFalse(
            "the claim value must be honoured, not merely its presence");
    }

    [Fact]
    public void IsGlobalAdmin_ClaimAbsent_ReturnsFalse()
    {
        ClaimsPrincipal principal = CreatePrincipal(new Claim("org_id", Guid.NewGuid().ToString()));

        principal.IsGlobalAdmin().Should().BeFalse();
    }

    [Fact]
    public void IsGlobalAdmin_NullPrincipal_ReturnsFalse()
    {
        ClaimsPrincipalExtensions.IsGlobalAdmin(null).Should().BeFalse();
    }

    [Fact]
    public void IsGlobalAdmin_AdminRoleWithoutTheClaim_ReturnsFalse()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim(ClaimTypes.Role, "admin"),
            new Claim("role", "admin"));

        principal.IsGlobalAdmin().Should().BeFalse(
            "an ordinary tenant admin role is assignable through UsersController and must never confer global admin");
    }

    [Fact]
    public void IsGlobalAdmin_OperatorClaimWithoutTheGlobalAdminClaim_ReturnsFalse()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim(ClaimsPrincipalExtensions.OperatorClaimType, "true"));

        principal.IsGlobalAdmin().Should().BeFalse(
            "the platform operator flag and the global administrator flag are separate concepts");
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));
}
