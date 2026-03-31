using Wallow.E2E.Tests.Fixtures;
using Wallow.E2E.Tests.Infrastructure;
using Wallow.E2E.Tests.PageObjects;
using Xunit.Abstractions;

namespace Wallow.E2E.Tests.Flows;

[Trait("Category", "E2E")]
public sealed class SettingsFlowTests : AuthenticatedE2ETestBase
{
    public SettingsFlowTests(DockerComposeFixture docker, PlaywrightFixture playwright, ITestOutputHelper output)
        : base(docker, playwright, output)
    {
    }

    [Fact(Skip = "Pending fix - tracked in beads")]
    [Trait("E2EGroup", "Settings")]
    public async Task SettingsPage_ShowsProfileEmail()
    {
        SettingsProfileSection settings = new(Page, Docker.WebBaseUrl);
        await settings.NavigateAsync();

        bool isLoaded = await settings.IsLoadedAsync();
        Assert.True(isLoaded, "Settings page should be loaded");

        string email = await settings.GetProfileEmailAsync();
        Assert.Equal(TestUser.Email, email);
    }

    [Fact]
    [Trait("E2EGroup", "Settings")]
    public async Task SettingsPage_ShowsProfileName()
    {
        SettingsProfileSection settings = new(Page, Docker.WebBaseUrl);
        await settings.NavigateAsync();

        bool isLoaded = await settings.IsLoadedAsync();
        Assert.True(isLoaded, "Settings page should be loaded");

        string name = await settings.GetProfileNameAsync();
        Assert.False(string.IsNullOrWhiteSpace(name), "Profile name should not be empty");
    }

    [Fact]
    [Trait("E2EGroup", "Settings")]
    public async Task SettingsPage_ShowsRolesSection()
    {
        SettingsProfileSection settings = new(Page, Docker.WebBaseUrl);
        await settings.NavigateAsync();

        bool isLoaded = await settings.IsLoadedAsync();
        Assert.True(isLoaded, "Settings page should be loaded");

        bool rolesVisible = await settings.IsRolesSectionVisibleAsync();
        Assert.True(rolesVisible, "Roles section should be visible on settings page");
    }
}
