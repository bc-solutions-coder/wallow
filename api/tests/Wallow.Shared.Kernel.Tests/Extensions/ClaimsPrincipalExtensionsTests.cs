using System.Security.Claims;
using Wallow.Shared.Kernel.Extensions;

namespace Wallow.Shared.Kernel.Tests.Extensions;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetSessionId_ReturnsSidClaimValue()
    {
        Claim[] claims = [new Claim(ClaimsPrincipalExtensions.SessionIdClaimType, "abc123")];
        ClaimsPrincipal principal = new(new ClaimsIdentity(claims));

        principal.GetSessionId().Should().Be("abc123");
    }

    [Fact]
    public void GetSessionId_WithoutSidClaim_ReturnsNull()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity());

        principal.GetSessionId().Should().BeNull();
    }

    [Fact]
    public void GetSessionId_NullPrincipal_ReturnsNull()
    {
        ClaimsPrincipal? principal = null;

        principal.GetSessionId().Should().BeNull();
    }
}
