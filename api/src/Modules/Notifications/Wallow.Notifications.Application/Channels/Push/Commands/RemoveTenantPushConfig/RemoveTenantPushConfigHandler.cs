using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Application.Channels.Push.Commands.RemoveTenantPushConfig;

public sealed class RemoveTenantPushConfigHandler(ITenantPushConfigurationRepository configurationRepository)
{
    public async Task<Result> Handle(
        RemoveTenantPushConfigCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Platform == Domain.Channels.Push.Enums.PushPlatform.WebPush)
        {
            return Result.Failure(Domain.Errors.NotificationsErrors.WebPushInvalidConfiguration);
        }
        await configurationRepository.DeleteByPlatformAsync(
            command.Platform,
            cancellationToken);

        return Result.Success();
    }
}
