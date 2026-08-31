using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;

namespace Wallow.Identity.Api.Controllers;

/// <summary>
/// The one anonymous read behind the auth host's branded transaction screens: given the pending
/// authorize request (the relative returnUrl the auth host was handed), who is asking the person
/// to sign in. The returnUrl is the credential — its redirect_uri must exactly match one the
/// client registered, so the endpoint describes a client only to a caller already inside a
/// genuine transaction. Every failure is the same shapeless 404.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/auth/authorize-context")]
[Authorize]
[EnableRateLimiting("auth")]
[Tags("Authorize Context")]
[Produces("application/json")]
public class AuthorizeContextController(IAuthorizeContextService authorizeContextService) : ControllerBase
{
    private const string AuthorizePathSuffix = "/connect/authorize";

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthorizeContextResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthorizeContextResponse>> Get(
        [FromQuery] string? returnUrl,
        [FromQuery] string? scope,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
        {
            return NotFound();
        }

        int queryIndex = returnUrl.IndexOf('?', StringComparison.Ordinal);
        string path = queryIndex < 0 ? returnUrl : returnUrl[..queryIndex];
        if (!path.EndsWith(AuthorizePathSuffix, StringComparison.Ordinal))
        {
            return NotFound();
        }

        Dictionary<string, StringValues> query = queryIndex < 0
            ? []
            : QueryHelpers.ParseQuery(returnUrl[queryIndex..]);

        string? clientId = query.TryGetValue("client_id", out StringValues clientIdValues)
            ? clientIdValues.ToString()
            : null;
        string? redirectUri = query.TryGetValue("redirect_uri", out StringValues redirectUriValues)
            ? redirectUriValues.ToString()
            : null;
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
        {
            return NotFound();
        }

        // The consent redirect narrows the transaction's scopes to the granted set and carries
        // them beside the returnUrl; an explicit scope parameter therefore wins over the one
        // embedded in the authorize request.
        string? effectiveScope = scope;
        if (string.IsNullOrWhiteSpace(effectiveScope) && query.TryGetValue("scope", out StringValues scopeValues))
        {
            effectiveScope = scopeValues.ToString();
        }

        string[] requestedScopes = string.IsNullOrWhiteSpace(effectiveScope)
            ? []
            : effectiveScope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        AuthorizeContextDto? context = await authorizeContextService.ResolveAsync(
            clientId, redirectUri, requestedScopes, ct);
        if (context is null)
        {
            return NotFound();
        }

        AuthorizeContextResponse response = new(
            context.ClientId,
            context.DisplayName,
            context.Tagline,
            context.LogoUrl,
            context.ThemeJson,
            context.OrganizationName,
            context.FirstParty,
            context.Scopes.Select(s => new ScopeInfo(s.Name, s.Description)).ToList());

        return Ok(response);
    }
}
