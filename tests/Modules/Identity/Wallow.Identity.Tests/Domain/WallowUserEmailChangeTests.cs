using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Tests.Domain;

public class WallowUserEmailChangeTests
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void InitiateEmailChange_SetsPendingEmailAndExpiry()
    {
        WallowUser user = CreateUser();
        DateTimeOffset expiry = _timeProvider.GetUtcNow().AddHours(24);

        user.InitiateEmailChange("newemail@example.com", expiry, _timeProvider);

        user.PendingEmail.Should().Be("newemail@example.com");
        user.PendingEmailExpiry.Should().Be(expiry);
    }

    [Fact]
    public void InitiateEmailChange_WithEmptyEmail_Throws()
    {
        WallowUser user = CreateUser();
        DateTimeOffset expiry = _timeProvider.GetUtcNow().AddHours(24);

        Action act = () => user.InitiateEmailChange("", expiry, _timeProvider);

        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void InitiateEmailChange_WithPastExpiry_Throws()
    {
        WallowUser user = CreateUser();
        DateTimeOffset pastExpiry = _timeProvider.GetUtcNow().AddHours(-1);

        Action act = () => user.InitiateEmailChange("newemail@example.com", pastExpiry, _timeProvider);

        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void ConfirmEmailChange_UpdatesEmailAndUserNameAndClearsPendingFields()
    {
        WallowUser user = CreateUser();
        DateTimeOffset expiry = _timeProvider.GetUtcNow().AddHours(24);
        user.InitiateEmailChange("newemail@example.com", expiry, _timeProvider);

        user.ConfirmEmailChange();

        user.Email.Should().Be("newemail@example.com");
        user.UserName.Should().Be("newemail@example.com");
        user.PendingEmail.Should().BeNull();
        user.PendingEmailExpiry.Should().BeNull();
    }

    [Fact]
    public void ConfirmEmailChange_WhenNoPendingEmail_Throws()
    {
        WallowUser user = CreateUser();

        Action act = () => user.ConfirmEmailChange();

        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void ClearPendingEmailChange_IsIdempotent()
    {
        WallowUser user = CreateUser();

        // First call on clean user — no-op, should not throw
        user.ClearPendingEmailChange();
        user.PendingEmail.Should().BeNull();
        user.PendingEmailExpiry.Should().BeNull();

        // Set pending email then clear
        DateTimeOffset expiry = _timeProvider.GetUtcNow().AddHours(24);
        user.InitiateEmailChange("newemail@example.com", expiry, _timeProvider);
        user.PendingEmail.Should().Be("newemail@example.com", "InitiateEmailChange must set PendingEmail before we test clearing it");
        user.ClearPendingEmailChange();

        user.PendingEmail.Should().BeNull();
        user.PendingEmailExpiry.Should().BeNull();

        // Second clear after already cleared — still no throw
        user.ClearPendingEmailChange();
        user.PendingEmail.Should().BeNull();
        user.PendingEmailExpiry.Should().BeNull();
    }

    private WallowUser CreateUser() =>
        WallowUser.Create(Guid.NewGuid(), "John", "Doe", "john@example.com", _timeProvider);
}
