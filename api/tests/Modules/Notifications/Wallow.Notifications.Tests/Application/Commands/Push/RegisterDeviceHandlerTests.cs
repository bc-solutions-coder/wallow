using Wallow.Notifications.Application.Channels.Push.Commands.RegisterDevice;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Tests.Application.Commands.Push;

public class RegisterDeviceHandlerTests
{
    private readonly IDeviceRegistrationRepository _deviceRegistrationRepository = Substitute.For<IDeviceRegistrationRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly RegisterDeviceHandler _handler;

    public RegisterDeviceHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        _handler = new RegisterDeviceHandler(_deviceRegistrationRepository, _timeProvider);
    }

    [Fact]
    public async Task Handle_WithValidCommand_RegistersDeviceAndSaves()
    {
        RegisterDeviceCommand command = new(
            new UserId(Guid.NewGuid()),
            TenantId.New(),
            PushPlatform.Fcm,
            "device-token-abc123");

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _deviceRegistrationRepository.Received(1).Add(Arg.Any<DeviceRegistration>());
        await _deviceRegistrationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithApnsPlatform_RegistersDeviceWithCorrectPlatform()
    {
        UserId userId = new(Guid.NewGuid());
        RegisterDeviceCommand command = new(userId, TenantId.New(), PushPlatform.Apns, "ios-token");

        await _handler.Handle(command, CancellationToken.None);

        _deviceRegistrationRepository.Received(1).Add(
            Arg.Is<DeviceRegistration>(d => d.Platform == PushPlatform.Apns && d.Token == "ios-token"));
    }
}
