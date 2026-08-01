using System.Security.Claims;
using Wallow.Shared.Kernel.Extensions;

namespace Wallow.Identity.Application.Helpers;

/// <summary>
/// Reads the global-admin flag off the claims a user owns, rather than off a principal.
/// </summary>
/// <remarks>
/// The distinction matters at both ends of a token's life. At authorize time there is no
/// principal to read yet; at refresh time there is one, but trusting it would let a token keep a
/// revoked global admin alive. Both ends go to the user's own claim store, so what counts as the
/// flag is stated once here.
/// </remarks>
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
