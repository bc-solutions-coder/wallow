using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Communications.Infrastructure.Services;

namespace Foundry.Communications.Tests.Infrastructure.Services;

public class EmailProviderAdapterTests
{
    private readonly IEmailProvider _emailProvider = Substitute.For<IEmailProvider>();
    private readonly EmailProviderAdapter _adapter;

    public EmailProviderAdapterTests()
    {
        _adapter = new EmailProviderAdapter(_emailProvider);
    }

    [Fact]
    public async Task SendAsync_DelegatesToEmailProvider_WithCorrectParameters()
    {
        _emailProvider.SendAsync(Arg.Any<EmailDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailDeliveryResult(true, null));

        await _adapter.SendAsync("to@test.com", "from@test.com", "Subject", "<p>Body</p>");

        await _emailProvider.Received(1).SendAsync(
            Arg.Is<EmailDeliveryRequest>(r =>
                r.To == "to@test.com" &&
                r.From == "from@test.com" &&
                r.Subject == "Subject" &&
                r.Body == "<p>Body</p>" &&
                !r.Attachment.HasValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithNullFrom_PassesNullToProvider()
    {
        _emailProvider.SendAsync(Arg.Any<EmailDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailDeliveryResult(true, null));

        await _adapter.SendAsync("to@test.com", null, "Subject", "<p>Body</p>");

        await _emailProvider.Received(1).SendAsync(
            Arg.Is<EmailDeliveryRequest>(r => r.From == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WhenProviderReturnsFailure_ThrowsInvalidOperationException()
    {
        _emailProvider.SendAsync(Arg.Any<EmailDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailDeliveryResult(false, "SMTP connection refused"));

        Func<Task> act = () => _adapter.SendAsync("to@test.com", null, "Subject", "Body");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to send email to to@test.com*SMTP connection refused*");
    }

    [Fact]
    public async Task SendAsync_WhenProviderSucceeds_DoesNotThrow()
    {
        _emailProvider.SendAsync(Arg.Any<EmailDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailDeliveryResult(true, null));

        Func<Task> act = () => _adapter.SendAsync("to@test.com", null, "Subject", "Body");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendAsync_PassesCancellationToken()
    {
        _emailProvider.SendAsync(Arg.Any<EmailDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailDeliveryResult(true, null));
        using CancellationTokenSource cts = new();
        CancellationToken token = cts.Token;

        await _adapter.SendAsync("to@test.com", null, "Subject", "Body", token);

        await _emailProvider.Received(1).SendAsync(
            Arg.Any<EmailDeliveryRequest>(),
            token);
    }

    [Fact]
    public async Task SendWithAttachmentAsync_DelegatesToEmailProvider_WithCorrectParameters()
    {
        _emailProvider.SendAsync(Arg.Any<EmailDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailDeliveryResult(true, null));
        byte[] attachment = [1, 2, 3, 4, 5];

        await _adapter.SendWithAttachmentAsync(
            "to@test.com", "from@test.com", "Subject", "<p>Body</p>",
            attachment, "report.pdf", "application/pdf");

        await _emailProvider.Received(1).SendAsync(
            Arg.Is<EmailDeliveryRequest>(r =>
                r.To == "to@test.com" &&
                r.From == "from@test.com" &&
                r.Subject == "Subject" &&
                r.Body == "<p>Body</p>" &&
                r.Attachment.HasValue &&
                r.AttachmentName == "report.pdf" &&
                r.AttachmentContentType == "application/pdf"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendWithAttachmentAsync_WithDefaultContentType_UsesOctetStream()
    {
        _emailProvider.SendAsync(Arg.Any<EmailDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailDeliveryResult(true, null));
        byte[] attachment = [1, 2, 3];

        await _adapter.SendWithAttachmentAsync(
            "to@test.com", null, "Subject", "Body",
            attachment, "file.bin");

        await _emailProvider.Received(1).SendAsync(
            Arg.Is<EmailDeliveryRequest>(r =>
                r.AttachmentContentType == "application/octet-stream"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendWithAttachmentAsync_MapsAttachmentBytesToReadOnlyMemory()
    {
        _emailProvider.SendAsync(Arg.Any<EmailDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailDeliveryResult(true, null));
        byte[] attachment = [10, 20, 30];

        await _adapter.SendWithAttachmentAsync(
            "to@test.com", null, "Subject", "Body",
            attachment, "data.bin");

        await _emailProvider.Received(1).SendAsync(
            Arg.Is<EmailDeliveryRequest>(r =>
                r.Attachment.HasValue &&
                r.Attachment.Value.Length == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendWithAttachmentAsync_WhenProviderReturnsFailure_ThrowsInvalidOperationException()
    {
        _emailProvider.SendAsync(Arg.Any<EmailDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailDeliveryResult(false, "Attachment too large"));
        byte[] attachment = [1, 2, 3];

        Func<Task> act = () => _adapter.SendWithAttachmentAsync(
            "to@test.com", null, "Subject", "Body",
            attachment, "file.bin");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to send email to to@test.com*Attachment too large*");
    }

    [Fact]
    public async Task SendWithAttachmentAsync_WhenProviderSucceeds_DoesNotThrow()
    {
        _emailProvider.SendAsync(Arg.Any<EmailDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailDeliveryResult(true, null));
        byte[] attachment = [1, 2, 3];

        Func<Task> act = () => _adapter.SendWithAttachmentAsync(
            "to@test.com", null, "Subject", "Body",
            attachment, "file.bin");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendWithAttachmentAsync_PassesCancellationToken()
    {
        _emailProvider.SendAsync(Arg.Any<EmailDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EmailDeliveryResult(true, null));
        using CancellationTokenSource cts = new();
        CancellationToken token = cts.Token;
        byte[] attachment = [1];

        await _adapter.SendWithAttachmentAsync(
            "to@test.com", null, "Subject", "Body",
            attachment, "file.bin", "text/plain", token);

        await _emailProvider.Received(1).SendAsync(
            Arg.Any<EmailDeliveryRequest>(),
            token);
    }
}
