using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Wallow.E2E.Tests.Fixtures;
using Wallow.E2E.Tests.Infrastructure;
using Wallow.E2E.Tests.PageObjects;
using Xunit.Abstractions;

namespace Wallow.E2E.Tests.Flows;

/// <summary>
/// Pins the contract that the auth origin is served by the React app
/// (apps/wallow-auth) rather than the Blazor Wallow.Auth app (Wallow-vec7.5.1).
/// </summary>
/// <remarks>
/// The login / register / MFA challenge / MFA enroll / logout behaviours themselves are
/// already covered by AuthFlowTests, MfaFlowTests, AltAuthFlowTests and
/// EmailVerificationFlowTests, all of which drive Docker.AuthBaseUrl through the shared page
/// objects. Porting them to React is a stack change, not a test change: once the auth service
/// serves apps/wallow-auth those suites are the port's behavioural oracle, so they are not
/// duplicated here.
/// <para>
/// What they do not cover is what this file adds: that the app answering at the auth origin is
/// the React one (its readiness signal, and the absence of Blazor's), that each auth route is
/// actually rendered by the standalone host rather than 404'd by the bare reverse proxy, and
/// the consent screen — which has no E2E coverage at all today, because every seeded client is
/// first-party (AuthorizationController's "wallow-" prefix plus Identity:FirstPartyClients) and
/// so never reaches it.
/// </para>
/// </remarks>
[Trait("Category", "E2E")]
[Trait("E2EGroup", "AuthApp")]
public sealed class AuthAppFlowTests : AuthenticatedE2ETestBase
{
    public AuthAppFlowTests(DockerComposeFixture docker, PlaywrightFixture playwright, ITestOutputHelper output)
        : base(docker, playwright, output)
    {
    }

    /// <summary>
    /// Every auth route the standalone host must render. The bare reverse proxy in
    /// apps/wallow-auth/src/lib/auth-server.ts serves /health, /v1/** and /connect/** and 404s
    /// everything else, so a route only appears here once the host server-renders the router.
    /// </summary>
    public static TheoryData<string> AuthRoutes =>
    [
        "/login",
        "/register",
        "/forgot-password",
        "/logout",
        "/terms",
        "/privacy",
    ];

    [Theory]
    [MemberData(nameof(AuthRoutes))]
    public async Task AuthRoute_IsServedByReactAppAndSignalsReady(string route)
    {
        await Page.GotoAsync($"{Docker.AuthBaseUrl}{route}");

        await E2ETestBase.WaitForAuthReadyAsync(Page);

        int readyCount = await Page.GetByTestId("auth-ready").CountAsync();
        Assert.True(readyCount == 1, $"{route} should carry exactly one auth-ready marker, found {readyCount}");

        int blazorCount = await Page.Locator("[data-blazor-ready]").CountAsync();
        Assert.True(blazorCount == 0, $"{route} should be served by the React app, but it emitted Blazor's ready signal");
    }

    [Fact]
    public async Task LoginScreen_RendersCredentialFormOnceReady()
    {
        await Page.GotoAsync($"{Docker.AuthBaseUrl}/login");
        await E2ETestBase.WaitForAuthReadyAsync(Page);

        await Assertions.Expect(Page.GetByTestId("login-email")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByTestId("login-password")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByTestId("login-submit")).ToBeEnabledAsync();
    }

    [Fact]
    public async Task ConsentScreen_ShowsRequestedScopesForThirdPartyClient()
    {
        string clientId = await RegisterThirdPartyClientAsync();

        await GotoAuthorizeAsync(clientId);

        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex("/consent", RegexOptions.IgnoreCase),
            new() { Timeout = 30_000 });
        await E2ETestBase.WaitForAuthReadyAsync(Page);

        await Assertions.Expect(Page.GetByTestId("consent-heading")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByTestId("consent-scopes")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByTestId("consent-approve")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByTestId("consent-deny")).ToBeVisibleAsync();

        string scopes = await Page.GetByTestId("consent-scopes").InnerTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(scopes), "Consent screen should list the requested scopes");
    }

    [Fact]
    public async Task ConsentScreen_DenyReturnsAccessDeniedToClient()
    {
        string clientId = await RegisterThirdPartyClientAsync();

        await GotoAuthorizeAsync(clientId);

        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex("/consent", RegexOptions.IgnoreCase),
            new() { Timeout = 30_000 });
        await E2ETestBase.WaitForAuthReadyAsync(Page);

        await Page.GetByTestId("consent-deny").ClickAsync();

        // Denial travels back to the client's redirect_uri as an OAuth error, per
        // AuthorizationController's consent_denied branch (Errors.ConsentRequired).
        await Page.WaitForURLAsync(
            url => url.Contains("error=", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000 });

        Assert.Contains("error=", Page.Url, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("code=", Page.Url, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The client's redirect_uri. The Web app's health endpoint is a stable 200 that parks the
    /// browser on a real page after the authorize leg resolves, so the assertions read the
    /// final URL rather than racing a callback handler that would reject the response anyway.
    /// </summary>
    private string RedirectUri => $"{Docker.WebBaseUrl}/health";

    /// <summary>
    /// Registers an OIDC client that is NOT first-party — the only way to reach the consent
    /// screen. AuthorizationController treats every "wallow-" prefixed client and everything in
    /// Identity:FirstPartyClients as pre-consented, which covers every client in api/seed.json,
    /// so the client has to be created per test through the dashboard's registration flow.
    /// </summary>
    private async Task<string> RegisterThirdPartyClientAsync()
    {
        AppRegistrationPage appPage = new(Page, Docker.WebBaseUrl);
        await appPage.NavigateAsync();

        await appPage.FillFormAsync(
            displayName: $"app-e2e-consent-{Guid.NewGuid():N}",
            clientType: "confidential",
            redirectUris: RedirectUri);
        await appPage.SubmitAsync();

        AppRegistrationResult result = await appPage.GetResultAsync();
        Assert.True(result.Success, $"Third-party client registration should succeed. Error: {result.ErrorMessage}");
        Assert.False(string.IsNullOrEmpty(result.ClientId), "Third-party client registration should return a client id");

        return result.ClientId!;
    }

    /// <summary>
    /// Starts an authorization request at the AUTH origin, not the API origin.
    /// </summary>
    /// <remarks>
    /// apps/wallow-auth reverse-proxies /connect/** at its root, which is the whole point of the
    /// unified origin: the Identity cookie is host-only on the auth origin, so an authorize
    /// request issued here carries it and the API sees an authenticated user. Issuing it against
    /// Docker.ApiBaseUrl instead would be cross-origin and arrive without that cookie.
    /// </remarks>
    private async Task GotoAuthorizeAsync(string clientId)
    {
        // PKCE is mandatory server-side (IdentityInfrastructureExtensions.cs:78 —
        // AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange()), so an authorize request
        // without a code_challenge is rejected with invalid_request before the consent screen is
        // ever reached. These tests stop at the consent decision and never redeem the code, so the
        // verifier itself is not needed beyond deriving the challenge.
        string codeVerifier = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        string codeChallenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

        string authorizeUrl =
            $"{Docker.AuthBaseUrl}/connect/authorize" +
            $"?client_id={Uri.EscapeDataString(clientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
            "&response_type=code" +
            "&scope=openid" +
            $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
            "&code_challenge_method=S256" +
            $"&state={Guid.NewGuid():N}";

        await Page.GotoAsync(authorizeUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Commit });
    }

    /// <summary>Base64url per RFC 7636 section 4.2: unpadded, with the URL-safe alphabet.</summary>
    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
