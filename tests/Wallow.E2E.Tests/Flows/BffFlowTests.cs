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

    /// <summary>
    /// Drives the example from anonymous through the real OIDC login and leaves the browser
    /// back on the example with a sealed BFF session.
    /// </summary>
    private async Task LoginToExampleAsync(TestUser user)
    {
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
    }

    [Fact]
    [Trait("E2EGroup", "Bff")]
    public async Task BffTunnel_LoginAuthedCallLogout_CompletesFullFlow()
    {
        TestUser user = await TestUserFactory.CreateAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        await LoginToExampleAsync(user);

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

    /// <summary>
    /// The CSRF gate on a real browser mutation. The proxy rejects any state-changing request
    /// that does not echo the session's CSRF token with a 403 <c>CSRF_INVALID</c> raised *before*
    /// the request is forwarded, so a mutation that comes back 204 from the API is proof of both
    /// halves of the double-submit: the browser sent the token, and the gate accepted it.
    ///
    /// Unit tests mock <c>fetch</c> and so cannot catch a regression where the real app stops
    /// attaching the header and every mutation silently 403s. This test can.
    /// </summary>
    [Fact]
    [Trait("E2EGroup", "Bff")]
    public async Task BffMutation_AfterLogin_CarriesCsrfTokenAndClearsTheGate()
    {
        TestUser user = await TestUserFactory.CreateAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        await LoginToExampleAsync(user);

        // Click the example's state-changing button and capture the proxied request it makes.
        // Creating an organization is granted to an ordinary signed-in user, so the only thing
        // standing between this POST and a 201 is the CSRF gate.
        IResponse mutation = await Page.RunAndWaitForResponseAsync(
            async () => await Page.GetByTestId("bff-mutate").ClickAsync(),
            response =>
                response.Url.Contains("/api/v1/identity/organizations", StringComparison.OrdinalIgnoreCase)
                && string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new PageRunAndWaitForResponseOptions { Timeout = 30_000 });

        // The browser really put the token on the wire (this is what the SDK's request
        // interceptor exists to do — mocked fetch can never prove it).
        Dictionary<string, string> headers = await mutation.Request.AllHeadersAsync();
        Assert.True(
            headers.TryGetValue("x-csrf-token", out string? csrfToken),
            "The mutation was sent without an x-csrf-token header — the SDK's CSRF interceptor did not run.");
        Assert.False(string.IsNullOrWhiteSpace(csrfToken), "The x-csrf-token header was empty.");

        // ...and the gate accepted it: 201 from the API, not the proxy's 403 CSRF_INVALID.
        Assert.Equal(201, mutation.Status);

        await Assertions.Expect(Page.GetByTestId("bff-mutate-result"))
            .ToContainTextAsync("201 created org", new() { Timeout = 15_000 });
    }
}
