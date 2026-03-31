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
    [Trait("E2EGroup", "MFA")]
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
    [Trait("E2EGroup", "MFA")]
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
    [Trait("E2EGroup", "MFA")]
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
    [Trait("E2EGroup", "MFA")]
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
    [Trait("E2EGroup", "MFA")]
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
    [Trait("E2EGroup", "MFA")]
    public async Task ChallengeDuringLogin_RejectsInvalidTotpCode()
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

        await challengePage.FillCodeAsync("000000");
        await challengePage.SubmitAsync();

        string? error = await challengePage.GetErrorAsync();
        Assert.False(string.IsNullOrEmpty(error), "An error message should be displayed for an invalid TOTP code");
        Assert.Contains("/mfa/challenge", Page.Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("E2EGroup", "MFA")]
    public async Task ChallengeDuringLogin_RejectsUsedBackupCode()
    {
        MfaTestUser user = await TestUserFactory.CreateWithMfaAsync(
            Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        // First login: consume the first backup code
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
        Assert.True(isSuccess, "First use of backup code should succeed");

        // Log out by navigating to the Auth logout endpoint
        await Page.GotoAsync($"{Docker.AuthBaseUrl}/account/logout");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Second login: attempt to reuse the same backup code
        LoginPage loginPage2 = new(Page, Docker.AuthBaseUrl);
        await loginPage2.NavigateAsync();
        await loginPage2.FillEmailAsync(user.Email);
        await loginPage2.FillPasswordAsync(user.Password);
        await loginPage2.SubmitAsync();

        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex("mfa/challenge", RegexOptions.IgnoreCase),
            new() { Timeout = 15_000 });

        MfaChallengePage challengePage2 = new(Page, Docker.AuthBaseUrl);
        bool isLoaded2 = await challengePage2.IsLoadedAsync();
        Assert.True(isLoaded2, "MFA challenge page should be loaded on second login");

        await challengePage2.ToggleBackupCodeAsync();
        await challengePage2.FillBackupCodeAsync(user.BackupCodes[0]);
        await challengePage2.SubmitAsync();

        string? error = await challengePage2.GetErrorAsync();
        Assert.False(string.IsNullOrEmpty(error), "An error message should be displayed for a reused backup code");
    }

    [Fact(Skip = "TOTP timing flake in CI - code expires between invalid and valid attempts")]
    [Trait("E2EGroup", "MFA")]
    public async Task EnrollmentDuringLogin_RejectsInvalidCodeAndAllowsRetry()
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

        // Submit an invalid code
        await enrollPage.FillCodeAsync("000000");
        await enrollPage.SubmitAsync(throwOnError: false);

        string? error = await enrollPage.GetErrorMessageAsync();
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error), "An error message should be displayed for an invalid TOTP code");

        // Retry with a valid code
        string validCode = await TotpHelper.GenerateFreshCodeAsync(secret);
        await enrollPage.FillCodeAsync(validCode);
        await enrollPage.SubmitAsync();

        await enrollPage.WaitForBackupCodesAsync();
        IReadOnlyList<string> backupCodes = await enrollPage.GetBackupCodesAsync();
        Assert.NotEmpty(backupCodes);
    }

    [Fact]
    [Trait("E2EGroup", "MFA")]
    public async Task EnrollmentCancel_DoesNotEnableMfa()
    {
        MfaTestUser user = await TestUserFactory.CreateInMfaRequiredOrgAsync(
            Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        // First login — should redirect to /mfa/enroll
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

        // Cancel enrollment — should navigate away from /mfa/enroll
        await enrollPage.CancelAsync();
        Assert.DoesNotContain("/mfa/enroll", Page.Url, StringComparison.OrdinalIgnoreCase);

        // Log out by navigating to the Auth logout endpoint
        await Page.GotoAsync($"{Docker.AuthBaseUrl}/account/logout");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Log in again — if MFA was not enabled, the org requirement should
        // redirect back to /mfa/enroll, proving cancel did not activate MFA
        LoginPage loginPage2 = new(Page, Docker.AuthBaseUrl);
        await loginPage2.NavigateAsync();
        await loginPage2.FillEmailAsync(user.Email);
        await loginPage2.FillPasswordAsync(user.Password);
        await loginPage2.SubmitAsync();

        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex("mfa/enroll", RegexOptions.IgnoreCase),
            new() { Timeout = 15_000 });
    }

    [Fact]
    [Trait("E2EGroup", "MFA")]
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

    [Fact]
    [Trait("E2EGroup", "MFA")]
    public async Task DisableMfa_WithWrongPassword_ShowsErrorAndMfaRemainsEnabled()
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

        // Navigate to settings and attempt to disable MFA with wrong password
        SettingsMfaSection settingsPage = new(Page, Docker.WebBaseUrl);
        await settingsPage.NavigateAsync();

        string status = await settingsPage.GetMfaStatusAsync();
        Assert.Contains("Enabled", status, StringComparison.OrdinalIgnoreCase);

        await settingsPage.ClickDisableAsync();
        string errorText = await settingsPage.SubmitPasswordAndExpectErrorAsync("wrongpassword123!");

        Assert.False(string.IsNullOrEmpty(errorText), "An error message should be displayed for wrong password");

        // MFA should still be enabled
        string statusAfter = await settingsPage.GetMfaStatusAsync();
        Assert.Contains("Enabled", statusAfter, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("E2EGroup", "MFA")]
    public async Task RegenerateBackupCodes_WithWrongPassword_ShowsErrorAndCodesUnchanged()
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

        // Navigate to settings and attempt to regenerate backup codes with wrong password
        SettingsMfaSection settingsPage = new(Page, Docker.WebBaseUrl);
        await settingsPage.NavigateAsync();

        int initialBackupCodeCount = await settingsPage.GetBackupCodeCountAsync();

        await settingsPage.ClickRegenerateCodesAsync();
        string errorText = await settingsPage.SubmitPasswordAndExpectErrorAsync("wrongpassword123!");

        Assert.False(string.IsNullOrEmpty(errorText), "An error message should be displayed for wrong password");

        // Backup code count should remain unchanged
        int backupCodeCountAfter = await settingsPage.GetBackupCodeCountAsync();
        Assert.Equal(initialBackupCodeCount, backupCodeCountAfter);
    }
}
