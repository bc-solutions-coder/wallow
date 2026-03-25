using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Asp.Versioning;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/register")]
[AllowAnonymous]
[Tags("Client Registration")]
[Produces("application/json")]
[Consumes("application/json")]
public class ClientRegistrationController(
    IOpenIddictApplicationManager applicationManager,
    IApiScopeRepository apiScopeRepository,
    IInitialAccessTokenRepository initialAccessTokenRepository,
    IOrganizationService organizationService,
    IHostEnvironment environment) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ClientRegistrationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ClientRegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ClientRegistrationResponse>> Register(
        [FromBody] RegisterClientRequest request,
        CancellationToken ct)
    {
        // In non-development environments, require a valid initial access token
        if (!environment.IsDevelopment())
        {
            string? authHeader = Request.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized();
            }

            string rawToken = authHeader["Bearer ".Length..];
            byte[] hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
            string tokenHash = Convert.ToBase64String(hashBytes);

            Domain.Entities.InitialAccessToken? accessToken = await initialAccessTokenRepository.GetByHashAsync(tokenHash, ct);
            if (accessToken is null || !accessToken.IsValid(DateTimeOffset.UtcNow))
            {
                return Unauthorized();
            }
        }

        // When a tenant is specified, verify the caller is a member of that organization
        if (request.TenantId.HasValue)
        {
            string? callerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (callerIdClaim is null || !Guid.TryParse(callerIdClaim, out Guid callerId))
            {
                return Forbid();
            }

            IReadOnlyList<Application.DTOs.UserDto> members =
                await organizationService.GetMembersAsync(request.TenantId.Value, ct);

            if (!members.Any(m => m.Id == callerId))
            {
                return Forbid();
            }
        }

        // Validate grant type prefix rules
        bool hasClientCredentials = request.GrantTypes.Contains(GrantTypes.ClientCredentials);
        bool hasAuthorizationCode = request.GrantTypes.Contains(GrantTypes.AuthorizationCode);

        if (hasClientCredentials && hasAuthorizationCode)
        {
            return BadRequest("Cannot mix client_credentials and authorization_code grant types in a single registration.");
        }

        if (hasClientCredentials && !request.ClientId.StartsWith("sa-", StringComparison.Ordinal))
        {
            return BadRequest("Client ID must start with 'sa-' for client_credentials grant type.");
        }

        if (hasAuthorizationCode && !request.ClientId.StartsWith("app-", StringComparison.Ordinal))
        {
            return BadRequest("Client ID must start with 'app-' for authorization_code grant type.");
        }

        // Validate all requested scopes exist
        if (request.Scopes.Count > 0)
        {
            IReadOnlyList<Domain.Entities.ApiScope> existingScopes = await apiScopeRepository.GetByCodesAsync(request.Scopes, ct);
            HashSet<string> existingScopeNames = existingScopes.Select(s => s.Code).ToHashSet(StringComparer.Ordinal);
            List<string> unknownScopes = request.Scopes.Where(s => !existingScopeNames.Contains(s)).ToList();

            if (unknownScopes.Count > 0)
            {
                return BadRequest($"Unknown scopes: {string.Join(", ", unknownScopes)}");
            }
        }

        // Check if client already exists — rotate secret if so
        object? existingApp = await applicationManager.FindByClientIdAsync(request.ClientId, ct);
        if (existingApp is not null)
        {
            string rotatedSecret = GenerateClientSecret();

            OpenIddictApplicationDescriptor descriptor = new();
            await applicationManager.PopulateAsync(descriptor, existingApp, ct);
            descriptor.ClientSecret = rotatedSecret;

            if (request.TenantId.HasValue)
            {
                SetTenantId(descriptor, request.TenantId.Value);
            }

            await applicationManager.UpdateAsync(existingApp, descriptor, ct);

            return Ok(new ClientRegistrationResponse(request.ClientId, rotatedSecret));
        }

        // Build new application descriptor
        string clientSecret = GenerateClientSecret();

        List<string> permissions = [Permissions.Endpoints.Token];

        if (hasClientCredentials)
        {
            permissions.Add(Permissions.GrantTypes.ClientCredentials);
        }

        if (hasAuthorizationCode)
        {
            permissions.Add(Permissions.Endpoints.Authorization);
            permissions.Add(Permissions.GrantTypes.AuthorizationCode);
            permissions.Add(Permissions.GrantTypes.RefreshToken);
            permissions.Add(Permissions.ResponseTypes.Code);
        }

        foreach (string scope in request.Scopes)
        {
            permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        OpenIddictApplicationDescriptor newDescriptor = new()
        {
            ClientId = request.ClientId,
            ClientSecret = clientSecret,
            DisplayName = request.ClientName,
            ClientType = ClientTypes.Confidential,
        };

        foreach (string permission in permissions)
        {
            newDescriptor.Permissions.Add(permission);
        }

        if (request.RedirectUris is { Count: > 0 })
        {
            foreach (string uri in request.RedirectUris)
            {
                newDescriptor.RedirectUris.Add(new Uri(uri));
            }
        }

        if (request.TenantId.HasValue)
        {
            SetTenantId(newDescriptor, request.TenantId.Value);
        }

        await applicationManager.CreateAsync(newDescriptor, ct);

        return StatusCode(StatusCodes.Status201Created, new ClientRegistrationResponse(request.ClientId, clientSecret));
    }

    private static void SetTenantId(OpenIddictApplicationDescriptor descriptor, Guid tenantId)
    {
        descriptor.Properties["tenant_id"] = JsonSerializer.SerializeToElement(tenantId.ToString());
    }

    private static string GenerateClientSecret()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }
}
