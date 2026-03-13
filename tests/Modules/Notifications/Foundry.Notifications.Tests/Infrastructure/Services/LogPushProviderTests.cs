using Foundry.Notifications.Application.Channels.Push.Interfaces;
using Foundry.Notifications.Domain.Channels.Push.Entities;
using Foundry.Notifications.Infrastructure.Services;
using Foundry.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace Foundry.Notifications.Tests.Infrastructure.Services;

public class LogPushProviderTests
{
    private readonly LogPushProvider _provider = new(NullLogger<LogPushProvider>.Instance);

    [Fact]
    public async Task SendAsync_AlwaysReturnsSuccess()
    {
        PushMessage message = PushMessage.Create(
            TenantId.New(), new UserId(Guid.NewGuid()), "Test Title", "Test Body", TimeProvider.System);

        PushDeliveryResult result = await _provider.SendAsync(message, "device-token-abc");

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }
}
