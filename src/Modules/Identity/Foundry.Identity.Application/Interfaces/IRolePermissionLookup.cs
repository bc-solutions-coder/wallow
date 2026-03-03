namespace Foundry.Identity.Application.Interfaces;

public interface IRolePermissionLookup
{
    IReadOnlyCollection<string> GetPermissions(IEnumerable<string> roles);
}
