using Microsoft.AspNetCore.Authorization;

namespace Foundry.Shared.Kernel.Identity.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(PermissionType permission)
        : base(permission.ToString())
    {
        Permission = permission;
    }

    public PermissionType Permission { get; }
}
