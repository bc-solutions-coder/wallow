using Foundry.Communications.Application.Channels.Email.EventHandlers;
using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Shared.Contracts.Communications.Email;
using Foundry.Shared.Contracts.Communications.Email.Events;
using Microsoft.Extensions.Logging;

namespace Foundry.Communications.Tests.Application.Channels.Email.EventHandlers;

public class SendEmailRequestedEventHandlerTests
{
    private readonly IEmailService _emailService;
    private readonly IEmailMessageRepository _emailMessageRepository;
    private readonly ILogger<SendEmailRequestedEvent> _logger;

    public SendEmailRequestedEventHandlerTests()
    {
        _emailService = Substitute.For<IEmailService>();
        _emailMessageRepository = Substitute.For<IEmailMessageRepository>();
        _logger = Substitute.For<ILogger<SendEmailRequestedEvent>>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
    }

    [Fact]
    public async Task HandleAsync_SendsEmail()
    {
        SendEmailRequestedEvent evt = new()
        {
            TenantId = Guid.NewGuid(),
            To = "user@example.com",
            From = "noreply@example.com",
            Subject = "Test Subject",
            Body = "Test Body",
            SourceModule = "Identity"
        };

        await SendEmailRequestedEventHandler.HandleAsync(
            evt, _emailService, _emailMessageRepository, TimeProvider.System, _logger, CancellationToken.None);

        await _emailService.Received(1).SendAsync(
            "user@example.com",
            "noreply@example.com",
            "Test Subject",
            "Test Body",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithNullFrom_SendsEmailWithNullFrom()
    {
        SendEmailRequestedEvent evt = new()
        {
            TenantId = Guid.NewGuid(),
            To = "user@example.com",
            From = null,
            Subject = "Subject",
            Body = "Body",
            SourceModule = "Billing"
        };

        await SendEmailRequestedEventHandler.HandleAsync(
            evt, _emailService, _emailMessageRepository, TimeProvider.System, _logger, CancellationToken.None);

        await _emailService.Received(1).SendAsync(
            "user@example.com", null, "Subject", "Body", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenSendFails_RethrowsException()
    {
        _emailService.SendAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("SMTP error")));

        SendEmailRequestedEvent evt = new()
        {
            TenantId = Guid.NewGuid(),
            To = "user@example.com",
            Subject = "Subject",
            Body = "Body",
            SourceModule = "Test"
        };

        Func<Task> act = () => SendEmailRequestedEventHandler.HandleAsync(
            evt, _emailService, _emailMessageRepository, TimeProvider.System, _logger, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("SMTP error");
    }

    [Fact]
    public async Task HandleAsync_WithNullSourceModule_UsesUnknown()
    {
        SendEmailRequestedEvent evt = new()
        {
            TenantId = Guid.NewGuid(),
            To = "user@example.com",
            Subject = "Subject",
            Body = "Body",
            SourceModule = null
        };

        await SendEmailRequestedEventHandler.HandleAsync(
            evt, _emailService, _emailMessageRepository, TimeProvider.System, _logger, CancellationToken.None);

        await _emailService.Received(1).SendAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        SendEmailRequestedEvent evt = new()
        {
            TenantId = Guid.NewGuid(),
            To = "user@example.com",
            Subject = "Subject",
            Body = "Body"
        };

        await SendEmailRequestedEventHandler.HandleAsync(
            evt, _emailService, _emailMessageRepository, TimeProvider.System, _logger, cts.Token);

        await _emailService.Received(1).SendAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), cts.Token);
    }
}
