using Wallow.Branding.Domain.Entities;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Branding.Tests.Domain;

public class ClientBrandingCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsBrandingWithCorrectProperties()
    {
        ClientBranding branding = ClientBranding.Create("client-1", "My App", "A tagline", "logos/key.png", "{\"primary\":\"#fff\"}");

        branding.ClientId.Should().Be("client-1");
        branding.DisplayName.Should().Be("My App");
        branding.Tagline.Should().Be("A tagline");
        branding.LogoStorageKey.Should().Be("logos/key.png");
        branding.ThemeJson.Should().Be("{\"primary\":\"#fff\"}");
        branding.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        branding.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithOnlyRequiredFields_SetsOptionalFieldsToNull()
    {
        ClientBranding branding = ClientBranding.Create("client-1", "My App");

        branding.Tagline.Should().BeNull();
        branding.LogoStorageKey.Should().BeNull();
        branding.ThemeJson.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyClientId_ThrowsBusinessRuleException(string? clientId)
    {
        Action act = () => ClientBranding.Create(clientId!, "My App");

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Branding.ClientBrandingClientIdRequired");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyDisplayName_ThrowsBusinessRuleException(string? displayName)
    {
        Action act = () => ClientBranding.Create("client-1", displayName!);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Branding.ClientBrandingDisplayNameRequired");
    }
}

public class ClientBrandingUpdateTests
{
    [Fact]
    public void Update_WithValidData_UpdatesAllFields()
    {
        ClientBranding branding = ClientBranding.Create("client-1", "Original");

        branding.Update("Updated Name", "New tagline", "logos/new.png", "{\"primary\":\"#000\"}");

        branding.DisplayName.Should().Be("Updated Name");
        branding.Tagline.Should().Be("New tagline");
        branding.LogoStorageKey.Should().Be("logos/new.png");
        branding.ThemeJson.Should().Be("{\"primary\":\"#000\"}");
        branding.UpdatedAt.Should().NotBeNull();
        branding.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_WithEmptyDisplayName_ThrowsBusinessRuleException(string? displayName)
    {
        ClientBranding branding = ClientBranding.Create("client-1", "Original");

        Action act = () => branding.Update(displayName!, null, null, null);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Branding.ClientBrandingDisplayNameRequired");
    }

    [Fact]
    public void ClearLogo_RemovesLogoAndSetsUpdatedAt()
    {
        ClientBranding branding = ClientBranding.Create("client-1", "My App", logoStorageKey: "logos/key.png");

        branding.ClearLogo();

        branding.LogoStorageKey.Should().BeNull();
        branding.UpdatedAt.Should().NotBeNull();
    }
}
