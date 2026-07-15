using Wallow.Identity.Infrastructure.Authorization;

namespace Wallow.Identity.Tests.Infrastructure;

public class PermissionRequirementTests
{
    [Fact]
    public void Constructor_SetsPermission()
    {
        PermissionRequirement requirement = new("UsersRead");

        requirement.Permission.Should().Be("UsersRead");
    }
}
