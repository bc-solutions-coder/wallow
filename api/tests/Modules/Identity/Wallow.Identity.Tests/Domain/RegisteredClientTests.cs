using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Tests.Domain;

public class RegisteredClientTests
{
    private static readonly Guid _organizationId = Guid.NewGuid();
    private static readonly Guid _actorId = Guid.NewGuid();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Create_StartsActive_AndNotPlatformSuspended()
    {
        RegisteredClient client = CreateClient();

        client.Status.Should().Be(RegisteredClientStatus.Active);
        client.IsPlatformSuspended.Should().BeFalse();
    }

    [Fact]
    public void SuspendByPlatform_RecordsReasonActorAndTime_AndLeavesTheOwnStatusAlone()
    {
        RegisteredClient client = CreateClient();
        _timeProvider.Advance(TimeSpan.FromDays(1));

        client.SuspendByPlatform("Abuse reports", _actorId, _timeProvider);

        client.IsPlatformSuspended.Should().BeTrue();
        client.PlatformSuspensionReason.Should().Be("Abuse reports");
        client.PlatformSuspendedBy.Should().Be(_actorId);
        client.PlatformSuspendedAt.Should().Be(_timeProvider.GetUtcNow());
        // The organization's own suspension is a separate axis; the platform does not rewrite it.
        client.Status.Should().Be(RegisteredClientStatus.Active);
    }

    [Fact]
    public void SuspendByPlatform_WhenAlreadySuspendedByPlatform_Throws()
    {
        RegisteredClient client = CreateClient();
        client.SuspendByPlatform("First reason", _actorId, _timeProvider);

        Action act = () => client.SuspendByPlatform("Second reason", _actorId, _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.ClientAlreadySuspendedByPlatform");
    }

    [Fact]
    public void SuspendByPlatform_WithoutAReason_Throws()
    {
        RegisteredClient client = CreateClient();

        Action act = () => client.SuspendByPlatform(" ", _actorId, _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.PlatformSuspensionReasonRequired");
    }

    [Fact]
    public void ReinstateByPlatform_ClearsTheSuspension()
    {
        RegisteredClient client = CreateClient();
        client.SuspendByPlatform("Abuse reports", _actorId, _timeProvider);

        client.ReinstateByPlatform();

        client.IsPlatformSuspended.Should().BeFalse();
        client.PlatformSuspensionReason.Should().BeNull();
        client.PlatformSuspendedBy.Should().BeNull();
        client.PlatformSuspendedAt.Should().BeNull();
    }

    [Fact]
    public void ReinstateByPlatform_WhenNotSuspendedByPlatform_Throws()
    {
        RegisteredClient client = CreateClient();

        Action act = client.ReinstateByPlatform;

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.ClientNotSuspendedByPlatform");
    }

    [Fact]
    public void Reinstate_OnAPlatformSuspendedButOtherwiseActiveClient_Throws()
    {
        RegisteredClient client = CreateClient();
        client.SuspendByPlatform("Abuse reports", _actorId, _timeProvider);

        // The org surface cannot lift a platform suspension: its own reinstate only answers its
        // own suspension, and this client's own status is still Active.
        Action act = client.Reinstate;

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.ClientNotSuspended");
    }

    [Fact]
    public void Suspend_AndReinstate_StillGovernTheOwnStatus_UnderAPlatformSuspension()
    {
        RegisteredClient client = CreateClient();
        client.SuspendByPlatform("Abuse reports", _actorId, _timeProvider);

        client.Suspend();
        client.Status.Should().Be(RegisteredClientStatus.Suspended);
        client.IsPlatformSuspended.Should().BeTrue();

        client.Reinstate();
        client.Status.Should().Be(RegisteredClientStatus.Active);
        client.IsPlatformSuspended.Should().BeTrue("the organization's reinstate does not lift the platform's suspension");
    }

    private RegisteredClient CreateClient() =>
        RegisteredClient.Create("acme-app", _organizationId, RegisteredClientKind.Application, _actorId, _timeProvider);
}
