using System.Security.Claims;

namespace Wallow.Identity.Application.Helpers;

public static class ExternalLoginClaimsHelper
{
    public static (string FirstName, string LastName) ExtractName(IEnumerable<Claim> claims, string email)
    {
        List<Claim> claimList = claims.ToList();

        string? givenName = claimList.FirstOrDefault(c => c.Type == ClaimTypes.GivenName)?.Value;
        string? surname = claimList.FirstOrDefault(c => c.Type == ClaimTypes.Surname)?.Value;

        if (!string.IsNullOrWhiteSpace(givenName) && !string.IsNullOrWhiteSpace(surname))
        {
            return (givenName, surname);
        }

        string? name = claimList.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
        if (!string.IsNullOrWhiteSpace(name))
        {
            int spaceIndex = name.IndexOf(' ', StringComparison.Ordinal);
            if (spaceIndex > 0)
            {
                return (name[..spaceIndex], name[(spaceIndex + 1)..]);
            }

            return (name, ExtractEmailLocalPart(email));
        }

        return ("User", ExtractEmailLocalPart(email));
    }

    public static string? ExtractEmail(IEnumerable<Claim> claims)
    {
        return claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
    }

    public static bool IsEmailVerified(IEnumerable<Claim> claims)
    {
        string? verified = claims.FirstOrDefault(c => c.Type == "email_verified")?.Value;
        return string.Equals(verified, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractEmailLocalPart(string email)
    {
        int atIndex = email.IndexOf('@', StringComparison.Ordinal);
        return atIndex > 0 ? email[..atIndex] : email;
    }
}
