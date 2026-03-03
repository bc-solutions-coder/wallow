using Foundry.Shared.Kernel.Identity.Authorization;

namespace Foundry.Identity.Tests.Infrastructure;

public class HasPermissionAttributeTests
{
    [Fact]
    public void Constructor_SetsPolicy_ToPermissionTypeName()
    {
        HasPermissionAttribute attribute = new HasPermissionAttribute(PermissionType.UsersRead);

        attribute.Policy.Should().Be("UsersRead");
    }

    [Fact]
    public void Constructor_WithDifferentPermission_SetsCorrectPolicy()
    {
        HasPermissionAttribute attribute = new HasPermissionAttribute(PermissionType.UsersCreate);

        attribute.Policy.Should().Be("UsersCreate");
    }
}
