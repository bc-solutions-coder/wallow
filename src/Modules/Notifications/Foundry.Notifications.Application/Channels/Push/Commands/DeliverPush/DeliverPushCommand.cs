using Foundry.Notifications.Domain.Channels.Push.Enums;
using Foundry.Notifications.Domain.Channels.Push.Identity;

namespace Foundry.Notifications.Application.Channels.Push.Commands.DeliverPush;

public sealed record DeliverPushCommand(
    PushMessageId PushMessageId,
    DeviceRegistrationId DeviceRegistrationId,
    string Token,
    PushPlatform Platform);
