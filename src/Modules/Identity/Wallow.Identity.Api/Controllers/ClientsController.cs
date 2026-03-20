using System.Security.Cryptography;
using Asp.Versioning;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/clients")]
[Authorize]
[HasPermission(PermissionType.AdminAccess)]
[Tags("Clients")]
[Produces("application/json")]
[Consumes("application/json")]
public class ClientsController(IOpenIddictApplicationManager applicationManager) : ControllerBase
{
    [HttpGet]
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
                PostLogoutRedirectUris = descriptor.PostLogoutRedirectUris.Select(u => u.ToString()).ToList()
            });
        }

        return Ok(clients);
    }

    [HttpGet("{id}")]
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
            PostLogoutRedirectUris = descriptor.PostLogoutRedirectUris.Select(u => u.ToString()).ToList()
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClientResponse>> Create(
        [FromBody] CreateClientRequest request,
        CancellationToken ct)
    {
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
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code
            }
        };

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
            PostLogoutRedirectUris = request.PostLogoutRedirectUris
        };

        return CreatedAtAction(nameof(GetById), new { id }, response);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientResponse>> Update(
        string id,
        [FromBody] UpdateClientRequest request,
        CancellationToken ct)
    {
        object? application = await applicationManager.FindByIdAsync(id, ct);
        if (application is null)
        {
            return NotFound();
        }

        OpenIddictApplicationDescriptor descriptor = new();
        await applicationManager.PopulateAsync(descriptor, application, ct);

        descriptor.DisplayName = request.Name;

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
            PostLogoutRedirectUris = request.PostLogoutRedirectUris
        });
    }

    [HttpDelete("{id}")]
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
            PostLogoutRedirectUris = descriptor.PostLogoutRedirectUris.Select(u => u.ToString()).ToList()
        });
    }

    private static string GenerateClientSecret()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }
}
