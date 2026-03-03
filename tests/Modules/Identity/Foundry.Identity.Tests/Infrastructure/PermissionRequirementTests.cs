using Foundry.Identity.Infrastructure.Authorization;

namespace Foundry.Identity.Tests.Infrastructure;

public class PermissionRequirementTests
{
    [Fact]
    public void Constructor_SetsPermission()
    {
        PermissionRequirement requirement = new PermissionRequirement("UsersRead");

        requirement.Permission.Should().Be("UsersRead");
    }
}
