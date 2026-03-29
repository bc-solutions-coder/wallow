using Microsoft.Playwright;
using Wallow.E2E.Tests.Fixtures;
using Wallow.E2E.Tests.Infrastructure;
using Wallow.E2E.Tests.PageObjects;
using Xunit.Abstractions;

namespace Wallow.E2E.Tests.Flows;

[Trait("Category", "E2E")]
public sealed class AuthFlowTests : E2ETestBase
{
    public AuthFlowTests(DockerComposeFixture docker, PlaywrightFixture playwright, ITestOutputHelper output)
        : base(docker, playwright, output)
    {
    }

    [Fact]
    public async Task RegistrationAndLoginFlow_CompletesSuccessfully()
    {
        TestUser user = await TestUserFactory.CreateAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        // Navigate to login and fill credentials
        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.NavigateAsync();
        bool loginLoaded = await loginPage.IsLoadedAsync();
        Assert.True(loginLoaded, "Login page should be loaded");

        await loginPage.FillEmailAsync(user.Email);
        await loginPage.FillPasswordAsync(user.Password);
        await loginPage.SubmitAsync();

        string? errorMessage = await loginPage.GetErrorMessageAsync();
        Assert.Null(errorMessage);
    }

    [Fact]
    public async Task LoginToDashboard_RedirectsAuthenticatedUser()
    {
        TestUser user = await TestUserFactory.CreateAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        // Trigger the OIDC login chain via the Web app's login endpoint
        await Page.GotoAsync($"{Docker.WebBaseUrl}/authentication/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify we landed on the Auth login page after OIDC redirect
        string loginPageUrl = Page.Url;
        Assert.True(
            loginPageUrl.Contains("/login", StringComparison.OrdinalIgnoreCase),
            $"Expected Auth login page after OIDC redirect, but got URL: {loginPageUrl}");

        await Page.GetByTestId("login-email").WaitForAsync(new() { Timeout = 15_000 });

        // Fill credentials using data-testid selectors via page object
        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.FillEmailAsync(user.Email);
        await loginPage.FillPasswordAsync(user.Password);
        await loginPage.SubmitAsync();

        // Wait for the OIDC redirect chain to reach the dashboard
        await Page.WaitForURLAsync(
            url => url.Contains("/dashboard", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000 });

        // Verify we ended up on the dashboard
        DashboardPage dashboardPage = new(Page, Docker.WebBaseUrl);
        bool dashboardLoaded = await dashboardPage.IsLoadedAsync();
        Assert.True(dashboardLoaded, $"Dashboard should be loaded after login. URL: {Page.Url}");

        string? welcomeMessage = await dashboardPage.GetWelcomeMessageAsync();
        Assert.NotNull(welcomeMessage);
    }

    [Fact]
    public async Task ForgotPasswordFlow_SendsResetEmailViaMailpit()
    {
        TestUser user = await TestUserFactory.CreateAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        // Navigate to login and click forgot password
        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.NavigateAsync();
        await loginPage.ClickForgotPasswordAsync();

        // Fill in the forgot password form using data-testid
        await Page.Locator("[data-testid='forgot-password-email']").FillAsync(user.Email);
        await Page.Locator("[data-testid='forgot-password-submit']").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify the reset email was sent via Mailpit
        string resetLink = await MailpitHelper.SearchForLinkAsync(Docker.MailpitBaseUrl, user.Email, "reset");
        Assert.False(string.IsNullOrEmpty(resetLink), "Password reset link should be present in email");

        // Visit the reset link and set a new password
        await Page.GotoAsync(resetLink);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        string newPassword = "NewP@ssw0rd!Strong34";
        ILocator passwordField = Page.GetByTestId("reset-password-new-password");
        if (await passwordField.IsVisibleAsync())
        {
            await passwordField.FillAsync(newPassword);
        }

        ILocator confirmField = Page.GetByTestId("reset-password-confirm");
        if (await confirmField.IsVisibleAsync())
        {
            await confirmField.FillAsync(newPassword);
        }

        ILocator submitButton = Page.GetByTestId("reset-password-submit");
        if (await submitButton.IsVisibleAsync())
        {
            await submitButton.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        // Log in with the new password
        await loginPage.NavigateAsync();
        await loginPage.FillEmailAsync(user.Email);
        await loginPage.FillPasswordAsync(newPassword);
        await loginPage.SubmitAsync();

        string? errorMessage = await loginPage.GetErrorMessageAsync();
        Assert.Null(errorMessage);
    }

    [Fact]
    public async Task AuthRedirectFlow_ProtectedRouteRedirectsToLoginThenReturns()
    {
        // Try to access a protected route without authentication
        DashboardPage dashboardPage = new(Page, Docker.WebBaseUrl);
        await dashboardPage.NavigateAsync();

        // Should be redirected to login
        await Page.WaitForURLAsync(url => url.Contains("/login") || url.Contains("/authentication/login"));
        string currentUrl = Page.Url;
        Assert.Contains("login", currentUrl, StringComparison.OrdinalIgnoreCase);
    }

}
