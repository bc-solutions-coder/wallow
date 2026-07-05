using Microsoft.Playwright;
using Wallow.E2E.Tests.Fixtures;
using Wallow.E2E.Tests.Infrastructure;
using Wallow.E2E.Tests.PageObjects;
using Xunit.Abstractions;

namespace Wallow.E2E.Tests.Flows;

/// <summary>
/// Drives the @bc-solutions-coder/sdk BFF reference example (packages/typescript-sdk/examples/tanstack-min)
/// through the full same-origin OIDC tunnel: anonymous -> login -> authenticated /api call with
/// silent refresh -> logout -> anonymous again. All browser state is exercised through the
/// example's `data-testid` DOM contract; credentials are entered on the real Wallow.Auth page.
/// </summary>
[Trait("Category", "E2E")]
public sealed class BffFlowTests : E2ETestBase
{
    public BffFlowTests(DockerComposeFixture docker, PlaywrightFixture playwright, ITestOutputHelper output)
        : base(docker, playwright, output)
    {
    }

    private static async Task<string> GetStatusAsync(IPage page)
    {
        ILocator status = page.GetByTestId("bff-user-status");
        await status.WaitForAsync(new() { Timeout = 15_000 });
        return (await status.InnerTextAsync()).Trim();
    }

    [Fact]
    [Trait("E2EGroup", "Bff")]
    public async Task BffTunnel_LoginAuthedCallLogout_CompletesFullFlow()
    {
        TestUser user = await TestUserFactory.CreateAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        // 1. Anonymous: the example calls getUser() -> GET /bff/user -> 401 -> null.
        await Page.GotoAsync($"{Docker.BffBaseUrl}/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        Assert.Equal("anonymous", await GetStatusAsync(Page));

        // 2. login() -> /bff/login -> OIDC challenge -> Wallow.Auth login page.
        await Page.GetByTestId("bff-login").ClickAsync();
        await Page.WaitForURLAsync(
            url => url.Contains("/login", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000 });
        await Page.GetByTestId("login-email").WaitForAsync(new() { Timeout = 15_000 });

        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.FillEmailAsync(user.Email);
        await loginPage.FillPasswordAsync(user.Password);
        await loginPage.SubmitAsync();

        // 3. Callback seals the session and returns to the example (returnTo "/").
        await Page.WaitForURLAsync(
            url => url.StartsWith(Docker.BffBaseUrl, StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000 });

        await Assertions.Expect(Page.GetByTestId("bff-user-status"))
            .ToHaveTextAsync("authenticated", new() { Timeout = 15_000 });
        await Assertions.Expect(Page.GetByTestId("bff-user-email"))
            .ToContainTextAsync(user.Email, new() { Timeout = 15_000 });

        // 4. Authenticated SDK call through the /api proxy returns 200 (with silent refresh).
        await Page.GetByTestId("bff-call-api").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("bff-api-result"))
            .ToContainTextAsync("200", new() { Timeout = 15_000 });

        // 5. logout() -> /bff/logout -> end-session -> back on the example, session cleared.
        await Page.GetByTestId("bff-logout").ClickAsync();
        await Page.WaitForURLAsync(
            url => url.StartsWith(Docker.BffBaseUrl, StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000 });

        // 6. getUser() returns null again -> anonymous.
        await Assertions.Expect(Page.GetByTestId("bff-user-status"))
            .ToHaveTextAsync("anonymous", new() { Timeout = 15_000 });
    }
}
