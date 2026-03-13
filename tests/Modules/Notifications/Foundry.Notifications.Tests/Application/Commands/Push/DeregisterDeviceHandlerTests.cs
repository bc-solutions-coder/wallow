using Foundry.Notifications.Application.Channels.Push.Commands.DeregisterDevice;
using Foundry.Notifications.Application.Channels.Push.Interfaces;
using Foundry.Notifications.Domain.Channels.Push;
using Foundry.Notifications.Domain.Channels.Push.Enums;
using Foundry.Notifications.Domain.Channels.Push.Identity;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Tests.Application.Commands.Push;

public class DeregisterDeviceHandlerTests
{
    private readonly IDeviceRegistrationRepository _deviceRegistrationRepository = Substitute.For<IDeviceRegistrationRepository>();
    private readonly DeregisterDeviceHandler _handler;

    public DeregisterDeviceHandlerTests()
    {
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

        DeregisterDeviceCommand command = new(registration.Id);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        registration.IsActive.Should().BeFalse();
        _deviceRegistrationRepository.Received(1).Update(registration);
        await _deviceRegistrationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDeviceNotFound_ReturnsNotFoundFailure()
    {
        DeviceRegistrationId id = DeviceRegistrationId.New();
        _deviceRegistrationRepository
            .GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((DeviceRegistration?)null);

        DeregisterDeviceCommand command = new(id);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
