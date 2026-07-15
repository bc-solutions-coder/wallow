using System.Security.Cryptography;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/initial-access-tokens")]
[Authorize]
[HasPermission(PermissionType.AdminAccess)]
[Tags("InitialAccessTokens")]
[Produces("application/json")]
[Consumes("application/json")]
public class InitialAccessTokensController(IInitialAccessTokenRepository repository) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(InitialAccessTokenCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InitialAccessTokenCreatedResponse>> Create(
        [FromBody] CreateInitialAccessTokenRequest request,
        CancellationToken ct)
    {
        byte[] rawBytes = RandomNumberGenerator.GetBytes(32);
        string rawToken = Convert.ToBase64String(rawBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        byte[] hashBytes = SHA256.HashData(rawBytes);
        string tokenHash = Convert.ToHexString(hashBytes);

        InitialAccessToken token = InitialAccessToken.Create(tokenHash, request.DisplayName, request.ExpiresAt);
        await repository.AddAsync(token, ct);

        InitialAccessTokenCreatedResponse response = new(
            token.Id.Value.ToString(),
            rawToken,
            token.DisplayName,
            token.ExpiresAt);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<InitialAccessTokenResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InitialAccessTokenResponse>>> GetAll(CancellationToken ct)
    {
        List<InitialAccessToken> tokens = await repository.ListAsync(ct);

        List<InitialAccessTokenResponse> response = tokens
            .Select(t => new InitialAccessTokenResponse(
                t.Id.Value.ToString(),
                t.DisplayName,
                t.ExpiresAt,
                t.IsRevoked))
            .ToList();

        return Ok(response);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out Guid guidId))
        {
            return NotFound();
        }

        InitialAccessTokenId tokenId = InitialAccessTokenId.Create(guidId);
        InitialAccessToken? token = await repository.GetByIdAsync(tokenId, ct);

        if (token is null)
        {
            return NotFound();
        }

        token.Revoke();
        await repository.SaveChangesAsync(ct);

        return NoContent();
    }
}
