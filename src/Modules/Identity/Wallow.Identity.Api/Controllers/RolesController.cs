using System.Net.Http.Json;
using Asp.Versioning;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/roles")]
[Authorize]
[Tags("Roles")]
[Produces("application/json")]
[Consumes("application/json")]
public class RolesController(IHttpClientFactory httpClientFactory, IRolePermissionLookup rolePermissionLookup) : ControllerBase
{

    /// <summary>
    /// Get all available roles in the system.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionType.RolesRead)]
    public async Task<ActionResult> GetRoles(CancellationToken ct)
    {
        HttpClient client = httpClientFactory.CreateClient("KeycloakAdminClient");
        HttpResponseMessage response = await client.GetAsync("/admin/realms/wallow/roles", ct);
        response.EnsureSuccessStatusCode();

        List<RoleInfo>? roles = await response.Content.ReadFromJsonAsync<List<RoleInfo>>(ct);

        List<object>? appRoles = roles?
            .Where(r => !IsKeycloakSystemRole(r.Name))
            .Select(r => new { r.Name, r.Description } as object)
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
        IReadOnlyCollection<string> permissions = rolePermissionLookup.GetPermissions(new[] { roleName });
        return Ok(permissions);
    }

    private static bool IsKeycloakSystemRole(string roleName)
    {
        HashSet<string> systemRoles =
        [
            "offline_access",
            "uma_authorization",
            "create-realm",
            "admin"
        ];

        return systemRoles.Contains(roleName)
               || roleName.StartsWith("uma_", StringComparison.Ordinal)
               || roleName.StartsWith("default-roles-", StringComparison.Ordinal);
    }

    private record RoleInfo(string Name, string? Description);
}
