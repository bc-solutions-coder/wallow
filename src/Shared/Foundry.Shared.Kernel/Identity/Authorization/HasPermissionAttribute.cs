using Microsoft.AspNetCore.Authorization;

namespace Foundry.Shared.Kernel.Identity.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
        : base(permission)
    {
        Permission = permission;
    }

    public string Permission { get; }
}
