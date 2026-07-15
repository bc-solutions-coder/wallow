using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Application.Channels.Push.Commands.RemoveTenantPushConfig;

public sealed class RemoveTenantPushConfigHandler(ITenantPushConfigurationRepository configurationRepository)
{
    public async Task<Result> Handle(
        RemoveTenantPushConfigCommand command,
        CancellationToken cancellationToken)
    {
        await configurationRepository.DeleteByPlatformAsync(
            command.Platform,
            cancellationToken);

        return Result.Success();
    }
}
