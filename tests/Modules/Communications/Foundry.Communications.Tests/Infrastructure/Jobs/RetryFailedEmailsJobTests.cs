using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Channels.Email.ValueObjects;
using Foundry.Communications.Infrastructure.Jobs;
using Foundry.Shared.Contracts.Communications.Email;
using Foundry.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Foundry.Communications.Tests.Infrastructure.Jobs;

public class RetryFailedEmailsJobTests
{
    private readonly IEmailMessageRepository _emailMessageRepository = Substitute.For<IEmailMessageRepository>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly ILogger<RetryFailedEmailsJob> _logger = NullLoggerFactory.Instance.CreateLogger<RetryFailedEmailsJob>();
    private readonly RetryFailedEmailsJob _sut;

    public RetryFailedEmailsJobTests()
    {
        _sut = new RetryFailedEmailsJob(_emailMessageRepository, _emailService, TimeProvider.System, _logger);
    }

    [Fact]
    public async Task ExecuteAsync_WithRetryableFailedEmails_ResetsAndSendsEach()
    {
        EmailMessage message1 = CreateFailedEmailMessage();
        EmailMessage message2 = CreateFailedEmailMessage();
        List<EmailMessage> failedMessages = [message1, message2];

        _emailMessageRepository
            .GetFailedRetryableAsync(3, 100, Arg.Any<CancellationToken>())
            .Returns(failedMessages);

        await _sut.ExecuteAsync();

        await _emailService.Received(2).SendAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await _emailMessageRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_CallsGetFailedRetryableWithMaxRetriesThree()
    {
        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<EmailMessage>().AsReadOnly());

        await _sut.ExecuteAsync();

        await _emailMessageRepository.Received(1).GetFailedRetryableAsync(
            3,
            100,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoFailedEmails_DoesNotSendAnyEmails()
    {
        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<EmailMessage>().AsReadOnly());

        await _sut.ExecuteAsync();

        await _emailService.DidNotReceive().SendAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await _emailMessageRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static readonly TenantId _testTenantId = TenantId.New();

    private static EmailMessage CreateFailedEmailMessage()
    {
        EmailAddress to = EmailAddress.Create("test@example.com");
        EmailAddress from = EmailAddress.Create("sender@example.com");
        EmailContent content = EmailContent.Create("Test Subject", "Test Body");
        EmailMessage message = EmailMessage.Create(_testTenantId, to, from, content, TimeProvider.System);
        message.MarkAsFailed("Transient error", TimeProvider.System);
        return message;
    }
}
