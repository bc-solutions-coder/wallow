using System.Security.Claims;
using Wallow.Shared.Kernel.Extensions;

namespace Wallow.Identity.Application.Helpers;

/// <summary>
/// Reads the global-admin flag from current user claims at authorization and refresh,
/// so refreshing does not preserve a grant removed from the claim store.
/// </summary>
public static class GlobalAdminClaims
{
    public static bool IsGranted(IEnumerable<Claim> userClaims)
    {
        ArgumentNullException.ThrowIfNull(userClaims);

        foreach (Claim claim in userClaims)
        {
            if (string.Equals(claim.Type, ClaimsPrincipalExtensions.GlobalAdminClaimType, StringComparison.Ordinal)
                && bool.TryParse(claim.Value, out bool isGlobalAdmin)
                && isGlobalAdmin)
            {
                return true;
            }
        }

        return false;
    }
}
