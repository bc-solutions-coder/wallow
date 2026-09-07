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
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Drives cookie sign-in, PKCE authorization, consent, code exchange, and refresh over HTTP.
/// The HTTPS test address allows Secure cookies to be sent. JWT readers decode payloads
/// without validating their signatures.
/// </summary>
public sealed class AuthorizationCodeFlowHarness : IDisposable
{
    /// <summary>The redirect URI <see cref="RegisterClientAsync"/> registers by default.</summary>
    public const string RedirectUri = "https://localhost/oidc/callback";

    /// <summary>The form field the consent screen posts its server-issued token under.</summary>
    public const string ConsentTokenField = AuthorizationController.ConsentTokenParameter;

    /// <summary>The form field carrying the user's answer: <c>granted</c> or <c>denied</c>.</summary>
    public const string ConsentDecisionField = AuthorizationController.ConsentDecisionParameter;

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

    /// <summary>
    /// Cookie-bearing client shared by this harness instance.
    /// </summary>
    public HttpClient Client => _client;

    /// <summary>
    /// Logs in and exchanges the returned ticket to establish the auth cookie.
    /// Uses a local return URL so the exchange does not depend on an AuthUrl fallback.
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
    /// Requests a PKCE authorization code and returns the response, including refusals or consent redirects.
    /// The optional organization argument sends the organization hint.
    /// </summary>
    public async Task<AuthorizeOutcome> AuthorizeAsync(
        string clientId,
        string scope,
        string redirectUri = RedirectUri,
        string? extraQuery = null,
        string? organization = null)
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

        if (!string.IsNullOrEmpty(organization))
        {
            query += $"&{AuthorizationController.OrganizationParameter}={Uri.EscapeDataString(organization)}";
        }

        if (!string.IsNullOrEmpty(extraQuery))
        {
            query += "&" + extraQuery;
        }

        using HttpResponseMessage response = await _client.GetAsync(
            new Uri($"/connect/authorize?{query}", UriKind.Relative));

        return await ReadAuthorizeOutcomeAsync(response, verifier);
    }

    /// <summary>
    /// Posts the request parameters from the consent return URL together with the decision.
    /// A null consentToken uses the redirect token; an empty string omits the token field.
    /// </summary>
    public async Task<AuthorizeOutcome> ConsentAsync(
        AuthorizeOutcome consentRedirect,
        bool grant,
        string? consentToken = null)
    {
        ArgumentNullException.ThrowIfNull(consentRedirect);

        if (consentRedirect.ReturnUrl is null)
        {
            throw new InvalidOperationException(
                $"The authorize endpoint did not redirect to the consent screen: {consentRedirect.Location}");
        }

        Dictionary<string, string> form = new(StringComparer.Ordinal);
        int separator = consentRedirect.ReturnUrl.IndexOf('?', StringComparison.Ordinal);
        string path = separator >= 0 ? consentRedirect.ReturnUrl[..separator] : consentRedirect.ReturnUrl;
        if (separator >= 0)
        {
            foreach ((string key, StringValues values) in QueryHelpers.ParseQuery(consentRedirect.ReturnUrl[separator..]))
            {
                form[key] = values.ToString();
            }
        }

        string token = consentToken ?? consentRedirect.ConsentToken ?? string.Empty;
        if (token.Length > 0)
        {
            form[ConsentTokenField] = token;
        }

        form[ConsentDecisionField] = grant ? AuthorizationController.ConsentGranted : AuthorizationController.ConsentDenied;

        using FormUrlEncodedContent content = new(form);
        using HttpResponseMessage response = await _client.PostAsync(new Uri(path, UriKind.Relative), content);

        return await ReadAuthorizeOutcomeAsync(response, consentRedirect.CodeVerifier);
    }

    private static async Task<AuthorizeOutcome> ReadAuthorizeOutcomeAsync(
        HttpResponseMessage response,
        string verifier)
    {
        Uri? location = response.Headers.Location;
        string? code = null;
        string? error = null;
        string? errorDescription = null;
        string? returnUrl = null;
        string? consentToken = null;

        if (location is not null)
        {
            string target = location.IsAbsoluteUri ? location.Query : location.OriginalString;
            int separator = target.IndexOf('?', StringComparison.Ordinal);
            if (separator >= 0 || location.IsAbsoluteUri)
            {
                Dictionary<string, StringValues> parsed = QueryHelpers.ParseQuery(
                    separator >= 0 ? target[separator..] : target);
                code = Single(parsed, "code");

                // Handle protocol errors and auth-app reason redirects.
                error = Single(parsed, "error") ?? Single(parsed, "reason");
                errorDescription = Single(parsed, "error_description");

                // Capture the fields needed to submit consent.
                returnUrl = Single(parsed, "returnUrl");
                consentToken = Single(parsed, ConsentTokenField);
            }
        }

        return new AuthorizeOutcome(
            response.StatusCode,
            location,
            code,
            error,
            verifier,
            await response.Content.ReadAsStringAsync(),
            returnUrl,
            consentToken,
            errorDescription);
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

    /// <summary>
    /// Submits a refresh grant and returns the token endpoint response.
    /// </summary>
    public Task<TokenOutcome> RefreshAsync(string clientId, string clientSecret, string refreshToken) =>
        PostTokenAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        });

    /// <summary>
    /// Runs authorization and code exchange, throwing if authorization returns no code.
    /// Does not automatically answer a consent prompt.
    /// </summary>
    public async Task<TokenOutcome> AcquireTokensAsync(
        string clientId,
        string clientSecret,
        string scope,
        string redirectUri = RedirectUri,
        string? organization = null)
    {
        AuthorizeOutcome authorize = await AuthorizeAsync(clientId, scope, redirectUri, organization: organization);
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
    /// Creates or updates a confidential client with authorization-code and refresh permissions.
    /// firstParty selects implicit consent; other clients use explicit consent.
    /// The optional tenantId sets the organization binding and is rejected for first-party clients.
    /// </summary>
    public static async Task RegisterClientAsync(
        IServiceProvider services,
        string clientId,
        string clientSecret,
        Guid? tenantId,
        IEnumerable<string> scopes,
        string redirectUri = RedirectUri,
        bool firstParty = false,
        string? frontchannelLogoutUri = null,
        string? backchannelLogoutUri = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(scopes);

        if (firstParty && tenantId is not null)
        {
            throw new ArgumentException(
                "A first-party client is never bound to an organization; pass the organization hint to AuthorizeAsync instead.",
                nameof(tenantId));
        }

        IOpenIddictApplicationManager applications =
            services.GetRequiredService<IOpenIddictApplicationManager>();

        OpenIddictApplicationDescriptor descriptor = new()
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            DisplayName = clientId,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = firstParty
                ? OpenIddictConstants.ConsentTypes.Implicit
                : OpenIddictConstants.ConsentTypes.Explicit,
        };

        descriptor.RedirectUris.Add(new Uri(redirectUri));
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);

        foreach (string scope in scopes)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
        }

        if (tenantId is { } boundTenantId)
        {
            descriptor.SetTenantId(boundTenantId.ToString());
        }

        if (frontchannelLogoutUri is not null)
        {
            descriptor.SetFrontchannelLogoutUri(new Uri(frontchannelLogoutUri));
        }

        if (backchannelLogoutUri is not null)
        {
            descriptor.SetBackchannelLogoutUri(new Uri(backchannelLogoutUri));
        }

        object? existing = await applications.FindByClientIdAsync(clientId, ct);
        if (existing is not null)
        {
            await applications.UpdateAsync(existing, descriptor, ct);
            return;
        }

        await applications.CreateAsync(descriptor, ct);
    }

    /// <summary>
    /// Creates a user with confirmed email for the configured password sign-in flow.
    /// </summary>
    public static async Task<Guid> CreateUserAsync(
        IServiceProvider services,
        string email,
        string password)
    {
        ArgumentNullException.ThrowIfNull(services);

        UserManager<WallowUser> users = services.GetRequiredService<UserManager<WallowUser>>();

        WallowUser user = WallowUser.Create("Harness", "User", email, TimeProvider.System);
        user.EmailConfirmed = true;

        IdentityResult result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create '{email}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        return user.Id;
    }

    /// <summary>
    /// Adds a named-role membership through the organization service.
    /// Use a different owner when the test user should not already have the creator role.
    /// </summary>
    public static async Task EnrollMemberAsync(
        IServiceProvider services,
        Guid organizationId,
        Guid userId,
        string roleName,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        IOrganizationService organizations = services.GetRequiredService<IOrganizationService>();
        await organizations.AddMemberAsync(organizationId, userId, roleName, Guid.NewGuid(), ct);
    }

    /// <summary>
    /// Reads claim values from the decoded payload, flattening one array level without validating the JWT.
    /// </summary>
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

    /// <summary>
    /// Decodes the JWT payload without verifying its signature or claims.
    /// </summary>
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
            ReadString(document.RootElement, "id_token"),
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

/// <summary>
/// Authorization response fields parsed from the redirect and response body.
/// ReturnUrl and ConsentToken capture consent-flow fields when present;
/// ErrorDescription captures the relying-party error description.
/// </summary>
public sealed record AuthorizeOutcome(
    HttpStatusCode StatusCode,
    Uri? Location,
    string? Code,
    string? Error,
    string CodeVerifier,
    string Body,
    string? ReturnUrl = null,
    string? ConsentToken = null,
    string? ErrorDescription = null);

/// <summary>What the token endpoint answered.</summary>
public sealed record TokenOutcome(
    HttpStatusCode StatusCode,
    string? AccessToken,
    string? RefreshToken,
    string? IdToken,
    string? Scope,
    string? Error,
    string Body)
{
    /// <summary>The access token, or a failure naming what the endpoint said instead.</summary>
    public string RequireAccessToken() => AccessToken
        ?? throw new InvalidOperationException(string.Create(
            CultureInfo.InvariantCulture,
            $"The token endpoint issued no access token ({(int)StatusCode}): {Body}"));

    /// <summary>The id_token, or a failure naming what the endpoint said instead.</summary>
    public string RequireIdToken() => IdToken
        ?? throw new InvalidOperationException(string.Create(
            CultureInfo.InvariantCulture,
            $"The token endpoint issued no id_token ({(int)StatusCode}): {Body}"));
}
