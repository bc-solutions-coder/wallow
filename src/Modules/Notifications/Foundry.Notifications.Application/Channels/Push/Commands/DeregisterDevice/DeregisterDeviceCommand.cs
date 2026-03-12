using Foundry.Notifications.Domain.Channels.Push.Identity;

namespace Foundry.Notifications.Application.Channels.Push.Commands.DeregisterDevice;

public sealed record DeregisterDeviceCommand(DeviceRegistrationId DeviceRegistrationId);
