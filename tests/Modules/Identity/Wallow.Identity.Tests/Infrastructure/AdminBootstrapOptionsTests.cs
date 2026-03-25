using Wallow.Identity.Infrastructure.Options;

namespace Wallow.Identity.Tests.Infrastructure;

public class AdminBootstrapOptionsTests
{
    [Fact]
    public void IsConfigured_BothSet_ReturnsTrue()
    {
        AdminBootstrapOptions options = new() { Email = "admin@test.com", Password = "P@ssw0rd" };

        options.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public void IsConfigured_EmailEmpty_ReturnsFalse()
    {
        AdminBootstrapOptions options = new() { Email = "", Password = "P@ssw0rd" };

        options.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void IsConfigured_PasswordEmpty_ReturnsFalse()
    {
        AdminBootstrapOptions options = new() { Email = "admin@test.com", Password = "" };

        options.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void IsConfigured_BothWhitespace_ReturnsFalse()
    {
        AdminBootstrapOptions options = new() { Email = "  ", Password = "  " };

        options.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void SectionName_IsAdminBootstrap()
    {
        AdminBootstrapOptions.SectionName.Should().Be("AdminBootstrap");
    }

    [Fact]
    public void Defaults_AreEmptyStrings()
    {
        AdminBootstrapOptions options = new();

        options.Email.Should().BeEmpty();
        options.Password.Should().BeEmpty();
        options.FirstName.Should().BeEmpty();
        options.LastName.Should().BeEmpty();
    }
}
