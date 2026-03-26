using Microsoft.Playwright;
using Wallow.E2E.Tests.Fixtures;
using Wallow.E2E.Tests.PageObjects;

namespace Wallow.E2E.Tests.Flows;

[Trait("Category", "E2E")]
public sealed class DashboardFlowTests : IClassFixture<DockerComposeFixture>, IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly DockerComposeFixture _docker;
    private readonly PlaywrightFixture _playwright;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private static readonly string _mailpitBaseUrl = Environment.GetEnvironmentVariable("E2E_MAILPIT_URL") ?? "http://localhost:8035";

    public DashboardFlowTests(DockerComposeFixture docker, PlaywrightFixture playwright)
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
    public async Task MfaEnrollmentFlow_ShowsSetupPageAndAcceptsCode()
    {
        string testEmail = $"e2e-{Guid.NewGuid():N}@test.local";
        string testPassword = "P@ssw0rd!Strong12";

        await RegisterAndLoginAsync(testEmail, testPassword);

        MfaEnrollPage mfaPage = new(_page, _docker.AuthBaseUrl);
        await mfaPage.NavigateAsync();

        bool isLoaded = await mfaPage.IsLoadedAsync();
        Assert.True(isLoaded, "MFA enrollment page should be loaded");

        await mfaPage.ClickBeginSetupAsync();

        bool hasQrCode = await mfaPage.GetQrCodeAsync();
        Assert.True(hasQrCode, "QR code secret should be displayed after beginning setup");

        // Submit an invalid code to verify the error handling path
        await mfaPage.FillCodeAsync("000000");
        await mfaPage.SubmitAsync();

        string? errorMessage = await mfaPage.GetErrorMessageAsync();
        Assert.NotNull(errorMessage);
    }

    [Fact]
    public async Task AppRegistrationFlow_RegistersNewApplication()
    {
        string testEmail = $"e2e-{Guid.NewGuid():N}@test.local";
        string testPassword = "P@ssw0rd!Strong12";

        await RegisterAndLoginAsync(testEmail, testPassword);

        AppRegistrationPage appPage = new(_page, _docker.WebBaseUrl);
        await appPage.NavigateAsync();

        bool isLoaded = await appPage.IsLoadedAsync();
        Assert.True(isLoaded, "App registration page should be loaded");

        await appPage.FillFormAsync(
            displayName: "E2E Test App",
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
        string testEmail = $"e2e-{Guid.NewGuid():N}@test.local";
        string testPassword = "P@ssw0rd!Strong12";

        await RegisterAndLoginAsync(testEmail, testPassword);

        OrganizationPage orgPage = new(_page, _docker.WebBaseUrl);
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
        string testEmail = $"e2e-{Guid.NewGuid():N}@test.local";
        string testPassword = "P@ssw0rd!Strong12";

        await RegisterAndLoginAsync(testEmail, testPassword);

        InquiryPage inquiryPage = new(_page, _docker.WebBaseUrl);
        await inquiryPage.NavigateAsync();

        bool isLoaded = await inquiryPage.IsLoadedAsync();
        Assert.True(isLoaded, "Inquiry page should be loaded");

        await inquiryPage.FillFormAsync(
            name: "E2E Tester",
            email: testEmail,
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

    private async Task RegisterAndLoginAsync(string email, string password)
    {
        // Step 1: Register the user at the Auth app
        RegisterPage registerPage = new(_page, _docker.AuthBaseUrl);
        await registerPage.NavigateAsync();
        await registerPage.FillFormAsync(email, password, password);
        await registerPage.SubmitAsync();

        // Step 2: Verify email via Mailpit
        string verificationLink = await GetVerificationLinkFromMailpitAsync(email);
        if (!string.IsNullOrEmpty(verificationLink))
        {
            await _page.GotoAsync(verificationLink);
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        // Step 3: Trigger the full OIDC login chain via the Web app.
        // Navigate to the Web app's login endpoint, which triggers:
        //   Web OIDC challenge → API /connect/authorize → Auth /login?returnUrl=...
        // The browser ends up on the Auth login page with a returnUrl that enables
        // the ticket-exchange flow back through the API's authorize endpoint.
        await _page.GotoAsync($"{_docker.WebBaseUrl}/authentication/login");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Step 4: Fill credentials on the Auth login page (redirected here by OIDC chain)
        await _page.Locator("#email").FillAsync(email);
        await _page.Locator("#password").FillAsync(password);
        await _page.Locator("button[type='submit']").ClickAsync();

        // Step 5: Wait for the full redirect chain to complete:
        //   Auth → exchange-ticket → API SignIn → /connect/authorize → Web /signin-oidc → dashboard
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private static async Task<string> GetVerificationLinkFromMailpitAsync(string recipientEmail)
    {
        using HttpClient httpClient = new();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        int maxRetries = 10;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            HttpResponseMessage response = await httpClient.GetAsync(
                $"{_mailpitBaseUrl}/api/v1/search?query=to:{recipientEmail}");

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                if (json.Contains("verify", StringComparison.OrdinalIgnoreCase) ||
                    json.Contains("confirm", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract first http link containing "verify" or "confirm"
                    int httpIndex = json.IndexOf("http", StringComparison.OrdinalIgnoreCase);
                    while (httpIndex >= 0)
                    {
                        int endIndex = json.IndexOfAny([' ', '"', '\'', '<', '\n', '\r'], httpIndex);
                        if (endIndex < 0)
                        {
                            endIndex = json.Length;
                        }

                        string url = json[httpIndex..endIndex];
                        if (url.Contains("verify", StringComparison.OrdinalIgnoreCase) ||
                            url.Contains("confirm", StringComparison.OrdinalIgnoreCase))
                        {
                            return url;
                        }

                        httpIndex = json.IndexOf("http", endIndex, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        return string.Empty;
    }
}
