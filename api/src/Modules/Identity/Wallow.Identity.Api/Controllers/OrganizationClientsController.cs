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
/// The org-scoped client surface: an organization's admins and managers register and manage the
/// clients it owns, developer applications and service accounts alike. A client of another
/// organization is answered as not found, never forbidden.
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
    private const string RedirectUrisField = "redirectUris";
    private const string PostLogoutRedirectUrisField = "postLogoutRedirectUris";
    private const string BackchannelLogoutUriField = "backchannelLogoutUri";
    private const string ScopesField = "scopes";
    private const string RefreshTokenLifetimeField = "refreshTokenLifetime";

    /// <summary>
    /// Register a developer application or a service account for the organization. The response
    /// carries the client secret exactly once. A service account ignores every URI field.
    /// </summary>
    [HttpPost]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [EnableRateLimiting("registration")]
    [ProducesResponseType(typeof(OrganizationClientRegistrationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
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

        // The service publishes each client event through the outbox, in the same transaction as
        // its writes — a post-commit publish here would reopen the crash window that drops the
        // event and, for registration, leaves the client without its branding row.
        OrganizationClientRegistrationResult result = await clients.RegisterAsync(
            orgId,
            new RegisterClientInput(kind.Value, request.Name.Trim(), configuration, brandingDisplayName, brandingTagline),
            Actor(),
            ct);

        return CreatedAtAction(nameof(GetById), new { orgId, clientId = result.Client.ClientId }, Reveal(result));
    }

    /// <summary>
    /// Replace the client secret. The response carries the new secret exactly once; the old one
    /// stops working immediately. <c>revokeActiveTokens</c> also ends every token the client was
    /// already issued.
    /// </summary>
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

    /// <summary>List the clients the organization owns.</summary>
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

    /// <summary>Get one of the organization's clients.</summary>
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
    /// Replace a client's redirect URIs, logout URI and scopes. Name and client id are immutable;
    /// a service account's URI fields are ignored.
    /// </summary>
    [HttpPatch("{clientId}")]
    [EnableRateLimiting("registration")]
    [HasPermission(PermissionType.OrganizationClientsManage)]
    [ProducesResponseType(typeof(OrganizationClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
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
    /// Suspend a client: every token it was issued stops working now and its realtime connections
    /// are closed, while its configuration, branding and consents are kept for reinstatement.
    /// </summary>
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

    /// <summary>Reinstate a suspended client exactly as it was.</summary>
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
    /// Place the platform's own suspension on the client, with a reason (global admins only).
    /// While it stands the client is refused everywhere, whatever its own status says, and the
    /// organization can read the reason but not lift it.
    /// </summary>
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
    /// Lift the platform suspension (global admins only). The client serves again unless the
    /// organization's own suspension still stands.
    /// </summary>
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
    /// Delete one of the organization's clients for good: every credential it holds is revoked
    /// first, then the client, its consents and its branding are removed.
    /// </summary>
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

    // Mirrors OrganizationsController: the caller's own tenant and the global admin reach every
    // organization; anyone else only through a membership that carries the permission.
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

    // Suspend and reinstate are the same request shape around a different transition: address the
    // organization, apply the transition (which publishes its own event, in its own transaction),
    // hand back the client as it now is.
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
    /// Validates and trims the optional initial branding. A service account carries none — it
    /// faces no end user. An application's effective display name (the branded one, or the
    /// client's name when none was given) may never read as the platform itself.
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
            ModelState.AddModelError("branding.displayName", "Display name must be at most 200 characters.");
        }

        if (tagline is { Length: > 500 })
        {
            ModelState.AddModelError("branding.tagline", "Tagline must be at most 500 characters.");
        }

        string effectiveDisplayName = displayName ?? request.Name?.Trim() ?? string.Empty;
        if (effectiveDisplayName.Length > 0 && forkBranding.Value.IsReservedDisplayName(effectiveDisplayName))
        {
            ModelState.AddModelError(
                "branding.displayName",
                $"'{forkBranding.Value.AppName}' is reserved for the platform itself.");
        }

        return (displayName, tagline);
    }

    private static string? TrimmedOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Who is acting and from where, for the audit trail the service's events carry.
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

    // What OpenIddict advertises as the issuer when none is configured: the origin it was reached on.
    private string RequestOrigin() => $"{Request.Scheme}://{Request.Host}";

    /// <summary>
    /// Validates the configuration half of a register or update request under the shared client
    /// URI rules, recording every refusal against its field so one response names them all.
    /// Returns <see langword="null"/> when anything was refused. A service account has no URI
    /// fields to validate: whatever the request carries there is dropped, not refused.
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
            // A service account never holds the refresh grant, so a lifetime is dropped with the
            // URI fields rather than refused.
            return valid ? new ClientConfigurationInput([], [], null, scopes) : null;
        }

        if (redirectValues.Count == 0)
        {
            valid = false;
            ModelState.AddModelError(RedirectUrisField, "At least one redirect URI is required.");
        }

        valid &= TryParseRedirectUris(redirectValues, RedirectUrisField, out List<Uri> redirectUris);
        valid &= TryParseRedirectUris(postLogoutValues, PostLogoutRedirectUrisField, out List<Uri> postLogoutRedirectUris);

        // Every org-registered client holds a secret, so plain http is within the rule here.
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
