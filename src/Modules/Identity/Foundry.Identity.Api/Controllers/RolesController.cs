using System.Net.Http.Json;
using Asp.Versioning;
using Foundry.Identity.Application.Interfaces;
using Foundry.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Foundry.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/roles")]
[Authorize]
[Tags("Roles")]
[Produces("application/json")]
[Consumes("application/json")]
public class RolesController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRolePermissionLookup _rolePermissionLookup;

    public RolesController(
        IHttpClientFactory httpClientFactory,
        IRolePermissionLookup rolePermissionLookup)
    {
        _httpClientFactory = httpClientFactory;
        _rolePermissionLookup = rolePermissionLookup;
    }

    /// <summary>
    /// Get all available roles in the system.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionType.RolesRead)]
    public async Task<ActionResult> GetRoles(CancellationToken ct)
    {
        HttpClient client = _httpClientFactory.CreateClient("KeycloakAdminClient");
        HttpResponseMessage response = await client.GetAsync("/admin/realms/foundry/roles", ct);
        response.EnsureSuccessStatusCode();

        List<RoleInfo>? roles = await response.Content.ReadFromJsonAsync<List<RoleInfo>>(ct);
        var appRoles = roles?.Where(r => !r.Name.StartsWith("uma_", StringComparison.Ordinal) && r.Name != "offline_access" && r.Name != "default-roles-foundry")
            .Select(r => new { r.Name, r.Description })
            .ToList();

        return Ok(appRoles);
    }

    /// <summary>
    /// Get the permissions associated with a specific role.
    /// </summary>
    [HttpGet("{roleName}/permissions")]
    [HasPermission(PermissionType.RolesRead)]
    public ActionResult GetRolePermissions(string roleName)
    {
        IReadOnlyCollection<string> permissions = _rolePermissionLookup.GetPermissions(new[] { roleName });
        return Ok(permissions);
    }

    private record RoleInfo(string Name, string? Description);
}
