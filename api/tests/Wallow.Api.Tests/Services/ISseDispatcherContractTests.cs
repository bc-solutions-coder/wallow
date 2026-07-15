using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Api.Tests.Services;

public class ISseDispatcherContractTests
{
    private readonly ISseDispatcher _dispatcher = Substitute.For<ISseDispatcher>();
    private static readonly Guid _tenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public async Task SendToTenantAsync_WithValidArgs_ReturnsCompletedTask()
    {
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Module", "Event", new { Id = 1 });

        Task result = _dispatcher.SendToTenantAsync(_tenantId, envelope, CancellationToken.None);

        await result;
        result.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task SendToTenantPermissionAsync_WithValidArgs_ReturnsCompletedTask()
    {
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Module", "Event", new { Id = 1 });

        Task result = _dispatcher.SendToTenantPermissionAsync(_tenantId, "billing:read", envelope, CancellationToken.None);

        await result;
        result.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task SendToTenantRoleAsync_WithValidArgs_ReturnsCompletedTask()
    {
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Module", "Event", new { Id = 1 });

        Task result = _dispatcher.SendToTenantRoleAsync(_tenantId, "Admin", envelope, CancellationToken.None);

        await result;
        result.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task SendToUserAsync_WithValidArgs_ReturnsCompletedTask()
    {
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Module", "Event", new { Id = 1 });

        Task result = _dispatcher.SendToUserAsync("user-42", envelope, CancellationToken.None);

        await result;
        result.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task SendToTenantPermissionAsync_CapturesPermissionArgument()
    {
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Module", "Event", new { Id = 1 });
        string expectedPermission = "inquiries:manage";

        await _dispatcher.SendToTenantPermissionAsync(_tenantId, expectedPermission, envelope);

        await _dispatcher.Received(1).SendToTenantPermissionAsync(
            _tenantId,
            Arg.Is<string>(p => p == "inquiries:manage"),
            Arg.Any<RealtimeEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToTenantAsync_ReturnType_IsTask()
    {
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Module", "Event", new { Id = 1 });

        object result = _dispatcher.SendToTenantAsync(_tenantId, envelope);

        result.Should().BeAssignableTo<Task>();
        await (Task)result;
    }

    [Fact]
    public async Task SendToTenantPermissionAsync_ReturnType_IsTask()
    {
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Module", "Event", new { Id = 1 });

        object result = _dispatcher.SendToTenantPermissionAsync(_tenantId, "perm", envelope);

        result.Should().BeAssignableTo<Task>();
        await (Task)result;
    }

    [Fact]
    public async Task SendToTenantRoleAsync_ReturnType_IsTask()
    {
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Module", "Event", new { Id = 1 });

        object result = _dispatcher.SendToTenantRoleAsync(_tenantId, "role", envelope);

        result.Should().BeAssignableTo<Task>();
        await (Task)result;
    }

    [Fact]
    public async Task SendToUserAsync_ReturnType_IsTask()
    {
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Module", "Event", new { Id = 1 });

        object result = _dispatcher.SendToUserAsync("user-1", envelope);

        result.Should().BeAssignableTo<Task>();
        await (Task)result;
    }
}
