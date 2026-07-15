using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Tests.Infrastructure;

public class RolePermissionLookupTests
{
    private readonly RolePermissionLookup _lookup = new RolePermissionLookup();

    [Fact]
    public void GetPermissions_AdminRole_ReturnsAllPermissions()
    {
        IEnumerable<string> roles = new[] { "admin" };

        IReadOnlyCollection<string> result = _lookup.GetPermissions(roles);

        string[] allExceptNone = PermissionType.All.Where(p => p != PermissionType.None).ToArray();
        result.Should().BeEquivalentTo(allExceptNone);
    }

    [Fact]
    public void GetPermissions_EmptyRoles_ReturnsEmpty()
    {
        IEnumerable<string> roles = [];

        IReadOnlyCollection<string> result = _lookup.GetPermissions(roles);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetPermissions_UnknownRole_ReturnsEmpty()
    {
        IEnumerable<string> roles = new[] { "nonexistent" };

        IReadOnlyCollection<string> result = _lookup.GetPermissions(roles);

        result.Should().BeEmpty();
    }
}
