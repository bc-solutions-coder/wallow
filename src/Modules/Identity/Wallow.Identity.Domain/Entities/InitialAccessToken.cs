using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Domain.Entities;

public sealed class InitialAccessToken : Entity<InitialAccessTokenId>
{
    public string TokenHash { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public DateTimeOffset? ExpiresAt { get; private set; }

    public bool IsRevoked { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private InitialAccessToken() { } // EF Core

    private InitialAccessToken(string tokenHash, string displayName, DateTimeOffset? expiresAt)
    {
        Id = InitialAccessTokenId.New();
        TokenHash = tokenHash;
        DisplayName = displayName;
        ExpiresAt = expiresAt;
        IsRevoked = false;
    }

    public static InitialAccessToken Create(string tokenHash, string displayName, DateTimeOffset? expiresAt)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new BusinessRuleException(
                "Identity.TokenHashRequired",
                "Token hash cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new BusinessRuleException(
                "Identity.TokenDisplayNameRequired",
                "Token display name cannot be empty");
        }

        return new InitialAccessToken(tokenHash, displayName, expiresAt);
    }

    public void Revoke()
    {
        if (IsRevoked)
        {
            throw new BusinessRuleException(
                "Identity.TokenAlreadyRevoked",
                "This token has already been revoked");
        }

        IsRevoked = true;
    }

    public bool IsValid(DateTimeOffset now)
    {
        return !IsRevoked && (ExpiresAt is null || ExpiresAt > now);
    }
}
