using Foundry.Notifications.Application.Channels.Push.Interfaces;
using Foundry.Notifications.Domain.Channels.Push.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Application.Channels.Push.Commands.SetTenantPushEnabled;

public sealed class SetTenantPushEnabledHandler(
    ITenantPushConfigurationRepository configurationRepository,
    TimeProvider timeProvider)
{
    public async Task<Result> Handle(
        SetTenantPushEnabledCommand command,
        CancellationToken cancellationToken)
    {
        TenantPushConfiguration? configuration = await configurationRepository.GetByPlatformAsync(
            command.Platform,
            cancellationToken);

        if (configuration is null)
        {
            return Result.Failure(Error.NotFound(nameof(TenantPushConfiguration), $"{command.TenantId}:{command.Platform}"));
        }

        if (command.IsEnabled)
        {
            configuration.Enable(timeProvider);
        }
        else
        {
            configuration.Disable(timeProvider);
        }

        await configurationRepository.UpsertAsync(configuration, cancellationToken);

        return Result.Success();
    }
}
