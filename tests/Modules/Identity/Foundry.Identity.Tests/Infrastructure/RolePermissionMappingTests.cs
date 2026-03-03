using Foundry.Identity.Infrastructure.Authorization;
using Foundry.Shared.Kernel.Identity.Authorization;

namespace Foundry.Identity.Tests.Infrastructure;

public class RolePermissionMappingTests
{
    [Fact]
    public void GetPermissions_AdminRole_ReturnsAllPermissions()
    {
        IEnumerable<string> roles = new[] { "admin" };

        IEnumerable<string> result = RolePermissionMapping.GetPermissions(roles);

        result.Should().BeEquivalentTo(PermissionType.All);
    }

    [Fact]
    public void GetPermissions_UserRole_ReturnsBasicPermissions()
    {
        IEnumerable<string> roles = new[] { "user" };

        List<string> result = RolePermissionMapping.GetPermissions(roles).ToList();

        result.Should().Contain(PermissionType.OrganizationsRead);
        result.Should().NotContain(PermissionType.UsersRead);
        result.Should().NotContain(PermissionType.BillingRead);
    }

    [Fact]
    public void GetPermissions_ManagerRole_ReturnsManagerPermissions()
    {
        IEnumerable<string> roles = new[] { "manager" };

        List<string> result = RolePermissionMapping.GetPermissions(roles).ToList();

        result.Should().Contain(PermissionType.UsersRead);
        result.Should().Contain(PermissionType.BillingRead);
        result.Should().Contain(PermissionType.OrganizationsRead);
        result.Should().Contain(PermissionType.OrganizationsManageMembers);
        result.Should().Contain(PermissionType.ApiKeysCreate);
        result.Should().Contain(PermissionType.SsoRead);
        result.Should().NotContain(PermissionType.UsersCreate);
        result.Should().NotContain(PermissionType.AdminAccess);
    }

    [Fact]
    public void GetPermissions_UnknownRole_ReturnsEmpty()
    {
        IEnumerable<string> roles = new[] { "unknown-role" };

        IEnumerable<string> result = RolePermissionMapping.GetPermissions(roles);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetPermissions_MultipleRoles_ReturnsCombinedDistinctPermissions()
    {
        IEnumerable<string> roles = new[] { "user", "manager" };

        List<string> result = RolePermissionMapping.GetPermissions(roles).ToList();

        result.Should().Contain(PermissionType.OrganizationsRead);
        result.Should().Contain(PermissionType.UsersRead);
        result.Should().Contain(PermissionType.BillingRead);
        result.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetPermissions_EmptyRoles_ReturnsEmpty()
    {
        IEnumerable<string> roles = Array.Empty<string>();

        IEnumerable<string> result = RolePermissionMapping.GetPermissions(roles);

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("ADMIN")]
    [InlineData("aDmIn")]
    public void GetPermissions_AdminRoleCaseInsensitive_ReturnsAllPermissions(string role)
    {
        IEnumerable<string> roles = new[] { role };

        IEnumerable<string> result = RolePermissionMapping.GetPermissions(roles);

        result.Should().BeEquivalentTo(PermissionType.All);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("MANAGER")]
    [InlineData("mAnAgEr")]
    public void GetPermissions_ManagerRoleCaseInsensitive_ReturnsSamePermissions(string role)
    {
        IEnumerable<string> roles = new[] { role };

        IEnumerable<string> result = RolePermissionMapping.GetPermissions(roles);

        IEnumerable<string> expected = RolePermissionMapping.GetPermissions(["manager"]);
        result.Should().BeEquivalentTo(expected);
    }
}
