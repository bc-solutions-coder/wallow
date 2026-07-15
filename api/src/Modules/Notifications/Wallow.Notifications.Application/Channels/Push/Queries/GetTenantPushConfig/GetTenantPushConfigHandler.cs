using Wallow.Notifications.Application.Channels.Push.DTOs;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Application.Channels.Push.Queries.GetTenantPushConfig;

public sealed class GetTenantPushConfigHandler(ITenantPushConfigurationRepository configurationRepository)
{
    private const string RedactedPlaceholder = "[redacted]";

    public async Task<Result<TenantPushConfigDto?>> Handle(
        GetTenantPushConfigQuery _,
        CancellationToken cancellationToken)
    {
        TenantPushConfiguration? configuration = await configurationRepository.GetAsync(cancellationToken);

        if (configuration is null)
        {
            return Result.Success<TenantPushConfigDto?>(null);
        }

        TenantPushConfigDto dto = new(
            configuration.Id.Value,
            configuration.TenantId.Value,
            configuration.Platform,
            RedactedPlaceholder,
            configuration.IsEnabled);

        return Result.Success<TenantPushConfigDto?>(dto);
    }
}
