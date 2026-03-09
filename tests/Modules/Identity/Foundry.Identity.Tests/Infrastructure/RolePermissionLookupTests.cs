using Foundry.Identity.Infrastructure.Authorization;
using Foundry.Shared.Kernel.Identity.Authorization;

namespace Foundry.Identity.Tests.Infrastructure;

public class RolePermissionLookupTests
{
    private readonly RolePermissionLookup _lookup = new RolePermissionLookup();

    [Fact]
    public void GetPermissions_AdminRole_ReturnsAllPermissions()
    {
        IEnumerable<string> roles = new[] { "admin" };

        IReadOnlyCollection<string> result = _lookup.GetPermissions(roles);

        result.Should().BeEquivalentTo(PermissionType.All);
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
