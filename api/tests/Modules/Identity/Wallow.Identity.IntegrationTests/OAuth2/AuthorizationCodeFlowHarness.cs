using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Drives the browser half of the OIDC server: password sign-in, the authorize endpoint with
/// PKCE, the code exchange and the refresh grant. The authorize endpoint reads the ASP.NET
/// Identity cookie and never a bearer token, so this holds its own cookie-bearing client on an
/// https base address — the auth cookie is marked Secure outside development and a CookieContainer
/// silently drops it over http. Access tokens are unencrypted JWTs here, so <see cref="ReadClaimValues"/>
/// reads them directly.
/// </summary>
public sealed class AuthorizationCodeFlowHarness : IDisposable
{
    /// <summary>The redirect URI <see cref="RegisterClientAsync"/> registers by default.</summary>
    public const string RedirectUri = "https://localhost/oidc/callback";

    /// <summary>
    /// Clients whose id starts with this skip the consent screen. A harness client that does not
    /// is answered with a redirect to the auth app's consent route, which nothing here can drive.
    /// </summary>
    public const string FirstPartyClientPrefix = "wallow-";

    private readonly HttpClient _client;

    public AuthorizationCodeFlowHarness(WallowApiFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost"),
        });
    }

    /// <summary>The cookie-bearing client every hop of the flow runs through.</summary>
    public HttpClient Client => _client;

    /// <summary>
    /// Signs a user in with their password and lands the auth cookie. Sign-in is two hops: the
    /// login endpoint issues a one-time ticket, and only the exchange sets the cookie. A local
    /// returnUrl is mandatory because the test host configures no AuthUrl to fall back to.
    /// </summary>
    public async Task SignInAsync(string email, string password)
    {
        using HttpResponseMessage login = await _client.PostAsJsonAsync(
            "/v1/identity/auth/login",
            new { email, password, rememberMe = false });

        string loginBody = await login.Content.ReadAsStringAsync();
        if (login.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException(
                $"Login for '{email}' failed with {(int)login.StatusCode}: {loginBody}");
        }

        using JsonDocument document = JsonDocument.Parse(loginBody);
        if (!document.RootElement.TryGetProperty("signInTicket", out JsonElement ticket)
            || ticket.GetString() is not string ticketValue)
        {
            throw new InvalidOperationException($"Login for '{email}' issued no ticket: {loginBody}");
        }

        using HttpResponseMessage exchange = await _client.GetAsync(
            new Uri(
                $"/v1/identity/auth/exchange-ticket?ticket={Uri.EscapeDataString(ticketValue)}&returnUrl=%2F",
                UriKind.Relative));

        if (exchange.StatusCode != HttpStatusCode.Found)
        {
            throw new InvalidOperationException(
                $"Ticket exchange for '{email}' set no cookie, answering {(int)exchange.StatusCode}: "
                + await exchange.Content.ReadAsStringAsync());
        }
    }

    /// <summary>
    /// Requests an authorization code. Returns the endpoint's answer whether it granted a code or
    /// refused, so a caller can assert on either.
    /// </summary>
    public async Task<AuthorizeOutcome> AuthorizeAsync(
        string clientId,
        string scope,
        string redirectUri = RedirectUri)
    {
        string verifier = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        string challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        string query = string.Join(
            '&',
            "response_type=code",
            $"client_id={Uri.EscapeDataString(clientId)}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            $"scope={Uri.EscapeDataString(scope)}",
            $"code_challenge={challenge}",
            "code_challenge_method=S256",
            "state=harness");

        using HttpResponseMessage response = await _client.GetAsync(
            new Uri($"/connect/authorize?{query}", UriKind.Relative));

        Uri? location = response.Headers.Location;
        string? code = null;
        string? error = null;

        if (location is not null)
        {
            string target = location.IsAbsoluteUri ? location.Query : location.OriginalString;
            int separator = target.IndexOf('?', StringComparison.Ordinal);
            if (separator >= 0 || location.IsAbsoluteUri)
            {
                Dictionary<string, StringValues> parsed = QueryHelpers.ParseQuery(
                    separator >= 0 ? target[separator..] : target);
                code = Single(parsed, "code");

                // A refusal from OpenIddict names 'error'; one the controller writes itself
                // redirects to the auth app's error screen and names 'reason'.
                error = Single(parsed, "error") ?? Single(parsed, "reason");
            }
        }

        return new AuthorizeOutcome(
            response.StatusCode,
            location,
            code,
            error,
            verifier,
            await response.Content.ReadAsStringAsync());
    }

    /// <summary>Exchanges an authorization code for tokens.</summary>
    public Task<TokenOutcome> ExchangeCodeAsync(
        string clientId,
        string clientSecret,
        string code,
        string codeVerifier,
        string redirectUri = RedirectUri) =>
        PostTokenAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code_verifier"] = codeVerifier,
        });

    /// <summary>Redeems a refresh token for a fresh set of tokens.</summary>
    public Task<TokenOutcome> RefreshAsync(string clientId, string clientSecret, string refreshToken) =>
        PostTokenAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        });

    /// <summary>
    /// Runs authorize then exchange for a caller that only wants the tokens, and throws when the
    /// authorize endpoint refuses rather than returning a token response with nothing in it.
    /// </summary>
    public async Task<TokenOutcome> AcquireTokensAsync(
        string clientId,
        string clientSecret,
        string scope,
        string redirectUri = RedirectUri)
    {
        AuthorizeOutcome authorize = await AuthorizeAsync(clientId, scope, redirectUri);
        if (authorize.Code is null)
        {
            throw new InvalidOperationException(
                $"The authorize endpoint issued no code for '{clientId}' "
                + $"({(int)authorize.StatusCode}, error '{authorize.Error}', location '{authorize.Location}').");
        }

        return await ExchangeCodeAsync(clientId, clientSecret, authorize.Code, authorize.CodeVerifier, redirectUri);
    }

    /// <summary>Creates an organization owned by the given user, and returns its id.</summary>
    public static async Task<Guid> CreateOrganizationAsync(
        IServiceProvider services,
        string name,
        Guid ownerUserId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        IOrganizationService organizations = services.GetRequiredService<IOrganizationService>();
        return await organizations.CreateOrganizationAsync(name, null, null, ownerUserId, ct);
    }

    /// <summary>
    /// Registers (or re-registers) a confidential client that can drive the authorization-code and
    /// refresh grants. The tenant property is what binds the client to an organization: a client
    /// carrying none resolves to the empty tenant, and the authorize endpoint then refuses every
    /// user as a non-member.
    /// </summary>
    public static async Task RegisterClientAsync(
        IServiceProvider services,
        string clientId,
        string clientSecret,
        Guid tenantId,
        IEnumerable<string> scopes,
        string redirectUri = RedirectUri,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(scopes);

        if (!clientId.StartsWith(FirstPartyClientPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"A harness client id must start with '{FirstPartyClientPrefix}' to skip consent.",
                nameof(clientId));
        }

        IOpenIddictApplicationManager applications =
            services.GetRequiredService<IOpenIddictApplicationManager>();

        OpenIddictApplicationDescriptor descriptor = new()
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            DisplayName = clientId,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
        };

        descriptor.RedirectUris.Add(new Uri(redirectUri));
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);

        foreach (string scope in scopes)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
        }

        descriptor.SetTenantId(tenantId.ToString());

        object? existing = await applications.FindByClientIdAsync(clientId, ct);
        if (existing is not null)
        {
            await applications.UpdateAsync(existing, descriptor, ct);
            return;
        }

        await applications.CreateAsync(descriptor, ct);
    }

    /// <summary>
    /// Creates a user who can complete a password sign-in. Confirming the email is not optional:
    /// the sign-in manager refuses an unconfirmed account outright. Any roles named must already
    /// exist; they decide which scopes the authorize endpoint is willing to grant.
    /// </summary>
    public static async Task<Guid> CreateUserAsync(
        IServiceProvider services,
        string email,
        string password,
        IEnumerable<string>? roles = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        UserManager<WallowUser> users = services.GetRequiredService<UserManager<WallowUser>>();

        WallowUser user = WallowUser.Create(Guid.Empty, "Harness", "User", email, TimeProvider.System);
        user.EmailConfirmed = true;

        IdentityResult result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create '{email}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        foreach (string role in roles ?? [])
        {
            IdentityResult assignment = await users.AddToRoleAsync(user, role);
            if (!assignment.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to grant '{email}' the '{role}' role: "
                    + string.Join("; ", assignment.Errors.Select(e => e.Description)));
            }
        }

        return user.Id;
    }

    /// <summary>Returns every value a JWT carries for the given claim, flattening array claims.</summary>
    public static IReadOnlyList<string> ReadClaimValues(string token, string claimType)
    {
        ArgumentNullException.ThrowIfNull(token);

        JsonElement payload = ReadPayload(token);
        if (!payload.TryGetProperty(claimType, out JsonElement claim))
        {
            return [];
        }

        return claim.ValueKind switch
        {
            JsonValueKind.Array => claim.EnumerateArray()
                .Select(element => element.ToString())
                .ToList(),
            JsonValueKind.Null or JsonValueKind.Undefined => [],
            _ => [claim.ToString()],
        };
    }

    /// <summary>Returns a JWT's decoded payload.</summary>
    public static JsonElement ReadPayload(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        string[] segments = token.Split('.');
        if (segments.Length < 2)
        {
            throw new ArgumentException("The token is not a JWT.", nameof(token));
        }

        using JsonDocument document = JsonDocument.Parse(Base64UrlDecode(segments[1]));
        return document.RootElement.Clone();
    }

    public void Dispose() => _client.Dispose();

    private async Task<TokenOutcome> PostTokenAsync(Dictionary<string, string> form)
    {
        using FormUrlEncodedContent content = new(form);
        using HttpResponseMessage response = await _client.PostAsync(
            new Uri("/connect/token", UriKind.Relative),
            content);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(body);

        return new TokenOutcome(
            response.StatusCode,
            ReadString(document.RootElement, "access_token"),
            ReadString(document.RootElement, "refresh_token"),
            ReadString(document.RootElement, "scope"),
            ReadString(document.RootElement, "error"),
            body);
    }

    private static string? Single(Dictionary<string, StringValues> query, string key) =>
        query.TryGetValue(key, out StringValues values) ? values.ToString() : null;

    private static string? ReadString(JsonElement root, string property) =>
        root.TryGetProperty(property, out JsonElement value) ? value.GetString() : null;

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty,
        };

        return Convert.FromBase64String(padded);
    }
}

/// <summary>What the authorize endpoint answered: a code, or the refusal it redirected to.</summary>
public sealed record AuthorizeOutcome(
    HttpStatusCode StatusCode,
    Uri? Location,
    string? Code,
    string? Error,
    string CodeVerifier,
    string Body);

/// <summary>What the token endpoint answered.</summary>
public sealed record TokenOutcome(
    HttpStatusCode StatusCode,
    string? AccessToken,
    string? RefreshToken,
    string? Scope,
    string? Error,
    string Body)
{
    /// <summary>The access token, or a failure naming what the endpoint said instead.</summary>
    public string RequireAccessToken() => AccessToken
        ?? throw new InvalidOperationException(string.Create(
            CultureInfo.InvariantCulture,
            $"The token endpoint issued no access token ({(int)StatusCode}): {Body}"));
}
