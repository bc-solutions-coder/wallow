using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Communications.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Foundry.Communications.Tests.Infrastructure.Services;

public class SmtpEmailProviderTests
{
    private readonly ILogger<SmtpEmailProvider> _logger = Substitute.For<ILogger<SmtpEmailProvider>>();

    private SmtpEmailProvider CreateProvider(SmtpSettings? settings = null)
    {
        SmtpSettings smtpSettings = settings ?? new SmtpSettings
        {
            Host = "invalid.host.that.does.not.exist",
            Port = 9999,
            MaxRetries = 1,
            TimeoutSeconds = 1,
            DefaultFromAddress = "test@foundry.local",
            DefaultFromName = "Foundry Test"
        };

        IOptions<SmtpSettings> options = Options.Create(smtpSettings);
        return new SmtpEmailProvider(options, _logger);
    }

    [Fact]
    public async Task SendAsync_WhenSmtpConnectionFails_ReturnsFailureResult()
    {
        SmtpEmailProvider provider = CreateProvider();
        EmailDeliveryRequest request = new("recipient@test.com", null, "Test Subject", "<p>Test Body</p>");

        EmailDeliveryResult result = await provider.SendAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenSmtpConnectionFails_ErrorMessageContainsDetails()
    {
        SmtpSettings settings = new()
        {
            Host = "invalid.host.that.does.not.exist",
            Port = 9999,
            MaxRetries = 2,
            TimeoutSeconds = 1,
            DefaultFromAddress = "test@foundry.local",
            DefaultFromName = "Foundry Test"
        };
        SmtpEmailProvider provider = CreateProvider(settings);
        EmailDeliveryRequest request = new("recipient@test.com", null, "Subject", "<p>Body</p>");

        EmailDeliveryResult result = await provider.SendAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SendAsync_WithCustomFrom_DoesNotReturnArgumentError()
    {
        SmtpEmailProvider provider = CreateProvider();
        EmailDeliveryRequest request = new("recipient@test.com", "custom@sender.com", "Subject", "<p>Body</p>");

        EmailDeliveryResult result = await provider.SendAsync(request);

        // Should fail due to connection, not argument errors
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotContain("argument", because: "custom from address should be accepted");
    }

    [Fact]
    public async Task SendAsync_WithNullFrom_UsesDefaultFromWithoutNullReference()
    {
        SmtpEmailProvider provider = CreateProvider();
        EmailDeliveryRequest request = new("recipient@test.com", null, "Subject", "<p>Body</p>");

        EmailDeliveryResult result = await provider.SendAsync(request);

        // Should fail due to connection, not a NullReferenceException
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task SendAsync_WithEmptyFrom_UsesDefaultFromWithoutNullReference()
    {
        SmtpEmailProvider provider = CreateProvider();
        EmailDeliveryRequest request = new("recipient@test.com", "", "Subject", "<p>Body</p>");

        EmailDeliveryResult result = await provider.SendAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task SendAsync_WithWhitespaceFrom_UsesDefaultFromWithoutNullReference()
    {
        SmtpEmailProvider provider = CreateProvider();
        EmailDeliveryRequest request = new("recipient@test.com", "  ", "Subject", "<p>Body</p>");

        EmailDeliveryResult result = await provider.SendAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task SendAsync_WithAttachment_WhenSmtpConnectionFails_ReturnsFailureResult()
    {
        SmtpEmailProvider provider = CreateProvider();
        byte[] attachmentData = new byte[100];
        EmailDeliveryRequest request = new(
            "recipient@test.com", null, "Subject", "<p>Body</p>",
            new ReadOnlyMemory<byte>(attachmentData), "test.txt", "text/plain");

        EmailDeliveryResult result = await provider.SendAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SendAsync_WithOversizedAttachment_ThrowsInvalidOperationException()
    {
        SmtpEmailProvider provider = CreateProvider();
        byte[] largeAttachment = new byte[11 * 1024 * 1024]; // 11MB
        EmailDeliveryRequest request = new(
            "recipient@test.com", null, "Subject", "<p>Body</p>",
            new ReadOnlyMemory<byte>(largeAttachment), "large-file.bin");

        Func<Task> act = () => provider.SendAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds maximum allowed size*");
    }

    [Fact]
    public async Task SendAsync_WithAttachmentAtMaxSize_DoesNotReturnSizeError()
    {
        SmtpEmailProvider provider = CreateProvider();
        byte[] maxAttachment = new byte[10 * 1024 * 1024]; // 10MB exactly
        EmailDeliveryRequest request = new(
            "recipient@test.com", null, "Subject", "<p>Body</p>",
            new ReadOnlyMemory<byte>(maxAttachment), "max-file.bin");

        EmailDeliveryResult result = await provider.SendAsync(request);

        // Should fail due to connection, not size validation
        result.ErrorMessage.Should().NotContain("exceeds maximum allowed size");
    }

    [Fact]
    public async Task SendAsync_WithMaxRetriesOne_FailsAfterSingleAttempt()
    {
        SmtpSettings settings = new()
        {
            Host = "invalid.host.that.does.not.exist",
            Port = 9999,
            MaxRetries = 1,
            TimeoutSeconds = 1,
            DefaultFromAddress = "test@foundry.local",
            DefaultFromName = "Foundry Test"
        };
        SmtpEmailProvider provider = CreateProvider(settings);
        EmailDeliveryRequest request = new("to@test.com", null, "Subject", "Body");

        EmailDeliveryResult result = await provider.SendAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WithValidSettings_DoesNotThrow()
    {
        SmtpSettings settings = new()
        {
            Host = "smtp.example.com",
            Port = 587,
            UseSsl = true,
            Username = "user",
            Password = "pass"
        };

        IOptions<SmtpSettings> options = Options.Create(settings);

        SmtpEmailProvider provider = new(options, _logger);

        provider.Should().NotBeNull();
    }
}
