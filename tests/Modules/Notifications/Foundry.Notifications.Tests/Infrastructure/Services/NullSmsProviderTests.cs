using Foundry.Notifications.Application.Channels.Sms.Interfaces;
using Foundry.Notifications.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Foundry.Notifications.Tests.Infrastructure.Services;

public class NullSmsProviderTests
{
    private readonly NullSmsProvider _provider = new(NullLogger<NullSmsProvider>.Instance);

    [Fact]
    public async Task SendAsync_AlwaysReturnsSuccess()
    {
        SmsDeliveryResult result = await _provider.SendAsync("+12025550100", "Test message");

        result.Success.Should().BeTrue();
        result.MessageSid.Should().Be("null-sid");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_WithDifferentInput_ReturnsSuccess()
    {
        SmsDeliveryResult result = await _provider.SendAsync("+447911123456", "Another message");

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_WithCancellationToken_ReturnsSuccess()
    {
        using CancellationTokenSource cts = new();

        SmsDeliveryResult result = await _provider.SendAsync("+12025550100", "Test", cts.Token);

        result.Success.Should().BeTrue();
        result.MessageSid.Should().Be("null-sid");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_WithEmptyBody_ReturnsSuccess()
    {
        SmsDeliveryResult result = await _provider.SendAsync("+12025550100", string.Empty);

        result.Success.Should().BeTrue();
        result.MessageSid.Should().Be("null-sid");
    }

    [Fact]
    public async Task SendAsync_WithLongMessage_ReturnsSuccess()
    {
        string longMessage = new string('X', 1600);

        SmsDeliveryResult result = await _provider.SendAsync("+12025550100", longMessage);

        result.Success.Should().BeTrue();
        result.MessageSid.Should().Be("null-sid");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_MultipleCalls_EachReturnsIndependentSuccess()
    {
        SmsDeliveryResult result1 = await _provider.SendAsync("+11111111111", "First");
        SmsDeliveryResult result2 = await _provider.SendAsync("+22222222222", "Second");

        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result1.MessageSid.Should().Be("null-sid");
        result2.MessageSid.Should().Be("null-sid");
    }
}
