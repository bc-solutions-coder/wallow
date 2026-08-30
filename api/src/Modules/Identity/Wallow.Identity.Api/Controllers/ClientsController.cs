using System.Security.Cryptography;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Api.Extensions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Helpers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Identity;
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
// Guards are per-action, and the class level must stay bare: stacked [HasPermission] attributes
// are separate policies that ALL have to pass, so a class-level AdminAccess would put the
// service-account actions back behind admin regardless of what each action declares.
public class ClientsController(
    IOpenIddictApplicationManager applicationManager,
    IServiceAccountService serviceAccountService) : ControllerBase
{
    /// <summary>
    /// Enough to sign a user in and keep them signed in, which is what a relying party registered
    /// through this endpoint exists to do. API scopes stay opt-in.
    /// </summary>
    private static readonly string[] _defaultScopes =
        [Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.Roles, Scopes.OfflineAccess];

    /// <summary>
    /// What an administrator may grant a client. <c>roles</c> sits outside
    /// <see cref="ApiScopes.LoginScopes"/> because self-service app registration does not hand a
    /// developer's app the user's role list; an administrator registering a first-party relying
    /// party may.
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
                FrontchannelLogoutUri = descriptor.GetFrontchannelLogoutUri()?.AbsoluteUri
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
            FrontchannelLogoutUri = descriptor.GetFrontchannelLogoutUri()?.AbsoluteUri
        });
    }

    [HttpPost]
    [HasPermission(PermissionType.AdminAccess)]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

        if (!RedirectUrisAreAcceptable(request.RedirectUris, request.PostLogoutRedirectUris))
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

        // Without these the client is refused every scope it asks for on its first authorize:
        // OpenIddict grants only what the application's own permissions list allows.
        foreach (string scope in scopes)
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        descriptor.SetFrontchannelLogoutUri(frontchannelLogoutUri);

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
            FrontchannelLogoutUri = frontchannelLogoutUri?.AbsoluteUri
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

        if (!RedirectUrisAreAcceptable(request.RedirectUris, request.PostLogoutRedirectUris))
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

        // Null clears the registration: Update replaces the whole mutable surface, so an omitted
        // URI opts the client back out of logout notifications rather than keeping the old one.
        descriptor.SetFrontchannelLogoutUri(frontchannelLogoutUri);

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
            FrontchannelLogoutUri = frontchannelLogoutUri?.AbsoluteUri
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
            FrontchannelLogoutUri = descriptor.GetFrontchannelLogoutUri()?.AbsoluteUri
        });
    }

    /// <summary>
    /// List all service accounts (client-credentials clients) for the current tenant.
    /// </summary>
    [HttpGet("service-accounts")]
    [HasPermission(PermissionType.ServiceAccountsRead)]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceAccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ServiceAccountDto>>> ListServiceAccounts(CancellationToken ct)
    {
        IReadOnlyList<ServiceAccountDto> accounts = await serviceAccountService.ListAsync(ct);
        return Ok(accounts);
    }

    /// <summary>
    /// Create a new service account. Returns the client secret which will NOT be shown again.
    /// </summary>
    [HttpPost("service-accounts")]
    [HasPermission(PermissionType.ServiceAccountsWrite)]
    [ProducesResponseType(typeof(ServiceAccountCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ServiceAccountCreatedResponse>> CreateServiceAccount(
        [FromBody] Contracts.Requests.CreateServiceAccountRequest request,
        CancellationToken ct)
    {
        Application.DTOs.CreateServiceAccountRequest appRequest = new(
            request.Name,
            request.Description,
            request.Scopes);

        ServiceAccountCreatedResult result = await serviceAccountService.CreateAsync(appRequest, ct);

        ServiceAccountCreatedResponse response = new()
        {
            Id = result.Id.Value.ToString(),
            ClientId = result.ClientId,
            ClientSecret = result.ClientSecret,
            TokenEndpoint = result.TokenEndpoint,
            Scopes = result.Scopes.ToList()
        };

        return CreatedAtAction(nameof(GetServiceAccount), new { id = result.Id.Value }, response);
    }

    /// <summary>
    /// Get a specific service account by ID.
    /// </summary>
    [HttpGet("service-accounts/{id:guid}")]
    [HasPermission(PermissionType.ServiceAccountsRead)]
    [ProducesResponseType(typeof(ServiceAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceAccountDto>> GetServiceAccount(Guid id, CancellationToken ct)
    {
        ServiceAccountDto? account = await serviceAccountService.GetAsync(ServiceAccountMetadataId.Create(id), ct);
        return account is null ? NotFound() : Ok(account);
    }

    /// <summary>
    /// Update the scopes assigned to a service account. Write-level rather than manage: creation
    /// already accepts arbitrary scopes, so gating updates higher would guard nothing.
    /// </summary>
    [HttpPut("service-accounts/{id:guid}/scopes")]
    [HasPermission(PermissionType.ServiceAccountsWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateServiceAccountScopes(
        Guid id,
        [FromBody] UpdateScopesRequest request,
        CancellationToken ct)
    {
        await serviceAccountService.UpdateScopesAsync(
            ServiceAccountMetadataId.Create(id),
            request.Scopes,
            ct);
        return NoContent();
    }

    /// <summary>
    /// Rotate the client secret for a service account. Returns the new secret which will NOT be shown again.
    /// </summary>
    [HttpPost("service-accounts/{id:guid}/rotate-secret")]
    [HasPermission(PermissionType.ServiceAccountsManage)]
    [ProducesResponseType(typeof(SecretRotatedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SecretRotatedResponse>> RotateServiceAccountSecret(Guid id, CancellationToken ct)
    {
        SecretRotatedResult result = await serviceAccountService.RotateSecretAsync(
            ServiceAccountMetadataId.Create(id),
            ct);

        return Ok(new SecretRotatedResponse
        {
            NewClientSecret = result.NewClientSecret,
            RotatedAt = result.RotatedAt
        });
    }

    /// <summary>
    /// Revoke and delete a service account.
    /// </summary>
    [HttpDelete("service-accounts/{id:guid}")]
    [HasPermission(PermissionType.ServiceAccountsManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RevokeServiceAccount(Guid id, CancellationToken ct)
    {
        await serviceAccountService.RevokeAsync(
            ServiceAccountMetadataId.Create(id),
            ct);
        return NoContent();
    }

    /// <summary>
    /// The same rule the organization surface and seed sync apply: absolute, fragment-free,
    /// HTTPS or loopback HTTP. Records the offending list in ModelState and reports whether
    /// both lists passed.
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
    /// The logout page loads this URI in a hidden iframe on the OP's own origin, so anything
    /// other than an absolute http(s) location is either unloadable there or a script vector.
    /// A null input is valid and parses to null (the client opts out of notifications).
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
