using Wallow.Identity.Infrastructure.Options;

namespace Wallow.Identity.Tests.Infrastructure;

public class AdminBootstrapOptionsTests
{
    [Fact]
    public void IsConfigured_AllRequiredSet_ReturnsTrue()
    {
        AdminBootstrapOptions options = new()
        {
            Email = "admin@test.com",
            Password = "P@ssw0rd",
            OrganizationName = "Wallow"
        };

        options.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public void IsConfigured_EmailEmpty_ReturnsFalse()
    {
        AdminBootstrapOptions options = new() { Email = "", Password = "P@ssw0rd", OrganizationName = "Wallow" };

        options.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void IsConfigured_PasswordEmpty_ReturnsFalse()
    {
        AdminBootstrapOptions options = new() { Email = "admin@test.com", Password = "", OrganizationName = "Wallow" };

        options.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void IsConfigured_OrganizationNameEmpty_ReturnsFalse()
    {
        // Without an organization the bootstrapped user holds no role anywhere and the setup
        // gate never closes, so an admin block missing it is not configured.
        AdminBootstrapOptions options = new() { Email = "admin@test.com", Password = "P@ssw0rd", OrganizationName = "" };

        options.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void IsConfigured_AllWhitespace_ReturnsFalse()
    {
        AdminBootstrapOptions options = new() { Email = "  ", Password = "  ", OrganizationName = "  " };

        options.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void SectionName_IsAdminBootstrap()
    {
        AdminBootstrapOptions.SectionName.Should().Be("AdminBootstrap");
    }

    [Fact]
    public void Defaults_AreEmptyAndNotGlobalAdmin()
    {
        AdminBootstrapOptions options = new();

        options.Email.Should().BeEmpty();
        options.Password.Should().BeEmpty();
        options.FirstName.Should().BeEmpty();
        options.LastName.Should().BeEmpty();
        options.OrganizationName.Should().BeEmpty();
        options.IsGlobalAdmin.Should().BeFalse();
    }
}
