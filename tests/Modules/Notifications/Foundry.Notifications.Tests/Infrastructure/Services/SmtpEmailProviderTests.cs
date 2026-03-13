using Foundry.Notifications.Application.Channels.Email.Interfaces;
using Foundry.Notifications.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;

namespace Foundry.Notifications.Tests.Infrastructure.Services;

public sealed class SmtpEmailProviderTests : IAsyncLifetime, IDisposable
{
    private readonly SmtpConnectionPool _connectionPool;

    private readonly IOptions<SmtpSettings> _settings = Options.Create(new SmtpSettings
    {
        DefaultFromAddress = "noreply@test.com",
        DefaultFromName = "Test Sender",
        Host = "localhost",
        Port = 19999, // Intentionally wrong port — no server listening
        TimeoutSeconds = 1
    });

    private readonly ResiliencePipelineProvider<string> _pipelineProvider;

    public SmtpEmailProviderTests()
    {
        _connectionPool = new SmtpConnectionPool(_settings, NullLogger<SmtpConnectionPool>.Instance);

        ResiliencePipelineBuilder builder = new();
        ResiliencePipeline pipeline = builder.Build();

        _pipelineProvider = Substitute.For<ResiliencePipelineProvider<string>>();
        _pipelineProvider.GetPipeline("smtp").Returns(pipeline);
    }

    private SmtpEmailProvider CreateSut()
    {
        return new SmtpEmailProvider(_connectionPool, _settings, _pipelineProvider,
            NullLogger<SmtpEmailProvider>.Instance);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _connectionPool.DisposeAsync();
    }

    public void Dispose()
    {
        _connectionPool.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SendAsync_WhenSmtpUnavailable_ReturnsFailure()
    {
        SmtpEmailProvider sut = CreateSut();
        EmailDeliveryRequest request = new("recipient@test.com", null, "Test Subject", "<p>Hello</p>");

        EmailDeliveryResult result = await sut.SendAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SendAsync_WithOversizedAttachment_ThrowsInvalidOperationException()
    {
        SmtpEmailProvider sut = CreateSut();
        byte[] largeAttachment = new byte[11 * 1024 * 1024]; // 11MB exceeds 10MB limit
        EmailDeliveryRequest request = new(
            "recipient@test.com", null, "Subject", "Body",
            Attachment: new ReadOnlyMemory<byte>(largeAttachment),
            AttachmentName: "large.bin");

        Func<Task> act = () => sut.SendAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds maximum allowed size*");
    }

    [Fact]
    public async Task SendAsync_WithSmallAttachment_DoesNotFailOnAttachmentSize()
    {
        SmtpEmailProvider sut = CreateSut();
        byte[] data = "file content"u8.ToArray();
        EmailDeliveryRequest request = new(
            "recipient@test.com", null, "Subject", "Body",
            Attachment: new ReadOnlyMemory<byte>(data),
            AttachmentName: "report.pdf",
            AttachmentContentType: "application/pdf");

        EmailDeliveryResult result = await sut.SendAsync(request);

        // Will fail on SMTP connection, not on attachment validation
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotContain("exceeds maximum allowed size");
    }

    [Fact]
    public async Task SendAsync_WithNullFrom_DoesNotThrowOnMessageBuilding()
    {
        SmtpEmailProvider sut = CreateSut();
        EmailDeliveryRequest request = new("recipient@test.com", null, "Subject", "Body");

        // Should not throw — message building with null From should use defaults
        EmailDeliveryResult result = await sut.SendAsync(request);

        // Fails on SMTP, not message building
        result.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task SendAsync_WithCustomFrom_DoesNotThrowOnMessageBuilding()
    {
        SmtpEmailProvider sut = CreateSut();
        EmailDeliveryRequest request = new("recipient@test.com", "custom@sender.com", "Subject", "Body");

        EmailDeliveryResult result = await sut.SendAsync(request);

        // Fails on SMTP, not message building
        result.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task SendAsync_WithWhitespaceFrom_DoesNotThrowOnMessageBuilding()
    {
        SmtpEmailProvider sut = CreateSut();
        EmailDeliveryRequest request = new("recipient@test.com", "  ", "Subject", "Body");

        EmailDeliveryResult result = await sut.SendAsync(request);

        result.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task SendAsync_WithNullSubject_DoesNotThrowOnMessageBuilding()
    {
        SmtpEmailProvider sut = CreateSut();
        EmailDeliveryRequest request = new("recipient@test.com", null, null!, "Body");

        EmailDeliveryResult result = await sut.SendAsync(request);

        // Fails on SMTP, not on null subject handling
        result.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task SendAsync_WithAttachmentButNoName_DoesNotThrowOnMessageBuilding()
    {
        SmtpEmailProvider sut = CreateSut();
        byte[] data = "data"u8.ToArray();
        EmailDeliveryRequest request = new(
            "recipient@test.com", null, "Subject", "Body",
            Attachment: new ReadOnlyMemory<byte>(data),
            AttachmentName: null);

        EmailDeliveryResult result = await sut.SendAsync(request);

        result.ErrorMessage.Should().NotContain("exceeds maximum allowed size");
    }

    [Fact]
    public async Task SendAsync_WithExactly10MbAttachment_DoesNotFailOnSizeValidation()
    {
        SmtpEmailProvider sut = CreateSut();
        byte[] exactLimit = new byte[10 * 1024 * 1024]; // Exactly 10MB
        EmailDeliveryRequest request = new(
            "recipient@test.com", null, "Subject", "Body",
            Attachment: new ReadOnlyMemory<byte>(exactLimit),
            AttachmentName: "exact.bin");

        EmailDeliveryResult result = await sut.SendAsync(request);

        // Should fail on SMTP, not size validation (10MB is at the limit, not over)
        result.ErrorMessage.Should().NotContain("exceeds maximum allowed size");
    }
}
