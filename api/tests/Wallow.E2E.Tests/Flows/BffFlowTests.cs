using System.Text.Json;
using Microsoft.Playwright;
using StackExchange.Redis;
using Wallow.E2E.Tests.Fixtures;
using Wallow.E2E.Tests.Infrastructure;
using Wallow.E2E.Tests.PageObjects;
using Xunit.Abstractions;

namespace Wallow.E2E.Tests.Flows;

/// <summary>
/// Drives the @bc-solutions-coder/sdk BFF reference example (apps/tanstack-min)
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

    /// <summary>
    /// Proves the server-side session pattern that the SDK's <c>ValkeySessionStore</c> exists to provide,
    /// which the cookie store cannot: after login the full session (including the access token) lives in
    /// Valkey while the cookie carries only an opaque reference, and logout deletes the Valkey record so
    /// the session is truly revoked server-side — not merely cleared from the browser.
    /// </summary>
    [Fact]
    [Trait("E2EGroup", "Bff")]
    public async Task BffSession_LivesInValkey_AndLogoutRevokesIt()
    {
        TestUser user = await TestUserFactory.CreateAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        await LoginToExampleAsync(user);

        await using ConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(Docker.ValkeyConnectionString);
        IDatabase db = redis.GetDatabase();
        IServer server = redis.GetServer(redis.GetEndPoints()[0]);

        // 1. The session — with its access token — is persisted server-side in Valkey, keyed by an
        //    opaque id. Correlate by the test user's unique email so parallel tests can't collide.
        StoredSession? stored = await FindSessionByEmailAsync(server, db, user.Email);
        Assert.True(
            stored is not null,
            $"No Valkey session record found for {user.Email} — the session was not persisted server-side.");
        Assert.False(
            string.IsNullOrWhiteSpace(stored!.Value.AccessToken),
            "The stored session carried no access token — tokens are not being held server-side.");

        // 2. The cookie is only an opaque reference: it must leak neither the access token nor the
        //    user's identity. This is the whole point of the server-side store.
        string sessionCookieValue = await GetBffSessionCookieValueAsync();
        Assert.DoesNotContain(stored.Value.AccessToken, sessionCookieValue);
        Assert.DoesNotContain(user.Email.ToLowerInvariant(), sessionCookieValue.ToLowerInvariant());

        // 3. Logout revokes the session server-side: the Valkey record is deleted, not just the cookie.
        await Page.GetByTestId("bff-logout").ClickAsync();
        await Page.WaitForURLAsync(
            url => url.StartsWith(Docker.BffBaseUrl, StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000 });
        await Assertions.Expect(Page.GetByTestId("bff-user-status"))
            .ToHaveTextAsync("anonymous", new() { Timeout = 15_000 });

        StoredSession? afterLogout = await FindSessionByEmailAsync(server, db, user.Email);
        Assert.True(
            afterLogout is null,
            "The Valkey session record survived logout — server-side revocation did not occur.");
    }

    private readonly record struct StoredSession(string Key, string AccessToken);

    /// <summary>
    /// Scans the BFF session namespace in Valkey for the record belonging to <paramref name="email"/>,
    /// returning its key and access token, or <c>null</c> when no such record exists.
    /// </summary>
    private static async Task<StoredSession?> FindSessionByEmailAsync(IServer server, IDatabase db, string email)
    {
        await foreach (RedisKey key in server.KeysAsync(pattern: "wallow:session:*"))
        {
            RedisValue value = await db.StringGetAsync(key);
            if (value.IsNullOrEmpty)
            {
                continue;
            }

            using JsonDocument document = JsonDocument.Parse(value.ToString());
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("user", out JsonElement userElement)
                || !userElement.TryGetProperty("email", out JsonElement emailElement)
                || !string.Equals(emailElement.GetString(), email, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string accessToken = root.TryGetProperty("accessToken", out JsonElement tokenElement)
                ? tokenElement.GetString() ?? string.Empty
                : string.Empty;
            return new StoredSession(key.ToString(), accessToken);
        }

        return null;
    }

    /// <summary>Returns the value of the opaque BFF session cookie set on the current browser context.</summary>
    private async Task<string> GetBffSessionCookieValueAsync()
    {
        IReadOnlyList<BrowserContextCookiesResult> cookies = await Context.CookiesAsync();
        foreach (BrowserContextCookiesResult cookie in cookies)
        {
            if (cookie.Name == "wallow_bff")
            {
                return cookie.Value;
            }
        }

        Assert.Fail("The BFF session cookie 'wallow_bff' was not set after login.");
        return string.Empty;
    }
}
