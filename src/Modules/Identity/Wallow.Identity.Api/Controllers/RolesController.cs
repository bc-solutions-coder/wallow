using Asp.Versioning;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/roles")]
[Authorize]
[Tags("Roles")]
[Produces("application/json")]
[Consumes("application/json")]
public class RolesController(RoleManager<WallowRole> roleManager, IRolePermissionLookup rolePermissionLookup) : ControllerBase
{

    /// <summary>
    /// Get all available roles in the system.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionType.RolesRead)]
    public async Task<ActionResult> GetRoles(CancellationToken ct)
    {
        List<WallowRole> roles = await roleManager.Roles.ToListAsync(ct);
        List<object> result = roles.Select(r => new { r.Name } as object).ToList();
        return Ok(result);
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
}
