using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Wallow.E2E.Tests.Fixtures;
using Wallow.E2E.Tests.PageObjects;

namespace Wallow.E2E.Tests.Flows;

[Trait("Category", "E2E")]
public sealed class AuthFlowTests : IClassFixture<DockerComposeFixture>, IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly DockerComposeFixture _docker;
    private readonly PlaywrightFixture _playwright;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    private static readonly string _mailpitBaseUrl = Environment.GetEnvironmentVariable("E2E_MAILPIT_URL") ?? "http://localhost:8035";

    public AuthFlowTests(DockerComposeFixture docker, PlaywrightFixture playwright)
    {
        _docker = docker;
        _playwright = playwright;
    }

    public async Task InitializeAsync()
    {
        _context = await _playwright.CreateBrowserContextAsync();
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task RegistrationAndLoginFlow_CompletesSuccessfully()
    {
        string testEmail = $"e2e-{Guid.NewGuid():N}@test.local";
        string testPassword = "P@ssw0rd!Strong12";

        // Register a new account
        RegisterPage registerPage = new(_page, _docker.AuthBaseUrl);
        await registerPage.NavigateAsync();
        bool registerLoaded = await registerPage.IsLoadedAsync();
        Assert.True(registerLoaded, "Register page should be loaded");

        await registerPage.FillFormAsync(testEmail, testPassword, testPassword);
        await registerPage.SubmitAsync();

        // Retrieve email verification link from Mailpit
        string verificationLink = await GetVerificationLinkFromMailpitAsync(testEmail);
        Assert.False(string.IsNullOrEmpty(verificationLink), "Verification link should be present in email");

        // Visit the verification link
        await _page.GotoAsync(verificationLink);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Log in with the verified account
        LoginPage loginPage = new(_page, _docker.AuthBaseUrl);
        await loginPage.NavigateAsync();
        bool loginLoaded = await loginPage.IsLoadedAsync();
        Assert.True(loginLoaded, "Login page should be loaded");

        await loginPage.FillEmailAsync(testEmail);
        await loginPage.FillPasswordAsync(testPassword);
        await loginPage.SubmitAsync();

        string? errorMessage = await loginPage.GetErrorMessageAsync();
        Assert.Null(errorMessage);
    }

    [Fact]
    public async Task LoginToDashboard_RedirectsAuthenticatedUser()
    {
        string testEmail = $"e2e-{Guid.NewGuid():N}@test.local";
        string testPassword = "P@ssw0rd!Strong12";

        // Pre-register and verify the account
        await RegisterAndVerifyAccountAsync(testEmail, testPassword);

        // Trigger the OIDC login chain via the Web app's login endpoint.
        // This redirects: Web OIDC challenge → API /connect/authorize → Auth /login?returnUrl=...
        await _page.GotoAsync($"{_docker.WebBaseUrl}/authentication/login");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Fill credentials on the Auth login page (redirected here by OIDC chain)
        await _page.Locator("#email").FillAsync(testEmail);
        await _page.Locator("#password").FillAsync(testPassword);
        await _page.Locator("button[type='submit']").ClickAsync();

        // Wait for the full redirect chain: Auth → exchange-ticket → API → authorize → Web callback → dashboard
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify we ended up on the dashboard
        DashboardPage dashboardPage = new(_page, _docker.WebBaseUrl);
        bool dashboardLoaded = await dashboardPage.IsLoadedAsync();
        Assert.True(dashboardLoaded, "Dashboard should be loaded after login");

        string? welcomeMessage = await dashboardPage.GetWelcomeMessageAsync();
        Assert.NotNull(welcomeMessage);
    }

    [Fact]
    public async Task ForgotPasswordFlow_SendsResetEmailViaMailpit()
    {
        string testEmail = $"e2e-{Guid.NewGuid():N}@test.local";
        string testPassword = "P@ssw0rd!Strong12";

        // Pre-register and verify the account
        await RegisterAndVerifyAccountAsync(testEmail, testPassword);

        // Navigate to login and click forgot password
        LoginPage loginPage = new(_page, _docker.AuthBaseUrl);
        await loginPage.NavigateAsync();
        await loginPage.ClickForgotPasswordAsync();

        // Fill in the forgot password form
        await _page.Locator("#forgot-email").FillAsync(testEmail);
        await _page.Locator("button:has-text('Send reset link')").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify the reset email was sent via Mailpit
        string resetLink = await GetPasswordResetLinkFromMailpitAsync(testEmail);
        Assert.False(string.IsNullOrEmpty(resetLink), "Password reset link should be present in email");

        // Visit the reset link and set a new password
        await _page.GotoAsync(resetLink);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        string newPassword = "NewP@ssw0rd!Strong34";
        ILocator passwordField = _page.Locator("#password");
        if (await passwordField.IsVisibleAsync())
        {
            await passwordField.FillAsync(newPassword);
        }

        ILocator confirmField = _page.Locator("#confirmPassword");
        if (await confirmField.IsVisibleAsync())
        {
            await confirmField.FillAsync(newPassword);
        }

        ILocator submitButton = _page.Locator("button[type='submit']");
        if (await submitButton.IsVisibleAsync())
        {
            await submitButton.ClickAsync();
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        // Log in with the new password
        await loginPage.NavigateAsync();
        await loginPage.FillEmailAsync(testEmail);
        await loginPage.FillPasswordAsync(newPassword);
        await loginPage.SubmitAsync();

        string? errorMessage = await loginPage.GetErrorMessageAsync();
        Assert.Null(errorMessage);
    }

    [Fact]
    public async Task AuthRedirectFlow_ProtectedRouteRedirectsToLoginThenReturns()
    {
        string testEmail = $"e2e-{Guid.NewGuid():N}@test.local";
        string testPassword = "P@ssw0rd!Strong12";

        // Pre-register and verify the account
        await RegisterAndVerifyAccountAsync(testEmail, testPassword);

        // Try to access a protected route without authentication
        DashboardPage dashboardPage = new(_page, _docker.WebBaseUrl);
        await dashboardPage.NavigateAsync();

        // Should be redirected to login
        await _page.WaitForURLAsync(url => url.Contains("/login") || url.Contains("/authentication/login"));
        string currentUrl = _page.Url;
        Assert.Contains("login", currentUrl, StringComparison.OrdinalIgnoreCase);
    }

    private async Task RegisterAndVerifyAccountAsync(string email, string password)
    {
        RegisterPage registerPage = new(_page, _docker.AuthBaseUrl);
        await registerPage.NavigateAsync();
        await registerPage.FillFormAsync(email, password, password);
        await registerPage.SubmitAsync();

        string verificationLink = await GetVerificationLinkFromMailpitAsync(email);
        if (!string.IsNullOrEmpty(verificationLink))
        {
            await _page.GotoAsync(verificationLink);
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
    }

    private static async Task<string> GetVerificationLinkFromMailpitAsync(string recipientEmail)
    {
        return await GetLinkFromMailpitAsync(recipientEmail, "verify", "confirm");
    }

    private static async Task<string> GetPasswordResetLinkFromMailpitAsync(string recipientEmail)
    {
        return await GetLinkFromMailpitAsync(recipientEmail, "reset-password", "reset");
    }

    private static async Task<string> GetLinkFromMailpitAsync(string recipientEmail, params string[] linkKeywords)
    {
        using HttpClient httpClient = new();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        // Poll Mailpit for the email (may take a moment to arrive)
        int maxRetries = 10;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            HttpResponseMessage response = await httpClient.GetAsync(
                $"{_mailpitBaseUrl}/api/v1/search?query=to:{recipientEmail}");

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                MailpitSearchResult? result = JsonSerializer.Deserialize<MailpitSearchResult>(json);

                if (result?.Messages is { Count: > 0 })
                {
                    // Get the most recent message
                    string messageId = result.Messages[0].Id;
                    HttpResponseMessage msgResponse = await httpClient.GetAsync(
                        $"{_mailpitBaseUrl}/api/v1/message/{messageId}");

                    if (msgResponse.IsSuccessStatusCode)
                    {
                        MailpitMessage? message = await msgResponse.Content.ReadFromJsonAsync<MailpitMessage>();
                        string body = message?.Text ?? message?.Html ?? string.Empty;

                        // Extract link matching any of the keywords
                        foreach (string keyword in linkKeywords)
                        {
                            string? link = ExtractLinkContaining(body, keyword);
                            if (link is not null)
                            {
                                return link;
                            }
                        }
                    }
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        return string.Empty;
    }

    private static string? ExtractLinkContaining(string body, string keyword)
    {
        // Look for URLs containing the keyword
        int searchIndex = 0;
        while (searchIndex < body.Length)
        {
            int httpIndex = body.IndexOf("http", searchIndex, StringComparison.OrdinalIgnoreCase);
            if (httpIndex < 0)
            {
                break;
            }

            int endIndex = body.IndexOfAny([' ', '"', '\'', '<', '\n', '\r'], httpIndex);
            if (endIndex < 0)
            {
                endIndex = body.Length;
            }

            string url = body[httpIndex..endIndex];
            if (url.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            searchIndex = endIndex;
        }

        return null;
    }

    // Mailpit API response models
    private sealed class MailpitSearchResult
    {
        [JsonPropertyName("messages")]
        public List<MailpitMessageSummary> Messages { get; set; } = [];
    }

    private sealed class MailpitMessageSummary
    {
        [JsonPropertyName("ID")]
        public string Id { get; set; } = string.Empty;
    }

    private sealed class MailpitMessage
    {
        [JsonPropertyName("Text")]
        public string? Text { get; set; }

        [JsonPropertyName("HTML")]
        public string? Html { get; set; }
    }
}
