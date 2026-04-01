using System.Net.Http.Json;
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
    [Trait("E2EGroup", "Auth")]
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
    [Trait("E2EGroup", "Auth")]
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

    [Fact(Skip = "Blazor circuit timeout on forgot-password page in containers - needs investigation")]
    [Trait("E2EGroup", "Auth")]
    public async Task ForgotPasswordFlow_SendsResetEmailViaMailpit()
    {
        TestUser user = await TestUserFactory.CreateAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        // Navigate to login and click forgot password
        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.NavigateAsync();
        await loginPage.ClickForgotPasswordAsync();

        await Page.WaitForURLAsync(
            url => url.Contains("/forgot-password", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 15_000 });

        // Wait for Blazor circuit before interacting with the form
        await WaitForBlazorReadyAsync(Page);

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
        await WaitForBlazorReadyAsync(Page);

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
    [Trait("E2EGroup", "Auth")]
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

    [Fact]
    [Trait("E2EGroup", "Auth")]
    public async Task LoginWithInvalidCredentials_ShowsError()
    {
        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.NavigateAsync();

        string fakeEmail = $"nonexistent-{Guid.NewGuid():N}@test.local";
        await loginPage.FillEmailAsync(fakeEmail);
        await loginPage.FillPasswordAsync("WrongP@ssw0rd!99");
        await loginPage.SubmitAsync();

        bool errorVisible = await loginPage.IsErrorVisibleAsync();
        Assert.True(errorVisible, "Error message should be visible after invalid credentials");

        // User should remain on the login page
        Assert.Contains("/login", Page.Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("E2EGroup", "Auth")]
    public async Task LoginWithUnverifiedEmail_ShowsError()
    {
        // Register a user via the API but skip email verification
        string email = $"e2e-unverified-{Guid.NewGuid():N}@test.local";
        string password = "P@ssw0rd!Strong12";

        using HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
        HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            $"{Docker.ApiBaseUrl}/v1/identity/auth/register",
            new { email, password, confirmPassword = password, clientId = "wallow-web-client" });
        response.EnsureSuccessStatusCode();

        // Attempt login without verifying email
        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.NavigateAsync();
        await loginPage.FillEmailAsync(email);
        await loginPage.FillPasswordAsync(password);
        await loginPage.SubmitAsync();

        bool errorVisible = await loginPage.IsErrorVisibleAsync();
        Assert.True(errorVisible, "Error message should be visible for unverified email");

        string? errorMessage = await loginPage.GetErrorMessageAsync();
        Assert.False(string.IsNullOrEmpty(errorMessage), "Error message text should not be empty");
    }

    [Fact]
    [Trait("E2EGroup", "Auth")]
    public async Task LoginWithEmptyFields_ShowsValidation()
    {
        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.NavigateAsync();

        // Submit without entering any credentials
        await loginPage.SubmitAsync();

        bool errorVisible = await loginPage.IsErrorVisibleAsync();
        Assert.True(errorVisible, "Error/validation message should be visible when submitting empty fields");
    }

    [Fact]
    [Trait("E2EGroup", "Auth")]
    public async Task Logout_ClearsSessionAndRedirects()
    {
        TestUser user = await TestUserFactory.CreateAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        // Log in via the OIDC flow to reach the dashboard
        await Page.GotoAsync($"{Docker.WebBaseUrl}/authentication/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByTestId("login-email").WaitForAsync(new() { Timeout = 15_000 });

        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.FillEmailAsync(user.Email);
        await loginPage.FillPasswordAsync(user.Password);
        await loginPage.SubmitAsync();

        await Page.WaitForURLAsync(
            url => url.Contains("/dashboard", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000 });

        // Click logout from the dashboard
        DashboardPage dashboardPage = new(Page, Docker.WebBaseUrl);
        await dashboardPage.ClickLogoutAsync();

        // The OIDC sign-out chain redirects through multiple hops, ending on the Auth login page
        await Page.WaitForURLAsync(
            url => url.Contains("/login", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000 });

        // Verify the login page is shown (session is cleared)
        LoginPage postLogoutLoginPage = new(Page, Docker.AuthBaseUrl);
        bool loginVisible = await postLogoutLoginPage.IsLoadedAsync();

        Assert.True(loginVisible,
            $"Expected login page after logout. URL: {Page.Url}");
    }

    [Fact]
    [Trait("E2EGroup", "Auth")]
    public async Task Logout_PreventsDashboardAccess()
    {
        TestUser user = await TestUserFactory.CreateAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        // Log in via the OIDC flow to reach the dashboard
        await Page.GotoAsync($"{Docker.WebBaseUrl}/authentication/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByTestId("login-email").WaitForAsync(new() { Timeout = 15_000 });

        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.FillEmailAsync(user.Email);
        await loginPage.FillPasswordAsync(user.Password);
        await loginPage.SubmitAsync();

        await Page.WaitForURLAsync(
            url => url.Contains("/dashboard", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000 });

        // Log out via the dashboard
        DashboardPage dashboardPage = new(Page, Docker.WebBaseUrl);
        await dashboardPage.ClickLogoutAsync();

        // Wait for the OIDC sign-out chain to complete — ends on the Auth login page
        await Page.WaitForURLAsync(
            url => url.Contains("/login", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000 });

        // Now try to access the dashboard directly — should redirect to login
        await Page.GotoAsync($"{Docker.WebBaseUrl}/dashboard/apps");
        await Page.WaitForURLAsync(
            url => url.Contains("/login", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000 });

        LoginPage postLogoutLoginPage = new(Page, Docker.AuthBaseUrl);
        bool loginVisible = await postLogoutLoginPage.IsLoadedAsync();

        Assert.True(loginVisible, $"Dashboard should redirect to login after logout. URL: {Page.Url}");
    }

    [Fact]
    [Trait("E2EGroup", "Auth")]
    public async Task RegisterWithDuplicateEmail_ShowsError()
    {
        // Create a verified user first
        TestUser existingUser = await TestUserFactory.CreateAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        // Try to register with the same email
        RegisterPage registerPage = new(Page, Docker.AuthBaseUrl);
        await registerPage.NavigateAsync();
        await registerPage.FillFormAsync(existingUser.Email, "P@ssw0rd!Strong99", "P@ssw0rd!Strong99");
        await registerPage.SubmitAsync();

        bool errorVisible = await registerPage.IsErrorVisibleAsync();
        Assert.True(errorVisible, "Error should be visible when registering with a duplicate email");

        string errorMessage = await registerPage.GetErrorMessageAsync();
        Assert.False(string.IsNullOrEmpty(errorMessage), "Error message should not be empty for duplicate email");
    }

    [Fact]
    [Trait("E2EGroup", "Auth")]
    public async Task RegisterWithWeakPassword_ShowsError()
    {
        RegisterPage registerPage = new(Page, Docker.AuthBaseUrl);
        await registerPage.NavigateAsync();

        string uniqueEmail = $"e2e-weak-{Guid.NewGuid():N}@test.local";
        await registerPage.FillFormAsync(uniqueEmail, "abc", "abc");
        await registerPage.SubmitAsync();

        // Password validation may be client-side or server-side; either way an error should appear
        bool errorVisible = await registerPage.IsErrorVisibleAsync(timeoutMs: 5_000);
        Assert.True(errorVisible, "Error should be visible when registering with a weak password");
    }
}
