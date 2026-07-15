using Microsoft.Playwright;
using Wallow.E2E.Tests.Fixtures;
using Xunit.Abstractions;

namespace Wallow.E2E.Tests.Infrastructure;

[Trait("Category", "E2E")]
public abstract class AuthenticatedE2ETestBase : E2ETestBase
{
    protected TestUser TestUser { get; private set; } = null!;

    protected AuthenticatedE2ETestBase(
        DockerComposeFixture docker,
        PlaywrightFixture playwright,
        ITestOutputHelper? testOutputHelper = null)
        : base(docker, playwright, testOutputHelper)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        TestUser = await TestUserFactory.CreateAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        // Trigger the OIDC login chain via the Web app
        await Page.GotoAsync($"{Docker.WebBaseUrl}/authentication/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for the login form to be visible
        await Page.GetByTestId("login-email").WaitForAsync(new() { Timeout = 15_000 });

        // Fill credentials using data-testid selectors
        await Page.GetByTestId("login-email").FillAsync(TestUser.Email);
        await Page.GetByTestId("login-password").FillAsync(TestUser.Password);
        await Page.GetByTestId("login-submit").ClickAsync();

        // Wait for OIDC redirect chain to land on the dashboard or an MFA page.
        // Matching MFA URLs here turns a 30-second timeout into a fast-fail with a clear message.
        await Page.WaitForURLAsync(
            url => url.Contains("/dashboard", StringComparison.OrdinalIgnoreCase)
                || url.Contains("mfa/enroll", StringComparison.OrdinalIgnoreCase)
                || url.Contains("mfa/challenge", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000 });

        string landedUrl = Page.Url;
        if (landedUrl.Contains("mfa/enroll", StringComparison.OrdinalIgnoreCase)
            || landedUrl.Contains("mfa/challenge", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"AuthenticatedE2ETestBase: login redirected to MFA ({landedUrl}) instead of dashboard. " +
                "The shared org likely has requireMfa=true from a prior test. Reset the database or check test isolation.");
        }

        // Each test's page object NavigateAsync waits for Blazor ready on the target page,
        // so we only need to confirm the OIDC chain completed successfully here.
    }
}
