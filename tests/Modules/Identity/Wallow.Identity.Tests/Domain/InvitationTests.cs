using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Tests.Domain;

public class InvitationTests
{
    private static readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());
    private static readonly Guid _userId = Guid.NewGuid();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Create_WithValidEmail_ReturnsPendingInvitation()
    {
        DateTimeOffset expiresAt = _timeProvider.GetUtcNow().AddDays(7);

        Invitation invitation = Invitation.Create(_tenantId, "user@example.com", expiresAt, _userId, _timeProvider);

        invitation.TenantId.Should().Be(_tenantId);
        invitation.Email.Should().Be("user@example.com");
        invitation.Status.Should().Be(InvitationStatus.Pending);
        invitation.ExpiresAt.Should().Be(expiresAt);
        invitation.Token.Should().NotBeNullOrWhiteSpace();
        invitation.AcceptedByUserId.Should().BeNull();
    }

    [Fact]
    public void Create_GeneratesUniqueTokens()
    {
        DateTimeOffset expiresAt = _timeProvider.GetUtcNow().AddDays(7);

        Invitation invitation1 = Invitation.Create(_tenantId, "a@example.com", expiresAt, _userId, _timeProvider);
        Invitation invitation2 = Invitation.Create(_tenantId, "b@example.com", expiresAt, _userId, _timeProvider);

        invitation1.Token.Should().NotBe(invitation2.Token);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithBlankEmail_ThrowsBusinessRuleException(string? email)
    {
        DateTimeOffset expiresAt = _timeProvider.GetUtcNow().AddDays(7);

        Action act = () => Invitation.Create(_tenantId, email!, expiresAt, _userId, _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*email*");
    }

    [Fact]
    public void Accept_WhenPending_SetsStatusToAccepted()
    {
        Invitation invitation = CreatePendingInvitation();
        Guid acceptingUserId = Guid.NewGuid();

        invitation.Accept(acceptingUserId, _timeProvider);

        invitation.Status.Should().Be(InvitationStatus.Accepted);
        invitation.AcceptedByUserId.Should().Be(acceptingUserId);
    }

    [Fact]
    public void Accept_WhenAlreadyAccepted_ThrowsBusinessRuleException()
    {
        Invitation invitation = CreatePendingInvitation();
        invitation.Accept(Guid.NewGuid(), _timeProvider);

        Action act = () => invitation.Accept(Guid.NewGuid(), _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*Cannot accept*");
    }

    [Fact]
    public void Accept_WhenRevoked_ThrowsBusinessRuleException()
    {
        Invitation invitation = CreatePendingInvitation();
        invitation.Revoke(_userId, _timeProvider);

        Action act = () => invitation.Accept(Guid.NewGuid(), _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*Cannot accept*");
    }

    [Fact]
    public void Revoke_WhenPending_SetsStatusToRevoked()
    {
        Invitation invitation = CreatePendingInvitation();

        invitation.Revoke(_userId, _timeProvider);

        invitation.Status.Should().Be(InvitationStatus.Revoked);
    }

    [Fact]
    public void Revoke_WhenAlreadyAccepted_ThrowsBusinessRuleException()
    {
        Invitation invitation = CreatePendingInvitation();
        invitation.Accept(Guid.NewGuid(), _timeProvider);

        Action act = () => invitation.Revoke(_userId, _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*Cannot revoke*");
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_ThrowsBusinessRuleException()
    {
        Invitation invitation = CreatePendingInvitation();
        invitation.Revoke(_userId, _timeProvider);

        Action act = () => invitation.Revoke(_userId, _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*Cannot revoke*");
    }

    [Fact]
    public void MarkExpired_WhenPending_SetsStatusToExpired()
    {
        Invitation invitation = CreatePendingInvitation();

        invitation.MarkExpired();

        invitation.Status.Should().Be(InvitationStatus.Expired);
    }

    [Fact]
    public void MarkExpired_WhenAlreadyAccepted_ThrowsBusinessRuleException()
    {
        Invitation invitation = CreatePendingInvitation();
        invitation.Accept(Guid.NewGuid(), _timeProvider);

        Action act = () => invitation.MarkExpired();

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*Cannot expire*");
    }

    [Fact]
    public void MarkExpired_WhenAlreadyExpired_ThrowsBusinessRuleException()
    {
        Invitation invitation = CreatePendingInvitation();
        invitation.MarkExpired();

        Action act = () => invitation.MarkExpired();

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*Cannot expire*");
    }

    private Invitation CreatePendingInvitation() =>
        Invitation.Create(_tenantId, "user@example.com", _timeProvider.GetUtcNow().AddDays(7), _userId, _timeProvider);
}
