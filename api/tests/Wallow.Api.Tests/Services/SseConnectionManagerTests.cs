using Wallow.Api.Services;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Api.Tests.Services;

public class SseConnectionManagerTests
{
    private readonly SseConnectionManager _sut = new();
    private static readonly Guid _tenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void AddConnection_WithValidData_IsRetrievableByGetConnectionsForTenant()
    {
        _sut.AddConnection("conn-1", "user-1", _tenantId,
            new HashSet<string> { "Notifications" },
            new HashSet<string> { "read" },
            new HashSet<string> { "admin" });

        IEnumerable<string> connections = _sut.GetConnectionsForTenant(_tenantId);

        connections.Should().Contain("conn-1");
    }

    [Fact]
    public void RemoveConnection_AfterAdd_ConnectionNoLongerReturned()
    {
        _sut.AddConnection("conn-1", "user-1", _tenantId,
            new HashSet<string> { "Notifications" },
            new HashSet<string>(),
            new HashSet<string>());

        _sut.RemoveConnection("conn-1");

        IEnumerable<string> connections = _sut.GetConnectionsForTenant(_tenantId);
        connections.Should().NotContain("conn-1");
    }

    [Fact]
    public void ShouldDeliver_NoAudienceRestrictions_ModuleMatches_ReturnsTrue()
    {
        SseConnectionState state = CreateState("user-1", _tenantId,
            modules: ["Notifications"],
            permissions: [],
            roles: []);

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Notifications", "SomeEvent", new { });

        bool result = _sut.ShouldDeliver(state, envelope, "Notifications");

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldDeliver_RequiredPermissionSet_ConnectionLacksPermission_ReturnsFalse()
    {
        SseConnectionState state = CreateState("user-1", _tenantId,
            modules: ["Billing"],
            permissions: ["read"],
            roles: []);

        RealtimeEnvelope envelope = new("InvoiceCreated", "Billing", new { }, DateTime.UtcNow,
            RequiredPermission: "billing:admin");

        bool result = _sut.ShouldDeliver(state, envelope, "Billing");

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldDeliver_RequiredRoleSet_ConnectionLacksRole_ReturnsFalse()
    {
        SseConnectionState state = CreateState("user-1", _tenantId,
            modules: ["Billing"],
            permissions: [],
            roles: ["member"]);

        RealtimeEnvelope envelope = new("InvoiceCreated", "Billing", new { }, DateTime.UtcNow,
            RequiredRole: "admin");

        bool result = _sut.ShouldDeliver(state, envelope, "Billing");

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldDeliver_TargetUserIdSet_DoesNotMatchConnection_ReturnsFalse()
    {
        SseConnectionState state = CreateState("user-1", _tenantId,
            modules: ["Notifications"],
            permissions: [],
            roles: []);

        RealtimeEnvelope envelope = new("Alert", "Notifications", new { }, DateTime.UtcNow,
            TargetUserId: "user-2");

        bool result = _sut.ShouldDeliver(state, envelope, "Notifications");

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldDeliver_ModuleNotInSubscriptions_ReturnsFalse()
    {
        SseConnectionState state = CreateState("user-1", _tenantId,
            modules: ["Notifications"],
            permissions: [],
            roles: []);

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Billing", "InvoiceCreated", new { });

        bool result = _sut.ShouldDeliver(state, envelope, "Billing");

        result.Should().BeFalse();
    }

    [Fact]
    public void GetConnectionForUser_AfterAdd_ReturnsConnectionId()
    {
        _sut.AddConnection("conn-99", "user-99", _tenantId,
            new HashSet<string> { "Notifications" },
            new HashSet<string>(),
            new HashSet<string>());

        IEnumerable<string> connections = _sut.GetConnectionForUser("user-99");

        connections.Should().Contain("conn-99");
    }

    [Fact]
    public void GetConnectionForUser_ForDifferentUser_ReturnsEmpty()
    {
        _sut.AddConnection("conn-a", "user-a", _tenantId,
            new HashSet<string> { "Notifications" },
            new HashSet<string>(),
            new HashSet<string>());

        IEnumerable<string> connections = _sut.GetConnectionForUser("user-b");

        connections.Should().BeEmpty();
    }

    [Fact]
    public void GetReader_ForExistingConnection_ReturnsNonNullReader()
    {
        _sut.AddConnection("conn-r1", "user-r1", _tenantId,
            new HashSet<string> { "Notifications" },
            new HashSet<string>(),
            new HashSet<string>());

        System.Threading.Channels.ChannelReader<RealtimeEnvelope>? reader = _sut.GetReader("conn-r1");

        reader.Should().NotBeNull();
    }

    [Fact]
    public void GetReader_ForNonExistentConnection_ReturnsNull()
    {
        System.Threading.Channels.ChannelReader<RealtimeEnvelope>? reader = _sut.GetReader("does-not-exist");

        reader.Should().BeNull();
    }

    [Fact]
    public void GetConnectionState_ForExistingConnection_ReturnsStateWithCorrectUserId()
    {
        _sut.AddConnection("conn-x", "user-x", _tenantId,
            new HashSet<string> { "Notifications" },
            new HashSet<string> { "read" },
            new HashSet<string> { "admin" });

        SseConnectionState? state = _sut.GetConnectionState("conn-x");

        state.Should().NotBeNull();
        state!.UserId.Should().Be("user-x");
    }

    [Fact]
    public void GetConnectionState_ForNonExistentConnection_ReturnsNull()
    {
        SseConnectionState? state = _sut.GetConnectionState("missing");

        state.Should().BeNull();
    }

    private static SseConnectionState CreateState(
        string userId,
        Guid tenantId,
        string[] modules,
        string[] permissions,
        string[] roles)
    {
        return new SseConnectionState(
            userId,
            tenantId,
            new HashSet<string>(modules),
            new HashSet<string>(permissions),
            new HashSet<string>(roles),
            System.Threading.Channels.Channel.CreateUnbounded<RealtimeEnvelope>());
    }
}
