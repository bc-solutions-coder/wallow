using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Tests.Infrastructure;

public class HasPermissionAttributeTests
{
    [Fact]
    public void Constructor_SetsPolicy_ToPermissionTypeName()
    {
        HasPermissionAttribute attribute = new(PermissionType.UsersRead);

        attribute.Policy.Should().Be("UsersRead");
    }

    [Fact]
    public void Constructor_WithDifferentPermission_SetsCorrectPolicy()
    {
        HasPermissionAttribute attribute = new(PermissionType.UsersCreate);

        attribute.Policy.Should().Be("UsersCreate");
    }
}
