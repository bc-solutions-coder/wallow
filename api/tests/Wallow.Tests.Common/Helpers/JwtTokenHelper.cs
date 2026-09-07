namespace Wallow.Tests.Common.Helpers;

public static class JwtTokenHelper
{
    /// <summary>
    /// Prefix for test credentials formatted as <c>test-token:userId:role1,role2</c>.
    /// </summary>
    public const string TokenPrefix = "test-token:";

    public static string GenerateToken(
        string userId,
        string[]? roles = null)
    {
        // SignalR tests carry the synthetic identity in the access token.
        string rolesString = roles != null ? string.Join(",", roles) : "admin";
        return $"{TokenPrefix}{userId}:{rolesString}";
    }

    /// <summary>
    /// Reads the user ID and comma-separated roles from a test-prefixed credential.
    /// Missing or empty roles default to admin; a missing prefix returns null.
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
