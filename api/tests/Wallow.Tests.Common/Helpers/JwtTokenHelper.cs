namespace Wallow.Tests.Common.Helpers;

public static class JwtTokenHelper
{
    /// <summary>
    /// Token prefix used to encode user information for TestAuthHandler.
    /// Format: test-token:userId:role1,role2
    /// </summary>
    public const string TokenPrefix = "test-token:";

    public static string GenerateToken(
        string userId,
        string[]? roles = null)
    {
        // With TestAuthHandler, tokens are no longer validated.
        // Encode the user ID and roles in the token so TestAuthHandler can extract them.
        // This is needed for SignalR tests where we can't use X-Test-User-Id headers.
        // Format: test-token:userId:role1,role2
        string rolesString = roles != null ? string.Join(",", roles) : "admin";
        return $"{TokenPrefix}{userId}:{rolesString}";
    }

    /// <summary>
    /// Parses a test token to extract user ID and roles.
    /// Returns null if the token is not a valid test token.
    /// </summary>
    public static (string UserId, string[] Roles)? ParseToken(string? token)
    {
        if (string.IsNullOrEmpty(token) || !token.StartsWith(TokenPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        string[] parts = token[TokenPrefix.Length..].Split(':');
        if (parts.Length < 1)
        {
            return null;
        }

        string userId = parts[0];
        string[] roles = parts.Length > 1 && !string.IsNullOrEmpty(parts[1])
            ? parts[1].Split(',')
            : new[] { "admin" };

        return (userId, roles);
    }
}
