using Wallow.E2E.Tests.Fixtures;
using Wallow.E2E.Tests.Infrastructure;
using Wallow.E2E.Tests.PageObjects;
using Xunit.Abstractions;

namespace Wallow.E2E.Tests.Flows;

[Trait("Category", "E2E")]
[Trait("E2EGroup", "AppRegistration")]
public sealed class AppRegistrationFlowTests : AuthenticatedE2ETestBase
{
    public AppRegistrationFlowTests(DockerComposeFixture docker, PlaywrightFixture playwright, ITestOutputHelper output)
        : base(docker, playwright, output)
    {
    }

    [Fact(Skip = "Pending fix - tracked in beads")]
    public async Task RegisterApp_RequiredFieldsOnly_ShowsClientId()
    {
        AppRegistrationPage appPage = new(Page, Docker.WebBaseUrl);
        await appPage.NavigateAsync();

        bool isLoaded = await appPage.IsLoadedAsync();
        Assert.True(isLoaded, "App registration page should be loaded");

        string appName = $"app-e2e-required-{Guid.NewGuid():N}";
        await appPage.FillFormAsync(displayName: appName, clientType: "public");
        await appPage.SubmitAsync();

        AppRegistrationResult result = await appPage.GetResultAsync();
        Assert.True(result.Success, $"App registration should succeed. Error: {result.ErrorMessage}");
        Assert.False(string.IsNullOrEmpty(result.ClientId), "Client ID should be returned");
    }

    [Fact(Skip = "Pending fix - tracked in beads")]
    public async Task RegisterApp_WithBranding_CompletesSuccessfully()
    {
        AppRegistrationPage appPage = new(Page, Docker.WebBaseUrl);
        await appPage.NavigateAsync();

        bool isLoaded = await appPage.IsLoadedAsync();
        Assert.True(isLoaded, "App registration page should be loaded");

        string appName = $"app-e2e-branding-{Guid.NewGuid():N}";
        await appPage.FillFormAsync(displayName: appName, clientType: "public");
        await appPage.FillBrandingAsync(companyName: "E2E Corp", tagline: "Test tagline");
        await appPage.SubmitAsync();

        AppRegistrationResult result = await appPage.GetResultAsync();
        Assert.True(result.Success, $"App registration should succeed. Error: {result.ErrorMessage}");
    }

    [Fact(Skip = "Pending fix - tracked in beads")]
    public async Task RegisterApp_ConfidentialType_ShowsClientSecret()
    {
        AppRegistrationPage appPage = new(Page, Docker.WebBaseUrl);
        await appPage.NavigateAsync();

        bool isLoaded = await appPage.IsLoadedAsync();
        Assert.True(isLoaded, "App registration page should be loaded");

        string appName = $"app-e2e-confidential-{Guid.NewGuid():N}";
        await appPage.FillFormAsync(
            displayName: appName,
            clientType: "confidential",
            redirectUris: "https://localhost:3000/callback");
        await appPage.SubmitAsync();

        AppRegistrationResult result = await appPage.GetResultAsync();
        Assert.True(result.Success, $"App registration should succeed. Error: {result.ErrorMessage}");

        string clientSecret = await appPage.GetClientSecretAsync();
        Assert.False(string.IsNullOrEmpty(clientSecret), "Client secret should be returned for confidential apps");
    }

    [Fact(Skip = "Pending fix - tracked in beads")]
    public async Task RegisterApp_WithLogo_UploadsSuccessfully()
    {
        AppRegistrationPage appPage = new(Page, Docker.WebBaseUrl);
        await appPage.NavigateAsync();

        bool isLoaded = await appPage.IsLoadedAsync();
        Assert.True(isLoaded, "App registration page should be loaded");

        string appName = $"app-e2e-logo-{Guid.NewGuid():N}";
        await appPage.FillFormAsync(displayName: appName, clientType: "public");

        string logoPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "test-logo.png");
        await appPage.UploadLogoAsync(logoPath);
        await appPage.SubmitAsync();

        AppRegistrationResult result = await appPage.GetResultAsync();
        Assert.True(result.Success, $"App registration with logo should succeed. Error: {result.ErrorMessage}");
    }

    [Fact(Skip = "Pending fix - tracked in beads")]
    public async Task RegisterApp_TogglingScope_ChangesSelection()
    {
        AppRegistrationPage appPage = new(Page, Docker.WebBaseUrl);
        await appPage.NavigateAsync();

        bool isLoaded = await appPage.IsLoadedAsync();
        Assert.True(isLoaded, "App registration page should be loaded");

        string appName = $"app-e2e-scopes-{Guid.NewGuid():N}";
        await appPage.FillFormAsync(displayName: appName, clientType: "public");

        await appPage.ToggleScopeAsync("announcements.read");
        await appPage.ToggleScopeAsync("storage.read");
        await appPage.SubmitAsync();

        AppRegistrationResult result = await appPage.GetResultAsync();
        Assert.True(result.Success, $"App registration with scopes should succeed. Error: {result.ErrorMessage}");
    }

    [Fact(Skip = "Pending fix - tracked in beads")]
    public async Task RegisteredApp_AppearsInAppsList()
    {
        AppRegistrationPage appPage = new(Page, Docker.WebBaseUrl);
        await appPage.NavigateAsync();

        bool isLoaded = await appPage.IsLoadedAsync();
        Assert.True(isLoaded, "App registration page should be loaded");

        string appName = $"app-e2e-list-{Guid.NewGuid():N}";
        await appPage.FillFormAsync(
            displayName: appName,
            clientType: "public",
            redirectUris: "https://localhost:3000/callback");
        await appPage.SubmitAsync();

        AppRegistrationResult result = await appPage.GetResultAsync();
        Assert.True(result.Success, $"App registration should succeed. Error: {result.ErrorMessage}");

        DashboardPage dashboard = new(Page, Docker.WebBaseUrl);
        await dashboard.NavigateToAppsAsync();

        AppRow? appRow = await dashboard.FindAppByNameAsync(appName);
        Assert.NotNull(appRow);
        Assert.Equal(appName, appRow.DisplayName);
    }
}
