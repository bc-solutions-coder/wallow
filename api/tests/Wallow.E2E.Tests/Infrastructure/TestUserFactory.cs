using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Wallow.E2E.Tests.Infrastructure;

public static class TestUserFactory
{
    private const string TestPassword = "P@ssw0rd!Strong12";
    private const int MaxMailRetries = 15;
    private static readonly TimeSpan _mailPollInterval = TimeSpan.FromSeconds(2);

    public static async Task<TestUser> CreateAsync(string apiBaseUrl, string mailpitBaseUrl)
    {
        string email = $"e2e-{Guid.NewGuid():N}@test.local";

        await RegisterUserAsync(apiBaseUrl, email);
        await VerifyEmailAsync(apiBaseUrl, mailpitBaseUrl, email);

        return new TestUser(email, TestPassword);
    }

    public static async Task<UnverifiedTestUser> CreateUnverifiedAsync(string apiBaseUrl, string mailpitBaseUrl)
    {
        string email = $"e2e-{Guid.NewGuid():N}@test.local";

        await RegisterUserAsync(apiBaseUrl, email);

        string verificationLink = await MailpitHelper.SearchForLinkAsync(
            mailpitBaseUrl, email, "verify", MaxMailRetries, (int)_mailPollInterval.TotalSeconds);

        if (string.IsNullOrEmpty(verificationLink))
        {
            verificationLink = await MailpitHelper.SearchForLinkAsync(
                mailpitBaseUrl, email, "confirm", maxRetries: 3, pollIntervalSeconds: 1);
        }

        if (string.IsNullOrEmpty(verificationLink))
        {
            throw new InvalidOperationException(
                $"Failed to retrieve verification email for {email} after {MaxMailRetries} attempts.");
        }

        return new UnverifiedTestUser(email, TestPassword, verificationLink);
    }

    public static async Task<MfaTestUser> CreateWithMfaAsync(string apiBaseUrl, string mailpitBaseUrl)
    {
        TestUser user = await CreateAsync(apiBaseUrl, mailpitBaseUrl);
        using HttpClient httpClient = CreateCookieClient();

        bool loggedIn = await LoginAndExchangeTicketAsync(httpClient, apiBaseUrl, user.Email, user.Password);
        if (!loggedIn)
        {
            throw new InvalidOperationException(
                "CreateWithMfaAsync failed: the shared org requires MFA (login returned partial auth). " +
                "This usually means a prior test contaminated the org. Reset the database or check test isolation.");
        }

        // Begin TOTP enrollment
        HttpResponseMessage enrollResponse = await httpClient.PostAsync(
            $"{apiBaseUrl}/v1/identity/mfa/enroll/totp", null);
        enrollResponse.EnsureSuccessStatusCode();

        JsonElement enrollResult = await enrollResponse.Content.ReadFromJsonAsync<JsonElement>();
        string secret = enrollResult.GetProperty("secret").GetString()
            ?? throw new InvalidOperationException("TOTP enrollment did not return a secret.");

        // Confirm enrollment with a valid TOTP code
        string code = await TotpHelper.GenerateFreshCodeAsync(secret);
        HttpResponseMessage confirmResponse = await httpClient.PostAsJsonAsync(
            $"{apiBaseUrl}/v1/identity/mfa/enroll/confirm",
            new { secret, code });
        confirmResponse.EnsureSuccessStatusCode();

        JsonElement confirmResult = await confirmResponse.Content.ReadFromJsonAsync<JsonElement>();
        List<string> backupCodes = confirmResult.GetProperty("backupCodes")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToList();

        return new MfaTestUser(user.Email, user.Password, secret, backupCodes);
    }

    public static async Task<OrgAdminTestUser> CreateOrgAdminAsync(string apiBaseUrl, string mailpitBaseUrl)
    {
        TestUser user = await CreateAsync(apiBaseUrl, mailpitBaseUrl);
        using HttpClient httpClient = CreateCookieClient();

        await LoginAndExchangeTicketAsync(httpClient, apiBaseUrl, user.Email, user.Password);

        // Create an isolated org so this test's org settings don't pollute the shared "Wallow" org
        HttpResponseMessage isolateResponse = await httpClient.PostAsJsonAsync(
            $"{apiBaseUrl}/v1/identity/test/isolated-org",
            new { requireMfa = false, gracePeriodDays = 0 });
        isolateResponse.EnsureSuccessStatusCode();

        JsonElement isolateResult = await isolateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Guid orgId = isolateResult.GetProperty("orgId").GetGuid();

        // Allow cache/projection propagation before the test navigates to the OIDC flow
        await Task.Delay(500);

        // Re-login with a fresh client to get a cookie carrying the org_id claim
        (HttpClient freshClient, HttpClientHandler freshHandler) = CreateCookieClientWithHandler();
        using (freshClient)
        {
            await LoginAndExchangeTicketAsync(freshClient, apiBaseUrl, user.Email, user.Password);
            string authCookie = freshHandler.CookieContainer.GetCookieHeader(new Uri(apiBaseUrl));
            return new OrgAdminTestUser(user.Email, user.Password, orgId, authCookie);
        }
    }

    public static Task<MfaTestUser> CreateInMfaRequiredOrgAsync(string apiBaseUrl, string mailpitBaseUrl)
        => CreateInMfaRequiredOrgAsync(apiBaseUrl, mailpitBaseUrl, gracePeriodDays: 0);

    public static async Task<MfaTestUser> CreateInMfaRequiredOrgAsync(
        string apiBaseUrl, string mailpitBaseUrl, int gracePeriodDays)
    {
        TestUser user = await CreateAsync(apiBaseUrl, mailpitBaseUrl);
        using HttpClient httpClient = CreateCookieClient();

        await LoginAndExchangeTicketAsync(httpClient, apiBaseUrl, user.Email, user.Password);

        // Create an isolated org so MFA settings don't pollute the shared "Wallow" org
        HttpResponseMessage isolateResponse = await httpClient.PostAsJsonAsync(
            $"{apiBaseUrl}/v1/identity/test/isolated-org",
            new { requireMfa = true, gracePeriodDays });
        isolateResponse.EnsureSuccessStatusCode();

        // Allow cache/projection propagation before the test navigates to the OIDC flow
        await Task.Delay(500);

        return new MfaTestUser(user.Email, user.Password, string.Empty, []);
    }

    private static HttpClient CreateCookieClient()
    {
        (HttpClient client, _) = CreateCookieClientWithHandler();
        return client;
    }

    private static (HttpClient Client, HttpClientHandler Handler) CreateCookieClientWithHandler()
    {
#pragma warning disable CA2000 // Handler is disposed by HttpClient via disposeHandler: true
        HttpClientHandler handler = new()
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            CheckCertificateRevocationList = true
        };
#pragma warning restore CA2000
        HttpClient client = new(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(30) };
        return (client, handler);
    }

    /// <summary>
    /// Logs in and exchanges the sign-in ticket for a full auth cookie.
    /// Returns true when full auth was obtained, false when MFA enrollment is required
    /// (no grace period) — in that case only a partial auth cookie is available.
    /// </summary>
    private static async Task<bool> LoginAndExchangeTicketAsync(
        HttpClient httpClient, string apiBaseUrl, string email, string password)
    {
        HttpResponseMessage loginResponse = await httpClient.PostAsJsonAsync(
            $"{apiBaseUrl}/v1/identity/auth/login",
            new { email, password, rememberMe = false });
        loginResponse.EnsureSuccessStatusCode();

        JsonElement loginResult = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();

        bool succeeded = loginResult.GetProperty("succeeded").GetBoolean();
        bool mfaRequired = loginResult.TryGetProperty("mfaRequired", out JsonElement mfaProp) && mfaProp.GetBoolean();
        bool mfaEnrollmentRequired = loginResult.TryGetProperty("mfaEnrollmentRequired", out JsonElement enrollProp) && enrollProp.GetBoolean();

        if (!succeeded && !mfaRequired && !mfaEnrollmentRequired)
        {
            throw new InvalidOperationException(
                $"Login failed for {email}: {loginResult}");
        }

        // When MFA enrollment is required without a grace period, the login issues a partial
        // cookie instead of a sign-in ticket. The org already requires MFA — no ticket to exchange.
        if (mfaEnrollmentRequired && !succeeded)
        {
            return false;
        }

        string signInTicket = loginResult.GetProperty("signInTicket").GetString()
            ?? throw new InvalidOperationException("Login did not return a signInTicket.");

        // Exchange the ticket for an auth cookie
        HttpResponseMessage exchangeResponse = await httpClient.GetAsync(
            $"{apiBaseUrl}/v1/identity/auth/exchange-ticket?ticket={Uri.EscapeDataString(signInTicket)}");
        exchangeResponse.EnsureSuccessStatusCode();
        return true;
    }

    private static async Task RegisterUserAsync(string apiBaseUrl, string email)
    {
        using HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

        object payload = new
        {
            email,
            password = TestPassword,
            confirmPassword = TestPassword,
            clientId = "wallow-web-client",
        };

        HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            $"{apiBaseUrl}/v1/identity/auth/register", payload);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Confirms the account by redeeming the emailed link's token against the API.
    /// </summary>
    /// <remarks>
    /// This deliberately calls the API rather than fetching the emailed link itself. The link
    /// points at the auth app's <c>/verify-email/confirm</c> screen, which redeems the token from
    /// the browser after hydration. Fetching that URL over plain HTTP returns 200 for the
    /// server-rendered shell without ever redeeming anything, so a fetch-and-check-the-status
    /// helper would report success while leaving the account unverified — every later login then
    /// fails with 403 <c>email_not_confirmed</c>. Redeeming the token directly is what the screen's
    /// own client-side call does, and it does not depend on how the screen is rendered.
    /// </remarks>
    private static async Task VerifyEmailAsync(string apiBaseUrl, string mailpitBaseUrl, string email)
    {
        // Try "verify" first, fall back to "confirm"
        string verificationLink = await MailpitHelper.SearchForLinkAsync(
            mailpitBaseUrl, email, "verify", MaxMailRetries, (int)_mailPollInterval.TotalSeconds);

        if (string.IsNullOrEmpty(verificationLink))
        {
            verificationLink = await MailpitHelper.SearchForLinkAsync(
                mailpitBaseUrl, email, "confirm", maxRetries: 3, pollIntervalSeconds: 1);
        }

        if (string.IsNullOrEmpty(verificationLink))
        {
            throw new InvalidOperationException(
                $"Failed to retrieve verification email for {email} after {MaxMailRetries} attempts.");
        }

        string token = ExtractQueryParameter(verificationLink, "token")
            ?? throw new InvalidOperationException(
                $"Verification link for {email} carried no token: {verificationLink}");

        using HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
        string verifyUrl =
            $"{apiBaseUrl}/v1/identity/auth/verify-email?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";

        HttpResponseMessage verifyResponse = await httpClient.GetAsync(verifyUrl);
        verifyResponse.EnsureSuccessStatusCode();
    }

    private static string? ExtractQueryParameter(string url, string name)
    {
        string query = new Uri(url).Query;

        foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = pair.Split('=', 2);
            if (parts.Length == 2 && string.Equals(parts[0], name, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return null;
    }
}
