using Foundry.Communications.Application.Channels.Email.Commands.SendEmail;
using Foundry.Communications.Application.Channels.Email.DTOs;
using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Channels.Email.Enums;
using Foundry.Shared.Contracts.Communications.Email;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Channels.Email.Handlers;

public class SendEmailHandlerTests
{
    private readonly IEmailMessageRepository _repository;
    private readonly IEmailService _emailService;
    private readonly SendEmailHandler _handler;

    public SendEmailHandlerTests()
    {
        _repository = Substitute.For<IEmailMessageRepository>();
        _emailService = Substitute.For<IEmailService>();
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId.New());
        _handler = new SendEmailHandler(_repository, _emailService, tenantContext, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccess()
    {
        SendEmailCommand command = new("to@example.com", "from@example.com", "Subject", "Body");

        Result<EmailDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.To.Should().Be("to@example.com");
        result.Value.Subject.Should().Be("Subject");
    }

    [Fact]
    public async Task Handle_WithValidCommand_AddsEmailToRepository()
    {
        SendEmailCommand command = new("to@example.com", null, "Subject", "Body");

        await _handler.Handle(command, CancellationToken.None);

        _repository.Received(1).Add(Arg.Any<EmailMessage>());
        await _repository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSendSucceeds_MarkEmailAsSent()
    {
        SendEmailCommand command = new("to@example.com", null, "Subject", "Body");

        Result<EmailDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(EmailStatus.Sent);
    }

    [Fact]
    public async Task Handle_WhenSendFails_MarkEmailAsFailed()
    {
        _emailService.SendAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("SMTP error")));

        SendEmailCommand command = new("to@example.com", null, "Subject", "Body");

        Result<EmailDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(EmailStatus.Failed);
        result.Value.FailureReason.Should().Contain("SMTP error");
    }

    [Fact]
    public async Task Handle_WithNullFrom_SendsEmail()
    {
        SendEmailCommand command = new("to@example.com", null, "Subject", "Body");

        await _handler.Handle(command, CancellationToken.None);

        await _emailService.Received(1).SendAsync(
            "to@example.com", null, "Subject", "Body", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithFrom_SendsEmailWithFrom()
    {
        SendEmailCommand command = new("to@example.com", "from@example.com", "Subject", "Body");

        await _handler.Handle(command, CancellationToken.None);

        await _emailService.Received(1).SendAsync(
            "to@example.com", "from@example.com", "Subject", "Body", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SavesChangesBeforeAndAfterSend()
    {
        SendEmailCommand command = new("to@example.com", null, "Subject", "Body");

        await _handler.Handle(command, CancellationToken.None);

        // First save after Add, second save after MarkAsSent/MarkAsFailed
        await _repository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        SendEmailCommand command = new("to@example.com", null, "Subject", "Body");

        await _handler.Handle(command, cts.Token);

        await _emailService.Received(1).SendAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), cts.Token);
    }
}
