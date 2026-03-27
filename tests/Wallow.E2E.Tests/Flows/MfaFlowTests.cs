using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Wallow.E2E.Tests.Fixtures;
using Wallow.E2E.Tests.Infrastructure;
using Wallow.E2E.Tests.PageObjects;
using Xunit.Abstractions;

namespace Wallow.E2E.Tests.Flows;

[Trait("Category", "E2E")]
public sealed class MfaFlowTests : E2ETestBase
{
    public MfaFlowTests(DockerComposeFixture docker, PlaywrightFixture playwright, ITestOutputHelper output)
        : base(docker, playwright, output)
    {
    }

    [Fact]
    public async Task EnrollmentDuringLogin_ShowsSetupPageAndActivatesMfa()
    {
        MfaTestUser user = await TestUserFactory.CreateInMfaRequiredOrgAsync(
            Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.NavigateAsync();
        await loginPage.FillEmailAsync(user.Email);
        await loginPage.FillPasswordAsync(user.Password);
        await loginPage.SubmitAsync();

        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex("mfa/enroll", RegexOptions.IgnoreCase),
            new() { Timeout = 15_000 });

        MfaEnrollPage enrollPage = new(Page, Docker.AuthBaseUrl);
        bool isLoaded = await enrollPage.IsLoadedAsync();
        Assert.True(isLoaded, "MFA enrollment page should be loaded after login redirect");

        await enrollPage.ClickBeginSetupAsync();

        string secret = await enrollPage.GetSecretTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(secret), "TOTP secret should be displayed");

        string code = await TotpHelper.GenerateFreshCodeAsync(secret);
        await enrollPage.FillCodeAsync(code);
        await enrollPage.SubmitAsync();

        await enrollPage.WaitForBackupCodesAsync();
        IReadOnlyList<string> backupCodes = await enrollPage.GetBackupCodesAsync();
        Assert.NotEmpty(backupCodes);
    }

    [Fact]
    public async Task ChallengeDuringLogin_AcceptsValidTotpCode()
    {
        MfaTestUser user = await TestUserFactory.CreateWithMfaAsync(
            Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.NavigateAsync();
        await loginPage.FillEmailAsync(user.Email);
        await loginPage.FillPasswordAsync(user.Password);
        await loginPage.SubmitAsync();

        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex("mfa/challenge", RegexOptions.IgnoreCase),
            new() { Timeout = 15_000 });

        MfaChallengePage challengePage = new(Page, Docker.AuthBaseUrl);
        bool isLoaded = await challengePage.IsLoadedAsync();
        Assert.True(isLoaded, "MFA challenge page should be loaded");

        string code = await TotpHelper.GenerateFreshCodeAsync(user.TotpSecret);
        await challengePage.FillCodeAsync(code);
        await challengePage.SubmitAsync();

        bool isSuccess = await challengePage.IsSuccessAsync();
        Assert.True(isSuccess, "MFA challenge should succeed with valid TOTP code");
    }

    [Fact]
    public async Task ChallengeDuringLogin_AcceptsBackupCode()
    {
        MfaTestUser user = await TestUserFactory.CreateWithMfaAsync(
            Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.NavigateAsync();
        await loginPage.FillEmailAsync(user.Email);
        await loginPage.FillPasswordAsync(user.Password);
        await loginPage.SubmitAsync();

        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex("mfa/challenge", RegexOptions.IgnoreCase),
            new() { Timeout = 15_000 });

        MfaChallengePage challengePage = new(Page, Docker.AuthBaseUrl);
        bool isLoaded = await challengePage.IsLoadedAsync();
        Assert.True(isLoaded, "MFA challenge page should be loaded");

        await challengePage.ToggleBackupCodeAsync();
        await challengePage.FillBackupCodeAsync(user.BackupCodes[0]);
        await challengePage.SubmitAsync();

        bool isSuccess = await challengePage.IsSuccessAsync();
        Assert.True(isSuccess, "MFA challenge should succeed with a valid backup code");
    }

    [Fact]
    public async Task GracePeriodFlow_AllowsLoginWithoutEnrollmentRedirect()
    {
        MfaTestUser user = await TestUserFactory.CreateInMfaRequiredOrgAsync(
            Docker.ApiBaseUrl, Docker.MailpitBaseUrl, gracePeriodDays: 3);

        // Trigger the OIDC login chain via the Web app
        await Page.GotoAsync($"{Docker.WebBaseUrl}/authentication/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByTestId("login-email").WaitForAsync(new() { Timeout = 15_000 });

        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.FillEmailAsync(user.Email);
        await loginPage.FillPasswordAsync(user.Password);
        await loginPage.SubmitAsync();

        // Grace period should allow dashboard access without MFA enrollment redirect
        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex("dashboard", RegexOptions.IgnoreCase),
            new() { Timeout = 30_000 });

        DashboardPage dashboardPage = new(Page, Docker.WebBaseUrl);
        bool dashboardLoaded = await dashboardPage.IsLoadedAsync();
        Assert.True(dashboardLoaded, $"Dashboard should load during grace period. URL: {Page.Url}");
    }

    [Fact]
    public async Task DisableMfa_FromSettingsPage()
    {
        MfaTestUser user = await TestUserFactory.CreateWithMfaAsync(
            Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        // Login through the MFA challenge
        await Page.GotoAsync($"{Docker.WebBaseUrl}/authentication/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByTestId("login-email").WaitForAsync(new() { Timeout = 15_000 });

        await Page.GetByTestId("login-email").FillAsync(user.Email);
        await Page.GetByTestId("login-password").FillAsync(user.Password);
        await Page.GetByTestId("login-submit").ClickAsync();

        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex("mfa/challenge", RegexOptions.IgnoreCase),
            new() { Timeout = 15_000 });

        MfaChallengePage challengePage = new(Page, Docker.AuthBaseUrl);
        bool isLoaded = await challengePage.IsLoadedAsync();
        Assert.True(isLoaded, "MFA challenge page should be loaded");

        string code = await TotpHelper.GenerateFreshCodeAsync(user.TotpSecret);
        await challengePage.FillCodeAsync(code);
        await challengePage.SubmitAsync();

        bool isSuccess = await challengePage.IsSuccessAsync();
        Assert.True(isSuccess, "MFA challenge should succeed before navigating to settings");

        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex("dashboard", RegexOptions.IgnoreCase),
            new() { Timeout = 30_000 });

        // Navigate to settings and disable MFA
        SettingsMfaSection settingsPage = new(Page, Docker.WebBaseUrl);
        await settingsPage.NavigateAsync();
        await settingsPage.ClickDisableAsync();
        await settingsPage.ConfirmPasswordAsync(user.Password);

        // Wait for Blazor to reflect the updated MFA status after disable
        await settingsPage.WaitForMfaStatusAsync("Disabled");
        string status = await settingsPage.GetMfaStatusAsync();
        Assert.DoesNotContain("Enabled", status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegenerateBackupCodes_FromSettingsPage()
    {
        MfaTestUser user = await TestUserFactory.CreateWithMfaAsync(
            Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        // Login through the MFA challenge
        await Page.GotoAsync($"{Docker.WebBaseUrl}/authentication/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByTestId("login-email").WaitForAsync(new() { Timeout = 15_000 });

        await Page.GetByTestId("login-email").FillAsync(user.Email);
        await Page.GetByTestId("login-password").FillAsync(user.Password);
        await Page.GetByTestId("login-submit").ClickAsync();

        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex("mfa/challenge", RegexOptions.IgnoreCase),
            new() { Timeout = 15_000 });

        MfaChallengePage challengePage = new(Page, Docker.AuthBaseUrl);
        bool isLoaded = await challengePage.IsLoadedAsync();
        Assert.True(isLoaded, "MFA challenge page should be loaded");

        string code = await TotpHelper.GenerateFreshCodeAsync(user.TotpSecret);
        await challengePage.FillCodeAsync(code);
        await challengePage.SubmitAsync();

        bool isSuccess = await challengePage.IsSuccessAsync();
        Assert.True(isSuccess, "MFA challenge should succeed before navigating to settings");

        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex("dashboard", RegexOptions.IgnoreCase),
            new() { Timeout = 30_000 });

        // Navigate to settings and regenerate backup codes
        SettingsMfaSection settingsPage = new(Page, Docker.WebBaseUrl);
        await settingsPage.NavigateAsync();
        await settingsPage.ClickRegenerateCodesAsync();
        await settingsPage.ConfirmPasswordAsync(user.Password);

        int backupCodeCount = await settingsPage.GetBackupCodeCountAsync();
        Assert.True(backupCodeCount > 0, "Regenerated backup codes count should be positive");
    }
}
