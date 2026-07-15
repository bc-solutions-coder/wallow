using Microsoft.AspNetCore.Authorization;

namespace Wallow.Identity.Infrastructure.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
