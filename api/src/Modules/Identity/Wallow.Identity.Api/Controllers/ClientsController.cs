using System.Security.Cryptography;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Api.Extensions;
using Wallow.Identity.Application.Helpers;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Kernel.Identity.Authorization;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/clients")]
[Authorize]
[Tags("Clients")]
[Produces("application/json")]
[Consumes("application/json")]
public class ClientsController(IOpenIddictApplicationManager applicationManager) : ControllerBase
{
    /// <summary>
    /// Sign-in scopes used when the request omits scopes or supplies an empty list.
    /// </summary>
    private static readonly string[] _defaultScopes =
        [Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.Roles, Scopes.OfflineAccess];

    /// <summary>
    /// Scopes this administrative endpoint permits, including roles beyond
    /// <see cref="ApiScopes.LoginScopes"/>.
    /// </summary>
    private static readonly HashSet<string> _grantableScopes =
        new(
            [.. ApiScopes.LoginScopes, .. ApiScopes.ValidScopes, Scopes.Roles],
            StringComparer.Ordinal);

    [HttpGet]
    [HasPermission(PermissionType.AdminAccess)]
    [ProducesResponseType(typeof(IReadOnlyList<ClientResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClientResponse>>> GetAll(CancellationToken ct)
    {
        List<ClientResponse> clients = [];

        await foreach (object application in applicationManager.ListAsync(int.MaxValue, 0, ct))
        {
            OpenIddictApplicationDescriptor descriptor = new();
            await applicationManager.PopulateAsync(descriptor, application, ct);

            string? id = await applicationManager.GetIdAsync(application, ct);
            string? clientId = await applicationManager.GetClientIdAsync(application, ct);

            clients.Add(new ClientResponse
            {
                Id = id ?? string.Empty,
                Name = descriptor.DisplayName ?? string.Empty,
                ClientId = clientId ?? string.Empty,
                RedirectUris = descriptor.RedirectUris.Select(u => u.ToString()).ToList(),
                PostLogoutRedirectUris = descriptor.PostLogoutRedirectUris.Select(u => u.ToString()).ToList(),
                Scopes = ScopesOf(descriptor),
                FrontchannelLogoutUri = descriptor.GetFrontchannelLogoutUri()?.AbsoluteUri,
                BackchannelLogoutUri = descriptor.GetBackchannelLogoutUri()?.AbsoluteUri,
                BackchannelLogoutSessionRequired = descriptor.GetBackchannelLogoutSessionRequired(),
                RefreshTokenLifetime = descriptor.GetRefreshTokenLifetimeSeconds()
            });
        }

        return Ok(clients);
    }

    [HttpGet("{id}")]
    [HasPermission(PermissionType.AdminAccess)]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientResponse>> GetById(string id, CancellationToken ct)
    {
        object? application = await applicationManager.FindByIdAsync(id, ct);
        if (application is null)
        {
            return NotFound();
        }

        OpenIddictApplicationDescriptor descriptor = new();
        await applicationManager.PopulateAsync(descriptor, application, ct);

        string? clientId = await applicationManager.GetClientIdAsync(application, ct);

        return Ok(new ClientResponse
        {
            Id = id,
            Name = descriptor.DisplayName ?? string.Empty,
            ClientId = clientId ?? string.Empty,
            RedirectUris = descriptor.RedirectUris.Select(u => u.ToString()).ToList(),
            PostLogoutRedirectUris = descriptor.PostLogoutRedirectUris.Select(u => u.ToString()).ToList(),
            Scopes = ScopesOf(descriptor),
            FrontchannelLogoutUri = descriptor.GetFrontchannelLogoutUri()?.AbsoluteUri,
            BackchannelLogoutUri = descriptor.GetBackchannelLogoutUri()?.AbsoluteUri,
            BackchannelLogoutSessionRequired = descriptor.GetBackchannelLogoutSessionRequired(),
            RefreshTokenLifetime = descriptor.GetRefreshTokenLifetimeSeconds()
        });
    }

    [HttpPost]
    [HasPermission(PermissionType.AdminAccess)]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ClientResponse>> Create(
        [FromBody] CreateClientRequest request,
        CancellationToken ct)
    {
        IReadOnlyList<string> scopes = request.Scopes is { Count: > 0 } ? request.Scopes : _defaultScopes;

        List<string> ungrantableScopes = scopes.Where(s => !_grantableScopes.Contains(s)).ToList();
        if (ungrantableScopes.Count > 0)
        {
            ModelState.AddModelError(
                nameof(request.Scopes),
                $"Unknown scopes: {string.Join(", ", ungrantableScopes)}.");
            return ValidationProblem(ModelState);
        }

        if (!TryParseFrontchannelLogoutUri(request.FrontchannelLogoutUri, out Uri? frontchannelLogoutUri))
        {
            ModelState.AddModelError(
                nameof(request.FrontchannelLogoutUri),
                FrontchannelLogoutUriError);
            return ValidationProblem(ModelState);
        }

        if (!TryParseBackchannelLogoutUri(request.BackchannelLogoutUri, out Uri? backchannelLogoutUri))
        {
            ModelState.AddModelError(
                nameof(request.BackchannelLogoutUri),
                ClientUriRules.BackchannelLogoutUriError);
            return ValidationProblem(ModelState);
        }

        if (!RedirectUrisAreAcceptable(request.RedirectUris, request.PostLogoutRedirectUris))
        {
            return ValidationProblem(ModelState);
        }

        if (!RefreshTokenLifetimeIsAcceptable(request.RefreshTokenLifetime))
        {
            return ValidationProblem(ModelState);
        }

        string clientSecret = GenerateClientSecret();

        OpenIddictApplicationDescriptor descriptor = new()
        {
            ClientId = Guid.NewGuid().ToString("N"),
            ClientSecret = clientSecret,
            DisplayName = request.Name,
            ClientType = ClientTypes.Confidential,
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.EndSession,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Revocation,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code
            }
        };

        // Register the scopes this client may request.
        foreach (string scope in scopes)
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        descriptor.SetFrontchannelLogoutUri(frontchannelLogoutUri);
        descriptor.SetBackchannelLogoutUri(backchannelLogoutUri);
        descriptor.SetBackchannelLogoutSessionRequired(request.BackchannelLogoutSessionRequired);

        // Pin the third-party lifetime default rather than relying on global configuration.
        int refreshTokenLifetime =
            request.RefreshTokenLifetime ?? ClientRefreshTokenLifetimes.ThirdPartyDefaultSeconds;
        descriptor.SetRefreshTokenLifetime(refreshTokenLifetime);

        foreach (string uri in request.RedirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(uri));
        }

        foreach (string uri in request.PostLogoutRedirectUris)
        {
            descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
        }

        object application = await applicationManager.CreateAsync(descriptor, ct);
        string? id = await applicationManager.GetIdAsync(application, ct);

        ClientResponse response = new()
        {
            Id = id ?? string.Empty,
            Name = request.Name,
            ClientId = descriptor.ClientId,
            ClientSecret = clientSecret,
            RedirectUris = request.RedirectUris,
            PostLogoutRedirectUris = request.PostLogoutRedirectUris,
            Scopes = scopes,
            FrontchannelLogoutUri = frontchannelLogoutUri?.AbsoluteUri,
            BackchannelLogoutUri = backchannelLogoutUri?.AbsoluteUri,
            BackchannelLogoutSessionRequired = request.BackchannelLogoutSessionRequired,
            RefreshTokenLifetime = refreshTokenLifetime
        };

        return CreatedAtAction(nameof(GetById), new { id }, response);
    }

    [HttpPut("{id}")]
    [HasPermission(PermissionType.AdminAccess)]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientResponse>> Update(
        string id,
        [FromBody] UpdateClientRequest request,
        CancellationToken ct)
    {
        if (!TryParseFrontchannelLogoutUri(request.FrontchannelLogoutUri, out Uri? frontchannelLogoutUri))
        {
            ModelState.AddModelError(
                nameof(request.FrontchannelLogoutUri),
                FrontchannelLogoutUriError);
            return ValidationProblem(ModelState);
        }

        if (!TryParseBackchannelLogoutUri(request.BackchannelLogoutUri, out Uri? backchannelLogoutUri))
        {
            ModelState.AddModelError(
                nameof(request.BackchannelLogoutUri),
                ClientUriRules.BackchannelLogoutUriError);
            return ValidationProblem(ModelState);
        }

        if (!RedirectUrisAreAcceptable(request.RedirectUris, request.PostLogoutRedirectUris))
        {
            return ValidationProblem(ModelState);
        }

        if (!RefreshTokenLifetimeIsAcceptable(request.RefreshTokenLifetime))
        {
            return ValidationProblem(ModelState);
        }

        object? application = await applicationManager.FindByIdAsync(id, ct);
        if (application is null)
        {
            return NotFound();
        }

        OpenIddictApplicationDescriptor descriptor = new();
        await applicationManager.PopulateAsync(descriptor, application, ct);

        descriptor.DisplayName = request.Name;

        // Null preserves the current lifetime; a value affects future refresh tokens.
        if (request.RefreshTokenLifetime is { } updatedLifetime)
        {
            descriptor.SetRefreshTokenLifetime(updatedLifetime);
        }

        // Omitted logout URLs remove their channel registrations.
        descriptor.SetFrontchannelLogoutUri(frontchannelLogoutUri);
        descriptor.SetBackchannelLogoutUri(backchannelLogoutUri);
        descriptor.SetBackchannelLogoutSessionRequired(request.BackchannelLogoutSessionRequired);

        descriptor.RedirectUris.Clear();
        foreach (string uri in request.RedirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(uri));
        }

        descriptor.PostLogoutRedirectUris.Clear();
        foreach (string uri in request.PostLogoutRedirectUris)
        {
            descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
        }

        await applicationManager.UpdateAsync(application, descriptor, ct);

        string? clientId = await applicationManager.GetClientIdAsync(application, ct);

        return Ok(new ClientResponse
        {
            Id = id,
            Name = request.Name,
            ClientId = clientId ?? string.Empty,
            RedirectUris = request.RedirectUris,
            PostLogoutRedirectUris = request.PostLogoutRedirectUris,
            Scopes = ScopesOf(descriptor),
            FrontchannelLogoutUri = frontchannelLogoutUri?.AbsoluteUri,
            BackchannelLogoutUri = backchannelLogoutUri?.AbsoluteUri,
            BackchannelLogoutSessionRequired = request.BackchannelLogoutSessionRequired,
            RefreshTokenLifetime = descriptor.GetRefreshTokenLifetimeSeconds()
        });
    }

    [HttpDelete("{id}")]
    [HasPermission(PermissionType.AdminAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(string id, CancellationToken ct)
    {
        object? application = await applicationManager.FindByIdAsync(id, ct);
        if (application is null)
        {
            return NotFound();
        }

        await applicationManager.DeleteAsync(application, ct);
        return NoContent();
    }

    [HttpPost("{id}/rotate-secret")]
    [HasPermission(PermissionType.AdminAccess)]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientResponse>> RotateSecret(string id, CancellationToken ct)
    {
        object? application = await applicationManager.FindByIdAsync(id, ct);
        if (application is null)
        {
            return NotFound();
        }

        string newSecret = GenerateClientSecret();

        OpenIddictApplicationDescriptor descriptor = new();
        await applicationManager.PopulateAsync(descriptor, application, ct);
        descriptor.ClientSecret = newSecret;
        await applicationManager.UpdateAsync(application, descriptor, ct);

        string? clientId = await applicationManager.GetClientIdAsync(application, ct);

        return Ok(new ClientResponse
        {
            Id = id,
            Name = descriptor.DisplayName ?? string.Empty,
            ClientId = clientId ?? string.Empty,
            ClientSecret = newSecret,
            RedirectUris = descriptor.RedirectUris.Select(u => u.ToString()).ToList(),
            PostLogoutRedirectUris = descriptor.PostLogoutRedirectUris.Select(u => u.ToString()).ToList(),
            Scopes = ScopesOf(descriptor),
            FrontchannelLogoutUri = descriptor.GetFrontchannelLogoutUri()?.AbsoluteUri,
            BackchannelLogoutUri = descriptor.GetBackchannelLogoutUri()?.AbsoluteUri,
            BackchannelLogoutSessionRequired = descriptor.GetBackchannelLogoutSessionRequired(),
            RefreshTokenLifetime = descriptor.GetRefreshTokenLifetimeSeconds()
        });
    }

    private bool RefreshTokenLifetimeIsAcceptable(int? refreshTokenLifetime)
    {
        if (refreshTokenLifetime is { } lifetime && !ClientRefreshTokenLifetimes.IsInRange(lifetime))
        {
            ModelState.AddModelError(
                nameof(CreateClientRequest.RefreshTokenLifetime),
                ClientRefreshTokenLifetimes.RangeMessage);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Records invalid redirect URIs using the shared HTTPS-or-loopback-HTTP rule.
    /// </summary>
    private bool RedirectUrisAreAcceptable(
        IReadOnlyList<string> redirectUris,
        IReadOnlyList<string> postLogoutRedirectUris)
    {
        if (ClientUriRules.FirstRefusedRedirect(redirectUris) is { } refusedRedirect)
        {
            ModelState.AddModelError(
                nameof(CreateClientRequest.RedirectUris),
                $"'{refusedRedirect}': {ClientUriRules.RedirectUriError}");
        }

        if (ClientUriRules.FirstRefusedRedirect(postLogoutRedirectUris) is { } refusedPostLogout)
        {
            ModelState.AddModelError(
                nameof(CreateClientRequest.PostLogoutRedirectUris),
                $"'{refusedPostLogout}': {ClientUriRules.RedirectUriError}");
        }

        return ModelState.IsValid;
    }

    private const string FrontchannelLogoutUriError =
        "The front-channel logout URI must be an absolute http or https URL.";

    /// <summary>
    /// Accepts absolute HTTP or HTTPS front-channel URLs. Null disables this channel.
    /// </summary>
    private static bool TryParseFrontchannelLogoutUri(string? value, out Uri? uri)
    {
        uri = null;
        if (value is null)
        {
            return true;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
        {
            uri = parsed;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Applies confidential-client back-channel URI rules. Null or whitespace disables this channel.
    /// </summary>
    private static bool TryParseBackchannelLogoutUri(string? value, out Uri? uri)
    {
        uri = null;
        return string.IsNullOrWhiteSpace(value)
            || ClientUriRules.TryParseBackchannelLogoutUri(value, isConfidential: true, out uri);
    }

    private static List<string> ScopesOf(OpenIddictApplicationDescriptor descriptor) =>
        descriptor.Permissions
            .Where(p => p.StartsWith(Permissions.Prefixes.Scope, StringComparison.Ordinal))
            .Select(p => p[Permissions.Prefixes.Scope.Length..])
            .ToList();

    private static string GenerateClientSecret()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }
}
