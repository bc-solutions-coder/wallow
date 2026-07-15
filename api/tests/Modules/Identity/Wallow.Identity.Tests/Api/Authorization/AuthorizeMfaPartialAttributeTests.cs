using Microsoft.AspNetCore.Authorization;
using Wallow.Identity.Api.Authorization;

namespace Wallow.Identity.Tests.Api.Authorization;

public class AuthorizeMfaPartialAttributeTests
{
    [Fact]
    public void Constructor_SetsPolicyToMfaPartial()
    {
        AuthorizeMfaPartialAttribute attribute = new();

        attribute.Policy.Should().Be("MfaPartial");
    }

    [Fact]
    public void Attribute_InheritsFromAuthorizeAttribute()
    {
        AuthorizeMfaPartialAttribute attribute = new();

        attribute.Should().BeAssignableTo<AuthorizeAttribute>();
    }
}
