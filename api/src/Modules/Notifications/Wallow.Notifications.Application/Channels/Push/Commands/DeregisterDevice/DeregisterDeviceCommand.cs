using Wallow.Notifications.Domain.Channels.Push.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Application.Channels.Push.Commands.DeregisterDevice;

public sealed record DeregisterDeviceCommand(DeviceRegistrationId DeviceRegistrationId, UserId UserId);
