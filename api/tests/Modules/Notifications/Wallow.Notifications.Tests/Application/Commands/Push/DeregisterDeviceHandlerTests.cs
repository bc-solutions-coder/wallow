using Wallow.Notifications.Application.Channels.Push.Commands.DeregisterDevice;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Notifications.Domain.Channels.Push.Identity;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Tests.Application.Commands.Push;

public class DeregisterDeviceHandlerTests
{
    private readonly IDeviceRegistrationRepository _deviceRegistrationRepository = Substitute.For<IDeviceRegistrationRepository>();
    private readonly DeregisterDeviceHandler _handler;

    public DeregisterDeviceHandlerTests()
    {
        _deviceRegistrationRepository.SaveDeactivationAsync(Arg.Any<DeviceRegistration>(), Arg.Any<CancellationToken>()).Returns(true);
        _handler = new DeregisterDeviceHandler(_deviceRegistrationRepository);
    }

    [Fact]
    public async Task Handle_WhenDeviceFound_DeactivatesAndSaves()
    {
        DeviceRegistration registration = DeviceRegistration.Register(
            new UserId(Guid.NewGuid()),
            TenantId.New(),
            PushPlatform.Fcm,
            "token",
            DateTimeOffset.UtcNow);

        _deviceRegistrationRepository
            .GetByIdAsync(registration.Id, Arg.Any<CancellationToken>())
            .Returns(registration);

        DeregisterDeviceCommand command = new(registration.Id, registration.UserId);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        registration.IsActive.Should().BeFalse();
        await _deviceRegistrationRepository.Received(1).SaveDeactivationAsync(registration, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDeviceNotFound_ReturnsNotFoundFailure()
    {
        DeviceRegistrationId id = DeviceRegistrationId.New();
        _deviceRegistrationRepository
            .GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((DeviceRegistration?)null);

        DeregisterDeviceCommand command = new(id, UserId.New());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
