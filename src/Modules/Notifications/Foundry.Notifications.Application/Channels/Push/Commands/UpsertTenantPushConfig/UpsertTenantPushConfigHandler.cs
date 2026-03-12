using Foundry.Notifications.Application.Channels.Push.Interfaces;
using Foundry.Notifications.Domain.Channels.Push.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Application.Channels.Push.Commands.UpsertTenantPushConfig;

public sealed class UpsertTenantPushConfigHandler(
    ITenantPushConfigurationRepository configurationRepository,
    IPushCredentialEncryptor credentialEncryptor,
    TimeProvider timeProvider)
{
    public async Task<Result> Handle(
        UpsertTenantPushConfigCommand command,
        CancellationToken cancellationToken)
    {
        string encryptedCredentials = credentialEncryptor.Encrypt(command.RawCredentials);

        TenantPushConfiguration? existing = await configurationRepository.GetByPlatformAsync(
            command.Platform,
            cancellationToken);

        if (existing is not null)
        {
            existing.UpdateCredentials(encryptedCredentials, timeProvider);
        }
        else
        {
            TenantPushConfiguration configuration = TenantPushConfiguration.Create(
                command.TenantId,
                command.Platform,
                encryptedCredentials,
                timeProvider);

            existing = configuration;
        }

        await configurationRepository.UpsertAsync(existing, cancellationToken);

        return Result.Success();
    }
}
