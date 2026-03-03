using Foundry.Identity.Infrastructure.Authorization;
using Foundry.Shared.Kernel.Identity.Authorization;

namespace Foundry.Identity.Tests.Infrastructure;

public class RolePermissionLookupTests
{
    private readonly RolePermissionLookup _lookup = new();

    [Fact]
    public void GetPermissions_AdminRole_ReturnsAllPermissions()
    {
        IEnumerable<string> roles = new[] { "admin" };

        IReadOnlyCollection<PermissionType> result = _lookup.GetPermissions(roles);

        PermissionType[] allPermissions = Enum.GetValues<PermissionType>();
        result.Should().BeEquivalentTo(allPermissions);
    }

    [Fact]
    public void GetPermissions_EmptyRoles_ReturnsEmpty()
    {
        IEnumerable<string> roles = Array.Empty<string>();

        IReadOnlyCollection<PermissionType> result = _lookup.GetPermissions(roles);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetPermissions_UnknownRole_ReturnsEmpty()
    {
        IEnumerable<string> roles = new[] { "nonexistent" };

        IReadOnlyCollection<PermissionType> result = _lookup.GetPermissions(roles);

        result.Should().BeEmpty();
    }
}
