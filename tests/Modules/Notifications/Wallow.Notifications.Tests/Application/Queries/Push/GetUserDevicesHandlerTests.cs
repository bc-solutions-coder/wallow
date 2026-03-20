using Wallow.Notifications.Application.Channels.Push.DTOs;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Application.Channels.Push.Queries.GetUserDevices;
using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Tests.Application.Queries.Push;

public class GetUserDevicesHandlerTests
{
    private readonly IDeviceRegistrationRepository _deviceRegistrationRepository = Substitute.For<IDeviceRegistrationRepository>();
    private readonly GetUserDevicesHandler _handler;

    public GetUserDevicesHandlerTests()
    {
        _handler = new GetUserDevicesHandler(_deviceRegistrationRepository);
    }

    [Fact]
    public async Task Handle_ReturnsActiveDevicesForUser()
    {
        Guid userId = Guid.NewGuid();
        UserId userIdTyped = new(userId);
        DeviceRegistration device = DeviceRegistration.Register(
            userIdTyped, TenantId.New(), PushPlatform.Fcm, "token-abc", DateTimeOffset.UtcNow);

        _deviceRegistrationRepository
            .GetActiveByUserAsync(userIdTyped, Arg.Any<CancellationToken>())
            .Returns(new List<DeviceRegistration> { device });

        Result<IReadOnlyList<DeviceRegistrationDto>> result = await _handler.Handle(
            new GetUserDevicesQuery(userId, Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Token.Should().Be("token-abc");
        result.Value[0].Platform.Should().Be(PushPlatform.Fcm);
        result.Value[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenNoDevices_ReturnsEmptyList()
    {
        Guid userId = Guid.NewGuid();
        _deviceRegistrationRepository
            .GetActiveByUserAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<DeviceRegistration>());

        Result<IReadOnlyList<DeviceRegistrationDto>> result = await _handler.Handle(
            new GetUserDevicesQuery(userId, Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
