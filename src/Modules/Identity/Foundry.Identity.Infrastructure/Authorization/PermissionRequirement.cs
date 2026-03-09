using Microsoft.AspNetCore.Authorization;

namespace Foundry.Identity.Infrastructure.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
