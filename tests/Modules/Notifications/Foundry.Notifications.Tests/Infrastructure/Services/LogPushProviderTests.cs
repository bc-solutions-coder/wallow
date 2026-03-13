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

    [Fact]
    public async Task SendAsync_WithDifferentDeviceToken_ReturnsSuccess()
    {
        PushMessage message = PushMessage.Create(
            TenantId.New(), new UserId(Guid.NewGuid()), "Alert", "Something happened", TimeProvider.System);

        PushDeliveryResult result = await _provider.SendAsync(message, "another-device-token-xyz");

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_WithEmptyBody_ReturnsSuccess()
    {
        PushMessage message = PushMessage.Create(
            TenantId.New(), new UserId(Guid.NewGuid()), "Title Only", string.Empty, TimeProvider.System);

        PushDeliveryResult result = await _provider.SendAsync(message, "device-token");

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_WithCancellationToken_ReturnsSuccess()
    {
        PushMessage message = PushMessage.Create(
            TenantId.New(), new UserId(Guid.NewGuid()), "Test", "Body", TimeProvider.System);
        using CancellationTokenSource cts = new();

        PushDeliveryResult result = await _provider.SendAsync(message, "device-token", cts.Token);

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_WithLongTitle_ReturnsSuccess()
    {
        string longTitle = new string('A', 500);
        PushMessage message = PushMessage.Create(
            TenantId.New(), new UserId(Guid.NewGuid()), longTitle, "Body", TimeProvider.System);

        PushDeliveryResult result = await _provider.SendAsync(message, "device-token");

        result.Success.Should().BeTrue();
    }
}
