using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Wallow.Identity.Api.Authorization;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Helpers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Enums;
using Wallow.Shared.Kernel.Configuration;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Api.Controllers;

/// <summary>
/// Manages organization-owned applications and service accounts. Inaccessible organizations
/// and clients outside the addressed organization return not found.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/organizations/{orgId:guid}/clients")]
[Authorize]
[TypeFilter(typeof(RefusePlatformSuspendedOrganizationFilter))]
[Tags("Organization Clients")]
[Produces("application/json")]
[Consumes("application/json")]
public class OrganizationClientsController(
    IOrganizationClientService clients,
    ITenantContext tenantContext,
    IOrganizationAccessPolicy accessPolicy,
    IOptions<ForkBrandingOptions> forkBranding) : ControllerBase
{
    // ModelState keys name request members; the problem contract camelCases each dot segment on the wire.
    private const string RedirectUrisField = nameof(RegisterOrganizationClientRequest.RedirectUris);
    private const string PostLogoutRedirectUrisField = nameof(RegisterOrganizationClientRequest.PostLogoutRedirectUris);
    private const string BackchannelLogoutUriField = nameof(RegisterOrganizationClientRequest.BackchannelLogoutUri);
    private const string ScopesField = nameof(RegisterOrganizationClientRequest.Scopes);
    private const string RefreshTokenLifetimeField = nameof(RegisterOrganizationClientRequest.RefreshTokenLifetime);
    private const string BrandingDisplayNameField =
        nameof(RegisterOrganizationClientRequest.Branding) + "." + nameof(RegisterOrganizationClientBranding.DisplayName);
    private const string BrandingTaglineField =
        nameof(RegisterOrganizationClientRequest.Branding) + "." + nameof(RegisterOrganizationClientBranding.Tagline);

    /// <summary>
    /// Register an organization client.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationClientsManage. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Creates a confidential
    /// application for authorization code with PKCE and refresh tokens, or a service-account for client credentials.
    /// Applications require redirect URIs; both kinds require scopes and reject platform-only API scopes. Returns the
    /// client secret once, with issuer and API URLs.
    /// </remarks>
    [HttpPost]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [EnableRateLimiting("registration")]
    [ProducesResponseType(typeof(OrganizationClientRegistrationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrganizationClientRegistrationResponse>> Register(
        Guid orgId,
        [FromBody] RegisterOrganizationClientRequest request,
        CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(orgId, ct))
        {
            return NotFound();
        }

        RegisteredClientKind? kind = OrganizationClientResponse.ParseKind(request.Kind);
        if (kind is null)
        {
            ModelState.AddModelError(
                nameof(request.Kind),
                $"Kind must be '{OrganizationClientResponse.ApplicationKind}' or '{OrganizationClientResponse.ServiceAccountKind}'.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            ModelState.AddModelError(nameof(request.Name), "Name is required.");
        }
        else if (request.Name.Trim().Length > 200)
        {
            ModelState.AddModelError(nameof(request.Name), "Name must be at most 200 characters.");
        }

        (string? brandingDisplayName, string? brandingTagline) = NormalizeBranding(kind, request);

        ClientConfigurationInput? configuration = ParseConfiguration(
            kind ?? RegisteredClientKind.Application,
            request.RedirectUris,
            request.PostLogoutRedirectUris,
            request.BackchannelLogoutUri,
            request.BackchannelLogoutSessionRequired,
            request.Scopes,
            request.RefreshTokenLifetime);
        if (kind is null || configuration is null || !ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // Registration and its outbox event commit together in the service.
        OrganizationClientRegistrationResult result = await clients.RegisterAsync(
            orgId,
            new RegisterClientInput(kind.Value, request.Name.Trim(), configuration, brandingDisplayName, brandingTagline),
            Actor(),
            ct);

        return CreatedAtAction(nameof(GetById), new { orgId, clientId = result.Client.ClientId }, Reveal(result));
    }

    /// <summary>
    /// Rotate an organization client secret.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationClientsManage. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Replaces the secret for a
    /// client owned by the addressed organization and reveals the new secret once. Set revokeActiveTokens to revoke
    /// issued access as part of rotation.
    /// </remarks>
    [HttpPost("{clientId}/rotate-secret")]
    [EnableRateLimiting("registration")]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [ProducesResponseType(typeof(OrganizationClientRegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationClientRegistrationResponse>> RotateSecret(
        Guid orgId,
        string clientId,
        [FromBody] RotateOrganizationClientSecretRequest request,
        CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(orgId, ct))
        {
            return NotFound();
        }

        OrganizationClientRegistrationResult? result = await clients.RotateSecretAsync(
            orgId, clientId, request.RevokeActiveTokens, Actor(), ct);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(Reveal(result));
    }

    /// <summary>
    /// List organization clients.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationClientsManage. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Returns owned applications and
    /// service accounts with configuration and status, without secrets.
    /// </remarks>
    [HttpGet]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [ProducesResponseType(typeof(IReadOnlyList<OrganizationClientResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<OrganizationClientResponse>>> List(Guid orgId, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(orgId, ct))
        {
            return NotFound();
        }

        IReadOnlyList<OrganizationClientDto> result = await clients.ListAsync(orgId, ct);
        return Ok(result.Select(OrganizationClientResponse.From).ToList());
    }

    /// <summary>
    /// Get an organization client.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationClientsManage. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Returns configuration and
    /// status without a secret. A missing client or one owned by another organization returns 404.
    /// </remarks>
    [HttpGet("{clientId}")]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [ProducesResponseType(typeof(OrganizationClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationClientResponse>> GetById(Guid orgId, string clientId, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(orgId, ct))
        {
            return NotFound();
        }

        OrganizationClientDto? client = await clients.GetAsync(orgId, clientId, ct);
        return client is null ? NotFound() : Ok(OrganizationClientResponse.From(client));
    }

    /// <summary>
    /// Replace organization client configuration.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationClientsManage. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Replaces application redirect
    /// URIs, logout settings, and scopes; null refreshTokenLifetime preserves the current value, in seconds. Service
    /// accounts ignore URI and lifetime settings. Returns the updated client; unknown or platform-only scopes are
    /// rejected.
    /// </remarks>
    [HttpPatch("{clientId}")]
    [EnableRateLimiting("registration")]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [ProducesResponseType(typeof(OrganizationClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrganizationClientResponse>> Update(
        Guid orgId,
        string clientId,
        [FromBody] UpdateOrganizationClientRequest request,
        CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(orgId, ct))
        {
            return NotFound();
        }

        // The kind decides which fields the request must carry, so it is read before validation.
        OrganizationClientDto? existing = await clients.GetAsync(orgId, clientId, ct);
        if (existing is null)
        {
            return NotFound();
        }

        ClientConfigurationInput? configuration = ParseConfiguration(
            existing.Kind,
            request.RedirectUris,
            request.PostLogoutRedirectUris,
            request.BackchannelLogoutUri,
            request.BackchannelLogoutSessionRequired,
            request.Scopes,
            request.RefreshTokenLifetime);
        if (configuration is null || !ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        OrganizationClientDto? client = await clients.UpdateAsync(orgId, clientId, configuration, ct);

        return client is null ? NotFound() : Ok(OrganizationClientResponse.From(client));
    }

    /// <summary>
    /// Suspend an organization client.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationClientsManage. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Revokes client access while
    /// retaining registration, branding, and permanent consents. Returns the updated client status.
    /// </remarks>
    [HttpPost("{clientId}/suspend")]
    [EnableRateLimiting("registration")]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [ProducesResponseType(typeof(OrganizationClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<ActionResult<OrganizationClientResponse>> Suspend(Guid orgId, string clientId, CancellationToken ct)
    {
        return TransitionAsync(
            orgId,
            clientId,
            (organizationId, id, token) => clients.SuspendAsync(organizationId, id, Actor(), token),
            ct);
    }

    /// <summary>
    /// Reinstate an organization client.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationClientsManage. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Lifts the client suspension
    /// imposed by its organization and returns its status. Revoked tokens stay revoked, and platform or organization
    /// restrictions still apply.
    /// </remarks>
    [HttpPost("{clientId}/reinstate")]
    [EnableRateLimiting("registration")]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [ProducesResponseType(typeof(OrganizationClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<ActionResult<OrganizationClientResponse>> Reinstate(Guid orgId, string clientId, CancellationToken ct)
    {
        return TransitionAsync(
            orgId,
            clientId,
            (organizationId, id, token) => clients.ReinstateAsync(organizationId, id, Actor(), token),
            ct);
    }

    /// <summary>
    /// Suspend a client at platform level.
    /// </summary>
    /// <remarks>
    /// Requires a global administrator and a client owned by the addressed organization. Records the supplied reason,
    /// revokes client access, and returns the updated client. A missing client or organization ownership mismatch
    /// returns 404.
    /// </remarks>
    [HttpPost("{clientId}/platform-suspension")]
    [EnableRateLimiting("registration")]
    [ProducesResponseType(typeof(OrganizationClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrganizationClientResponse>> PlacePlatformSuspension(
        Guid orgId,
        string clientId,
        [FromBody] PlatformSuspensionRequest request,
        CancellationToken ct)
    {
        if (!User.IsGlobalAdmin())
        {
            return Forbid();
        }

        return await TransitionAsync(
            orgId,
            clientId,
            (organizationId, id, token) => clients.SuspendByPlatformAsync(
                organizationId, id, request.Reason, Actor(), token),
            ct);
    }

    /// <summary>
    /// Lift a client platform suspension.
    /// </summary>
    /// <remarks>
    /// Requires a global administrator and a client owned by the addressed organization. Clears the platform
    /// suspension and returns the updated client. Revoked tokens and other client or organization restrictions remain
    /// unchanged.
    /// </remarks>
    [HttpDelete("{clientId}/platform-suspension")]
    [EnableRateLimiting("registration")]
    [ProducesResponseType(typeof(OrganizationClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrganizationClientResponse>> LiftPlatformSuspension(
        Guid orgId,
        string clientId,
        CancellationToken ct)
    {
        if (!User.IsGlobalAdmin())
        {
            return Forbid();
        }

        return await TransitionAsync(
            orgId,
            clientId,
            (organizationId, id, token) => clients.ReinstateByPlatformAsync(organizationId, id, Actor(), token),
            ct);
    }

    /// <summary>
    /// Delete an organization client.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationClientsManage. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Revokes access and deletes the
    /// client and its authorizations, then requests branding cleanup. A missing client or one owned by another
    /// organization returns 404.
    /// </remarks>
    [HttpDelete("{clientId}")]
    [EnableRateLimiting("registration")]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid orgId, string clientId, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(orgId, ct))
        {
            return NotFound();
        }

        if (!await clients.DeleteAsync(orgId, clientId, Actor(), ct))
        {
            return NotFound();
        }

        return NoContent();
    }

    // Accept the resolved tenant, a global administrator, or a permitted foreign membership.
    private async Task<bool> CanAddressOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        if (orgId == tenantContext.TenantId.Value || User.IsGlobalAdmin())
        {
            return true;
        }

        return Guid.TryParse(User.GetUserId(), out Guid callerId)
            && await accessPolicy.HasPermissionInOrganizationAsync(
                orgId, callerId, PermissionType.OrganizationClientsManage, ct);
    }


    private async Task<ActionResult<OrganizationClientResponse>> TransitionAsync(
        Guid orgId,
        string clientId,
        Func<Guid, string, CancellationToken, Task<OrganizationClientDto?>> transition,
        CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(orgId, ct))
        {
            return NotFound();
        }

        OrganizationClientDto? client = await transition(orgId, clientId, ct);
        return client is null ? NotFound() : Ok(OrganizationClientResponse.From(client));
    }

    /// <summary>
    /// Trims application branding and rejects excessive lengths or reserved platform display names.
    /// Service-account branding is ignored.
    /// </summary>
    private (string? DisplayName, string? Tagline) NormalizeBranding(
        RegisteredClientKind? kind, RegisterOrganizationClientRequest request)
    {
        if (kind != RegisteredClientKind.Application)
        {
            return (null, null);
        }

        string? displayName = TrimmedOrNull(request.Branding?.DisplayName);
        string? tagline = TrimmedOrNull(request.Branding?.Tagline);

        if (displayName is { Length: > 200 })
        {
            ModelState.AddModelError(BrandingDisplayNameField, "Display name must be at most 200 characters.");
        }

        if (tagline is { Length: > 500 })
        {
            ModelState.AddModelError(BrandingTaglineField, "Tagline must be at most 500 characters.");
        }

        string effectiveDisplayName = displayName ?? request.Name?.Trim() ?? string.Empty;
        if (effectiveDisplayName.Length > 0 && forkBranding.Value.IsReservedDisplayName(effectiveDisplayName))
        {
            ModelState.AddModelError(
                BrandingDisplayNameField,
                $"'{forkBranding.Value.AppName}' is reserved for the platform itself.");
        }

        return (displayName, tagline);
    }

    private static string? TrimmedOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();


    private ClientActorContext Actor() => new(
        Guid.Parse(User.GetUserId()!),
        HttpContext.Connection.RemoteIpAddress?.ToString());

    private OrganizationClientRegistrationResponse Reveal(OrganizationClientRegistrationResult result) =>
        new()
        {
            Client = OrganizationClientResponse.From(result.Client),
            ClientSecret = result.ClientSecret,
            Issuer = result.Issuer ?? RequestOrigin(),
            ApiBaseUrl = result.ApiBaseUrl ?? RequestOrigin(),
        };

    // Use the request origin when the service supplies no configured endpoint.
    private string RequestOrigin() => $"{Request.Scheme}://{Request.Host}";

    /// <summary>
    /// Validates scopes, lifetime, and application URIs, recording field errors.
    /// Service-account URI fields are ignored.
    /// </summary>
    private ClientConfigurationInput? ParseConfiguration(
        RegisteredClientKind kind,
        IReadOnlyList<string> redirectValues,
        IReadOnlyList<string> postLogoutValues,
        string? backchannelValue,
        bool backchannelSessionRequired,
        IReadOnlyList<string> scopes,
        int? refreshTokenLifetime)
    {
        bool valid = true;
        if (scopes.Count == 0)
        {
            valid = false;
            ModelState.AddModelError(ScopesField, "At least one scope is required.");
        }

        if (refreshTokenLifetime is { } lifetime && !ClientRefreshTokenLifetimes.IsInRange(lifetime))
        {
            valid = false;
            ModelState.AddModelError(RefreshTokenLifetimeField, ClientRefreshTokenLifetimes.RangeMessage);
        }

        if (kind == RegisteredClientKind.ServiceAccount)
        {
            // Ignore a valid lifetime for service accounts; range validation still applies.
            return valid ? new ClientConfigurationInput([], [], null, scopes) : null;
        }

        if (redirectValues.Count == 0)
        {
            valid = false;
            ModelState.AddModelError(RedirectUrisField, "At least one redirect URI is required.");
        }

        valid &= TryParseRedirectUris(redirectValues, RedirectUrisField, out List<Uri> redirectUris);
        valid &= TryParseRedirectUris(postLogoutValues, PostLogoutRedirectUrisField, out List<Uri> postLogoutRedirectUris);

        // Organization clients are confidential, so back-channel HTTP is allowed.
        Uri? backchannelLogoutUri = null;
        if (!string.IsNullOrWhiteSpace(backchannelValue)
            && !ClientUriRules.TryParseBackchannelLogoutUri(
                backchannelValue, isConfidential: true, out backchannelLogoutUri))
        {
            valid = false;
            ModelState.AddModelError(BackchannelLogoutUriField, ClientUriRules.BackchannelLogoutUriError);
        }

        return valid
            ? new ClientConfigurationInput(
                redirectUris,
                postLogoutRedirectUris,
                backchannelLogoutUri,
                scopes,
                backchannelSessionRequired,
                refreshTokenLifetime)
            : null;
    }

    private bool TryParseRedirectUris(IReadOnlyList<string> values, string field, out List<Uri> uris)
    {
        uris = new List<Uri>(values.Count);
        bool valid = true;
        foreach (string value in values)
        {
            if (ClientUriRules.TryParseRedirectUri(value, out Uri? uri))
            {
                uris.Add(uri);
            }
            else
            {
                valid = false;
                ModelState.AddModelError(field, $"'{value}': {ClientUriRules.RedirectUriError}");
            }
        }

        return valid;
    }
}
