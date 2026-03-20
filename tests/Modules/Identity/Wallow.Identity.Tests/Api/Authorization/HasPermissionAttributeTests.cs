using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Tests.Api.Authorization;

public class HasPermissionAttributeTests
{
    [Fact]
    public void Constructor_WithPermission_SetsPolicy()
    {
        HasPermissionAttribute attribute = new(PermissionType.UsersRead);

        attribute.Policy.Should().Be("UsersRead");
    }

    [Fact]
    public void Constructor_WithDifferentPermission_SetsCorrectPolicy()
    {
        HasPermissionAttribute attribute = new(PermissionType.OrganizationsCreate);

        attribute.Policy.Should().Be("OrganizationsCreate");
    }

    [Theory]
    [InlineData(PermissionType.UsersRead)]
    [InlineData(PermissionType.UsersCreate)]
    [InlineData(PermissionType.UsersUpdate)]
    [InlineData(PermissionType.RolesRead)]
    [InlineData(PermissionType.RolesUpdate)]
    [InlineData(PermissionType.OrganizationsRead)]
    [InlineData(PermissionType.OrganizationsCreate)]
    [InlineData(PermissionType.OrganizationsManageMembers)]
    [InlineData(PermissionType.SsoRead)]
    [InlineData(PermissionType.SsoManage)]
    [InlineData(PermissionType.ApiKeysRead)]
    [InlineData(PermissionType.ApiKeysCreate)]
    [InlineData(PermissionType.ApiKeysUpdate)]
    [InlineData(PermissionType.ApiKeysDelete)]
    public void Constructor_WithVariousPermissions_SetsCorrectPolicy(string permission)
    {
        HasPermissionAttribute attribute = new(permission);

        attribute.Policy.Should().Be(permission);
    }

    [Fact]
    public void Attribute_CanBeAppliedToClassAndMethod()
    {
        AttributeUsageAttribute? usage = typeof(HasPermissionAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .FirstOrDefault() as AttributeUsageAttribute;

        usage.Should().NotBeNull();
        usage.ValidOn.Should().HaveFlag(AttributeTargets.Class);
        usage.ValidOn.Should().HaveFlag(AttributeTargets.Method);
        usage.AllowMultiple.Should().BeTrue();
    }
}
