using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Notifications.Infrastructure.Services;

namespace Wallow.Notifications.Tests.Infrastructure.Services;

public class EmailProviderAdapterTests
{
    private readonly IEmailProvider _emailProvider = Substitute.For<IEmailProvider>();
    private readonly EmailProviderAdapter _adapter;

    public EmailProviderAdapterTests()
    {
        _adapter = new EmailProviderAdapter(_emailProvider);
    }

    [Fact]
    public async Task SendAsync_WhenProviderSucceeds_CompletesSuccessfully()
    {
        _emailProvider
            .SendAsync(Arg.Any<EmailDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailDeliveryResult(true, null));

        Func<Task> act = () => _adapter.SendAsync("user@test.com", null, "Subject", "Body");

        await act.Should().NotThrowAsync();
        await _emailProvider.Received(1).SendAsync(
            Arg.Is<EmailDeliveryRequest>(r =>
                r.To == "user@test.com" &&
                r.Subject == "Subject" &&
                r.Body == "Body"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WhenProviderFails_ThrowsInvalidOperationException()
    {
        _emailProvider
            .SendAsync(Arg.Any<EmailDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailDeliveryResult(false, "SMTP error"));

        Func<Task> act = () => _adapter.SendAsync("user@test.com", null, "Subject", "Body");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SMTP error*");
    }

    [Fact]
    public async Task SendAsync_WithFromAddress_PassesFromToProvider()
    {
        _emailProvider
            .SendAsync(Arg.Any<EmailDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailDeliveryResult(true, null));

        await _adapter.SendAsync("to@test.com", "from@test.com", "Subject", "Body");

        await _emailProvider.Received(1).SendAsync(
            Arg.Is<EmailDeliveryRequest>(r => r.From == "from@test.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendWithAttachmentAsync_WhenProviderSucceeds_CompletesSuccessfully()
    {
        _emailProvider
            .SendAsync(Arg.Any<EmailDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailDeliveryResult(true, null));

        byte[] attachment = new byte[] { 1, 2, 3 };

        Func<Task> act = () => _adapter.SendWithAttachmentAsync(
            "user@test.com", null, "Subject", "Body", attachment, "file.pdf");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendWithAttachmentAsync_WhenProviderFails_ThrowsInvalidOperationException()
    {
        _emailProvider
            .SendAsync(Arg.Any<EmailDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailDeliveryResult(false, "Auth error"));

        byte[] attachment = new byte[] { 1, 2, 3 };

        Func<Task> act = () => _adapter.SendWithAttachmentAsync(
            "user@test.com", null, "Subject", "Body", attachment, "file.pdf");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
