using Foundry.Notifications.Application.Channels.Push.Interfaces;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Application.Channels.Push.Commands.RemoveTenantPushConfig;

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
