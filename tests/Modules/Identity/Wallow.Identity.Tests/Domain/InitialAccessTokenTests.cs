using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Tests.Domain;

public class InitialAccessTokenTests
{
    [Fact]
    public void Create_WithValidParameters_CreatesToken()
    {
        InitialAccessToken token = InitialAccessToken.Create("hash123", "My Token", DateTimeOffset.UtcNow.AddDays(30));

        token.TokenHash.Should().Be("hash123");
        token.DisplayName.Should().Be("My Token");
        token.ExpiresAt.Should().NotBeNull();
        token.IsRevoked.Should().BeFalse();
        token.Id.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_WithEmptyTokenHash_ThrowsBusinessRuleException()
    {
        Func<InitialAccessToken> act = () => InitialAccessToken.Create("", "My Token", DateTimeOffset.UtcNow.AddDays(30));

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*hash*");
    }

    [Fact]
    public void Create_WithEmptyDisplayName_ThrowsBusinessRuleException()
    {
        Func<InitialAccessToken> act = () => InitialAccessToken.Create("hash123", "", DateTimeOffset.UtcNow.AddDays(30));

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*display name*");
    }

    [Fact]
    public void Create_WithNullExpiry_SetsExpiresAtNull()
    {
        InitialAccessToken token = InitialAccessToken.Create("hash123", "My Token", null);

        token.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void IsValid_WhenNotRevokedAndNotExpired_ReturnsTrue()
    {
        InitialAccessToken token = InitialAccessToken.Create("hash123", "My Token", DateTimeOffset.UtcNow.AddDays(30));

        bool result = token.IsValid(DateTimeOffset.UtcNow);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenRevoked_ReturnsFalse()
    {
        InitialAccessToken token = InitialAccessToken.Create("hash123", "My Token", DateTimeOffset.UtcNow.AddDays(30));
        token.Revoke();

        bool result = token.IsValid(DateTimeOffset.UtcNow);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenExpired_ReturnsFalse()
    {
        InitialAccessToken token = InitialAccessToken.Create("hash123", "My Token", DateTimeOffset.UtcNow.AddDays(-1));

        bool result = token.IsValid(DateTimeOffset.UtcNow);

        result.Should().BeFalse();
    }

    [Fact]
    public void Revoke_SetsIsRevokedTrue()
    {
        InitialAccessToken token = InitialAccessToken.Create("hash123", "My Token", DateTimeOffset.UtcNow.AddDays(30));

        token.Revoke();

        token.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_ThrowsBusinessRuleException()
    {
        InitialAccessToken token = InitialAccessToken.Create("hash123", "My Token", DateTimeOffset.UtcNow.AddDays(30));
        token.Revoke();

        Action act = () => token.Revoke();

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*already been revoked*");
    }
}
