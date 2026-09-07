using Wallow.Notifications.Domain.Channels.Push.Identity;

namespace Wallow.Notifications.Application.Channels.Push.Commands.DeliverPush;

public sealed record DeliverPushCommand(
    PushMessageId PushMessageId,
    DeviceRegistrationId DeviceRegistrationId,
    Guid TenantId,
    int Attempt = 0);
