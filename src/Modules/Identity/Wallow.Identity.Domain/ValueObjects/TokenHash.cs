using System.Security.Cryptography;
using System.Text;

namespace Wallow.Identity.Domain.ValueObjects;

public static class TokenHash
{
    public static string Compute(string token)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(token);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
