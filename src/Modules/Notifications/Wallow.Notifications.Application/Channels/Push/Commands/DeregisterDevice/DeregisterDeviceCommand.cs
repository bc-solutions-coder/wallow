using Wallow.Notifications.Domain.Channels.Push.Identity;

namespace Wallow.Notifications.Application.Channels.Push.Commands.DeregisterDevice;

public sealed record DeregisterDeviceCommand(DeviceRegistrationId DeviceRegistrationId);
