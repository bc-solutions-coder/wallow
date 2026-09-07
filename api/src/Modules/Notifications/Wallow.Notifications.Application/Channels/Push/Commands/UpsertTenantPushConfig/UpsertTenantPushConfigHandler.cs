using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Application.Channels.Push.Commands.UpsertTenantPushConfig;

public sealed class UpsertTenantPushConfigHandler(
    ITenantPushConfigurationRepository configurationRepository,
    IPushCredentialEncryptor credentialEncryptor,
    TimeProvider timeProvider,
    IWebPushConfiguration webPushConfiguration)
{
    public async Task<Result> Handle(
        UpsertTenantPushConfigCommand command,
        CancellationToken cancellationToken)
    {
        string encryptedCredentials = credentialEncryptor.Encrypt(command.RawCredentials);

        TenantPushConfiguration? existing = await configurationRepository.GetByPlatformAsync(
            command.Platform,
            cancellationToken);

        if (command.Platform == Domain.Channels.Push.Enums.PushPlatform.WebPush
            && !await webPushConfiguration.ValidateReplacementAsync(command.RawCredentials, existing?.EncryptedCredentials, cancellationToken))
        {
            return Result.Failure(Domain.Errors.NotificationsErrors.WebPushInvalidConfiguration);
        }

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
