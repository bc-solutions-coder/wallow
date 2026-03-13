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
}
