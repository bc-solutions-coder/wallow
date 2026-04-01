using Microsoft.Playwright;
using Wallow.E2E.Tests.Fixtures;
using Wallow.E2E.Tests.Infrastructure;
using Wallow.E2E.Tests.PageObjects;
using Xunit.Abstractions;

namespace Wallow.E2E.Tests.Flows;

[Trait("Category", "E2E")]
public sealed class AltAuthFlowTests : E2ETestBase
{
    public AltAuthFlowTests(DockerComposeFixture docker, PlaywrightFixture playwright, ITestOutputHelper output)
        : base(docker, playwright, output)
    {
    }

    [Fact(Skip = "Magic link URL lacks OIDC returnUrl - needs app-level fix to include returnUrl in email")]
    [Trait("E2EGroup", "AltAuth")]
    public async Task MagicLinkLogin_HappyPath_LandsDashboard()
    {
        TestUser user = await TestUserFactory.CreateAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        // Trigger the OIDC login chain via the Web app's login endpoint
        await Page.GotoAsync($"{Docker.WebBaseUrl}/authentication/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByTestId("login-email").WaitForAsync(new() { Timeout = 15_000 });

        // Switch to magic link tab and request a link
        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.SwitchToMagicLinkTabAsync();
        await loginPage.FillMagicLinkEmailAsync(user.Email);
        await loginPage.SubmitMagicLinkAsync();

        bool sentVisible = await loginPage.IsMagicLinkSentVisibleAsync();
        Assert.True(sentVisible, "Magic link sent confirmation should be visible");

        // Retrieve the magic link from Mailpit
        string magicLink = await MailpitHelper.SearchForLinkAsync(
            Docker.MailpitBaseUrl, user.Email, "magicLinkToken");

        Assert.False(string.IsNullOrEmpty(magicLink), "Magic link should be present in email");

        // Navigate to the magic link to authenticate
        await Page.GotoAsync(magicLink);

        // Magic link includes OIDC returnUrl from initial Web app login flow,
        // so auth completion will redirect back to the dashboard automatically
        await Page.WaitForURLAsync(
            url => url.Contains("/dashboard", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000 });

        DashboardPage dashboardPage = new(Page, Docker.WebBaseUrl);
        bool dashboardLoaded = await dashboardPage.IsLoadedAsync();
        Assert.True(dashboardLoaded, $"Dashboard should be loaded after magic link login. URL: {Page.Url}");
    }

    [Fact]
    [Trait("E2EGroup", "AltAuth")]
    public async Task MagicLinkLogin_UnregisteredEmail_ShowsSameSuccessMessage()
    {
        // Trigger the OIDC login chain via the Web app's login endpoint
        await Page.GotoAsync($"{Docker.WebBaseUrl}/authentication/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByTestId("login-email").WaitForAsync(new() { Timeout = 15_000 });

        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.SwitchToMagicLinkTabAsync();

        // Use a random unregistered email
        string unregisteredEmail = $"nonexistent-{Guid.NewGuid():N}@test.local";
        await loginPage.FillMagicLinkEmailAsync(unregisteredEmail);
        await loginPage.SubmitMagicLinkAsync();

        // Same confirmation should appear to prevent email enumeration
        bool sentVisible = await loginPage.IsMagicLinkSentVisibleAsync();
        Assert.True(sentVisible, "Magic link sent confirmation should be visible even for unregistered email");
    }

    [Fact]
    [Trait("E2EGroup", "AltAuth")]
    public async Task MagicLinkLogin_InvalidToken_ShowsError()
    {
        // Navigate to the API magic link verify endpoint with a garbage token
        string invalidUrl =
            $"{Docker.ApiBaseUrl}/v1/identity/auth/passwordless/magic-link/verify?token=invalid-token-{Guid.NewGuid():N}";

        IResponse? response = await Page.GotoAsync(invalidUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The page should show an error state -- either via HTTP status or visible error content
        string content = await Page.ContentAsync();
        string currentUrl = Page.Url;

        // Accept any of: non-success HTTP status, error visible on page, or redirect to login
        bool hasError = (response is not null && !response.Ok)
            || content.Contains("invalid", StringComparison.OrdinalIgnoreCase)
            || content.Contains("expired", StringComparison.OrdinalIgnoreCase)
            || content.Contains("error", StringComparison.OrdinalIgnoreCase)
            || currentUrl.Contains("/login", StringComparison.OrdinalIgnoreCase);

        Assert.True(hasError,
            $"Invalid magic link token should produce an error. Status: {response?.Status}, URL: {currentUrl}");
    }

    [Fact]
    [Trait("E2EGroup", "AltAuth")]
    public async Task MagicLinkLogin_PasswordLoginBeforeLinkClicked_Graceful()
    {
        TestUser user = await TestUserFactory.CreateAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        // Trigger the OIDC login chain and request a magic link
        await Page.GotoAsync($"{Docker.WebBaseUrl}/authentication/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByTestId("login-email").WaitForAsync(new() { Timeout = 15_000 });

        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.SwitchToMagicLinkTabAsync();
        await loginPage.FillMagicLinkEmailAsync(user.Email);
        await loginPage.SubmitMagicLinkAsync();

        bool sentVisible = await loginPage.IsMagicLinkSentVisibleAsync();
        Assert.True(sentVisible, "Magic link sent confirmation should be visible");

        // Retrieve the magic link before using password login
        string magicLink = await MailpitHelper.SearchForLinkAsync(
            Docker.MailpitBaseUrl, user.Email, "magicLinkToken");

        Assert.False(string.IsNullOrEmpty(magicLink), "Magic link should be present in email");

        // Now log in with password instead via a fresh OIDC chain
        await Page.GotoAsync($"{Docker.WebBaseUrl}/authentication/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByTestId("login-email").WaitForAsync(new() { Timeout = 15_000 });

        await loginPage.FillEmailAsync(user.Email);
        await loginPage.FillPasswordAsync(user.Password);
        await loginPage.SubmitAsync();

        await Page.WaitForURLAsync(
            url => url.Contains("/dashboard", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000 });

        DashboardPage dashboardPage = new(Page, Docker.WebBaseUrl);
        bool dashboardLoaded = await dashboardPage.IsLoadedAsync();
        Assert.True(dashboardLoaded, "Dashboard should be loaded after password login");

        // Now navigate to the previously obtained magic link -- should not crash
        IResponse? magicLinkResponse = await Page.GotoAsync(magicLink);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The page should handle this gracefully: success, redirect to dashboard, or redirect to login
        string currentUrl = Page.Url;
        bool isGraceful = magicLinkResponse is not null
            && (magicLinkResponse.Ok
                || magicLinkResponse.Status is >= 300 and < 400
                || currentUrl.Contains("/dashboard", StringComparison.OrdinalIgnoreCase)
                || currentUrl.Contains("/login", StringComparison.OrdinalIgnoreCase));

        Assert.True(isGraceful,
            $"Clicking magic link after password login should be handled gracefully. Status: {magicLinkResponse?.Status}, URL: {currentUrl}");
    }

    [Fact]
    [Trait("E2EGroup", "AltAuth")]
    public async Task OtpLogin_HappyPath_LandsDashboard()
    {
        TestUser user = await TestUserFactory.CreateAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        await Page.GotoAsync($"{Docker.WebBaseUrl}/authentication/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByTestId("login-email").WaitForAsync(new() { Timeout = 15_000 });

        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.SwitchToOtpTabAsync();
        await loginPage.FillOtpEmailAsync(user.Email);
        await loginPage.SubmitOtpRequestAsync();

        bool codeFormVisible = await loginPage.IsOtpCodeFormVisibleAsync();
        Assert.True(codeFormVisible, "OTP code form should be visible after requesting a code");

        string otpCode = await MailpitHelper.SearchForCodeAsync(Docker.MailpitBaseUrl, user.Email);
        Assert.False(string.IsNullOrEmpty(otpCode), "OTP code should be present in email");

        await loginPage.FillOtpCodeAsync(otpCode);
        await loginPage.SubmitOtpVerifyAsync();

        await Page.WaitForURLAsync(
            url => url.Contains("/dashboard", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000 });

        DashboardPage dashboardPage = new(Page, Docker.WebBaseUrl);
        bool dashboardLoaded = await dashboardPage.IsLoadedAsync();
        Assert.True(dashboardLoaded, $"Dashboard should be loaded after OTP login. URL: {Page.Url}");
    }

    [Fact]
    [Trait("E2EGroup", "AltAuth")]
    public async Task OtpLogin_WrongCode_ShowsErrorAndAllowsRetry()
    {
        TestUser user = await TestUserFactory.CreateAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        await Page.GotoAsync($"{Docker.WebBaseUrl}/authentication/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByTestId("login-email").WaitForAsync(new() { Timeout = 15_000 });

        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.SwitchToOtpTabAsync();
        await loginPage.FillOtpEmailAsync(user.Email);
        await loginPage.SubmitOtpRequestAsync();

        bool codeFormVisible = await loginPage.IsOtpCodeFormVisibleAsync();
        Assert.True(codeFormVisible, "OTP code form should be visible after requesting a code");

        // Submit an incorrect code
        await loginPage.FillOtpCodeAsync("000000");
        await loginPage.SubmitOtpVerifyAsync();

        bool errorVisible = await loginPage.IsErrorVisibleAsync();
        Assert.True(errorVisible, "Error message should be visible after submitting wrong OTP code");

        // Code form should still be usable for retry
        bool stillVisible = await loginPage.IsOtpCodeFormVisibleAsync();
        Assert.True(stillVisible, "OTP code form should still be visible to allow retry");
    }

    [Fact]
    [Trait("E2EGroup", "AltAuth")]
    public async Task OtpLogin_UnregisteredEmail_ShowsSameSuccessMessage()
    {
        await Page.GotoAsync($"{Docker.WebBaseUrl}/authentication/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByTestId("login-email").WaitForAsync(new() { Timeout = 15_000 });

        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.SwitchToOtpTabAsync();

        string unregisteredEmail = $"nonexistent-{Guid.NewGuid():N}@test.local";
        await loginPage.FillOtpEmailAsync(unregisteredEmail);
        await loginPage.SubmitOtpRequestAsync();

        // Same UX should appear to prevent email enumeration
        bool codeFormVisible = await loginPage.IsOtpCodeFormVisibleAsync();
        Assert.True(codeFormVisible, "OTP code form should be visible even for unregistered email to prevent enumeration");
    }

    [Fact]
    [Trait("E2EGroup", "AltAuth")]
    public async Task OtpLogin_FabricatedCode_ShowsError()
    {
        TestUser user = await TestUserFactory.CreateAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        await Page.GotoAsync($"{Docker.WebBaseUrl}/authentication/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByTestId("login-email").WaitForAsync(new() { Timeout = 15_000 });

        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.SwitchToOtpTabAsync();
        await loginPage.FillOtpEmailAsync(user.Email);
        await loginPage.SubmitOtpRequestAsync();

        bool codeFormVisible = await loginPage.IsOtpCodeFormVisibleAsync();
        Assert.True(codeFormVisible, "OTP code form should be visible after requesting a code");

        // Submit a fabricated code without checking Mailpit — simulates expired/never-issued code
        await loginPage.FillOtpCodeAsync("123456");
        await loginPage.SubmitOtpVerifyAsync();

        bool errorVisible = await loginPage.IsErrorVisibleAsync();
        Assert.True(errorVisible, "Error message should be visible after submitting a fabricated OTP code");

        string? errorMessage = await loginPage.GetErrorMessageAsync();
        Assert.False(string.IsNullOrEmpty(errorMessage), "Error message text should not be empty");
    }
}
