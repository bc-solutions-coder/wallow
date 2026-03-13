using Foundry.Notifications.Application.Channels.Email.Commands.SendEmail;
using Foundry.Notifications.Application.Channels.Email.DTOs;
using Foundry.Notifications.Application.Channels.Email.Interfaces;
using Foundry.Notifications.Application.Preferences.Interfaces;
using Foundry.Notifications.Domain.Channels.Email.Entities;
using Foundry.Notifications.Domain.Preferences;
using Foundry.Shared.Contracts.Communications.Email;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Tests.Application.Commands.Email;

public class SendEmailHandlerTests
{
    private readonly IEmailMessageRepository _emailMessageRepository = Substitute.For<IEmailMessageRepository>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly INotificationPreferenceChecker _preferenceChecker = Substitute.For<INotificationPreferenceChecker>();
    private readonly SendEmailHandler _handler;

    public SendEmailHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        _tenantContext.TenantId.Returns(TenantId.New());
        _handler = new SendEmailHandler(
            _emailMessageRepository,
            _emailService,
            _tenantContext,
            _timeProvider,
            _preferenceChecker);
    }

    [Fact]
    public async Task Handle_WithValidCommand_SendsEmailAndSaves()
    {
        SendEmailCommand command = new("user@test.com", null, "Welcome", "Hello!");

        Result<EmailDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _emailMessageRepository.Received(1).Add(Arg.Any<EmailMessage>());
        await _emailService.Received(1).SendAsync(
            "user@test.com", null, "Welcome", "Hello!",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPreferenceDisabled_SkipsEmailAndReturnsSuccess()
    {
        UserId userId = new(Guid.NewGuid());
        SendEmailCommand command = new("user@test.com", null, "Subject", "Body",
            UserId: userId, NotificationType: "InvoiceCreated");

        _preferenceChecker
            .IsChannelEnabledAsync(userId, ChannelType.Email, "InvoiceCreated", Arg.Any<CancellationToken>())
            .Returns(false);

        Result<EmailDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _emailMessageRepository.DidNotReceive().Add(Arg.Any<EmailMessage>());
        await _emailService.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEmailServiceThrows_MarksEmailAsFailed()
    {
        SendEmailCommand command = new("user@test.com", null, "Subject", "Body");

        _emailService
            .SendAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(Task.FromException(new InvalidOperationException("SMTP error")));

        Result<EmailDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _emailMessageRepository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithFromAddress_PassesFromToEmailService()
    {
        SendEmailCommand command = new("to@test.com", "from@test.com", "Subject", "Body");

        await _handler.Handle(command, CancellationToken.None);

        await _emailService.Received(1).SendAsync(
            "to@test.com", "from@test.com", "Subject", "Body",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoUserIdOrNotificationType_SkipsPreferenceCheck()
    {
        SendEmailCommand command = new("user@test.com", null, "Subject", "Body");

        await _handler.Handle(command, CancellationToken.None);

        await _preferenceChecker.DidNotReceive().IsChannelEnabledAsync(
            Arg.Any<UserId>(), Arg.Any<ChannelType>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
