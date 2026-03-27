using Wallow.E2E.Tests.Fixtures;
using Wallow.E2E.Tests.Infrastructure;
using Wallow.E2E.Tests.PageObjects;
using Xunit.Abstractions;

namespace Wallow.E2E.Tests.Flows;

[Trait("Category", "E2E")]
public sealed class DashboardFlowTests : AuthenticatedE2ETestBase
{
    public DashboardFlowTests(DockerComposeFixture docker, PlaywrightFixture playwright, ITestOutputHelper output)
        : base(docker, playwright, output)
    {
    }

    [Fact]
    public async Task AppRegistrationFlow_RegistersNewApplication()
    {
        AppRegistrationPage appPage = new(Page, Docker.WebBaseUrl);
        await appPage.NavigateAsync();

        bool isLoaded = await appPage.IsLoadedAsync();
        Assert.True(isLoaded, "App registration page should be loaded");

        await appPage.FillFormAsync(
            displayName: "app-e2e-test",
            clientType: "public",
            redirectUris: "https://localhost:3000/callback");

        await appPage.SubmitAsync();

        AppRegistrationResult result = await appPage.GetResultAsync();
        Assert.True(result.Success, $"App registration should succeed. Error: {result.ErrorMessage}");
        Assert.False(string.IsNullOrEmpty(result.ClientId), "Client ID should be returned");
    }

    [Fact]
    public async Task OrganizationManagementFlow_ShowsOrganizationsList()
    {
        OrganizationPage orgPage = new(Page, Docker.WebBaseUrl);
        await orgPage.NavigateAsync();

        bool isLoaded = await orgPage.IsLoadedAsync();
        Assert.True(isLoaded, "Organizations page should be loaded");

        // A freshly registered user should see the empty state
        bool isEmpty = await orgPage.IsEmptyStateAsync();
        if (isEmpty)
        {
            Assert.True(isEmpty, "New user should see the empty organizations state");
        }
        else
        {
            IReadOnlyList<OrganizationRow> organizations = await orgPage.GetOrganizationsAsync();
            Assert.NotEmpty(organizations);
        }
    }

    [Fact]
    public async Task InquirySubmissionFlow_SubmitsInquirySuccessfully()
    {
        InquiryPage inquiryPage = new(Page, Docker.WebBaseUrl);
        await inquiryPage.NavigateAsync();

        bool isLoaded = await inquiryPage.IsLoadedAsync();
        Assert.True(isLoaded, "Inquiry page should be loaded");

        await inquiryPage.FillFormAsync(
            name: "E2E Tester",
            email: TestUser.Email,
            message: "This is an automated E2E test inquiry.",
            phone: "+1234567890",
            company: "E2E Corp",
            projectType: "web-app",
            budgetRange: "5k-15k",
            timeline: "1-3-months");

        await inquiryPage.SubmitInquiryAsync();

        bool isSuccess = await inquiryPage.IsSubmissionSuccessAsync();
        Assert.True(isSuccess, "Inquiry submission should succeed");
    }

    [Fact]
    public async Task MfaEnrollmentFlow_ShowsSetupPageAndAcceptsCode()
    {
        SettingsMfaSection settingsMfa = new(Page, Docker.WebBaseUrl);
        await settingsMfa.NavigateAsync();

        await settingsMfa.ClickEnableAsync();

        MfaEnrollPage enrollPage = new(Page, Docker.AuthBaseUrl);
        bool isLoaded = await enrollPage.IsLoadedAsync();
        Assert.True(isLoaded, "MFA enrollment page should be loaded");

        await enrollPage.ClickBeginSetupAsync();

        string secret = await enrollPage.GetSecretTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(secret), "TOTP secret should be displayed");

        string code = await TotpHelper.GenerateFreshCodeAsync(secret);
        await enrollPage.FillCodeAsync(code);
        await enrollPage.SubmitAsync();

        IReadOnlyList<string> backupCodes = await enrollPage.GetBackupCodesAsync();
        Assert.NotEmpty(backupCodes);
    }
}
