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

        // Wait for the OIDC redirect chain to reach the dashboard
        await Page.WaitForURLAsync(
            url => url.Contains("/dashboard", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000 });

        // Each test's page object NavigateAsync waits for Blazor ready on the target page,
        // so we only need to confirm the OIDC chain completed successfully here.
    }
}
