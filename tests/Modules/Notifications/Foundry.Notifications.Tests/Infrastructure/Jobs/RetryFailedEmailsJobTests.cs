using Foundry.Notifications.Application.Channels.Email.Interfaces;
using Foundry.Notifications.Domain.Channels.Email.Entities;
using Foundry.Notifications.Domain.Channels.Email.ValueObjects;
using Foundry.Notifications.Infrastructure.Jobs;
using Foundry.Shared.Contracts.Communications.Email;
using Foundry.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Foundry.Notifications.Tests.Infrastructure.Jobs;

public class RetryFailedEmailsJobTests
{
    private readonly IEmailMessageRepository _emailMessageRepository = Substitute.For<IEmailMessageRepository>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly RetryFailedEmailsJob _sut;

    public RetryFailedEmailsJobTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        ILogger<RetryFailedEmailsJob> logger = NullLogger<RetryFailedEmailsJob>.Instance;
        _sut = new RetryFailedEmailsJob(_emailMessageRepository, _emailService, _timeProvider, logger);
    }

    [Fact]
    public async Task ExecuteAsync_WithFailedEmails_RetriesAndSavesChanges()
    {
        EmailMessage message = CreateFailedEmailMessage();
        IReadOnlyList<EmailMessage> failedMessages = new List<EmailMessage> { message };

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(failedMessages);

        await _sut.ExecuteAsync(CancellationToken.None);

        await _emailService.Received(1).SendAsync(
            message.To.Value,
            message.From?.Value,
            message.Content.Subject,
            message.Content.Body,
            Arg.Any<CancellationToken>());

        await _emailMessageRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNoFailedEmails_SavesChangesWithoutSending()
    {
        IReadOnlyList<EmailMessage> emptyList = new List<EmailMessage>();

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(emptyList);

        await _sut.ExecuteAsync(CancellationToken.None);

        await _emailService.DidNotReceive().SendAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await _emailMessageRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSendThrows_MarksAsFailedAndContinues()
    {
        EmailMessage message1 = CreateFailedEmailMessage();
        EmailMessage message2 = CreateFailedEmailMessage();
        IReadOnlyList<EmailMessage> failedMessages = new List<EmailMessage> { message1, message2 };

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(failedMessages);

        _emailService
            .When(x => x.SendAsync(message1.To.Value, Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("SMTP error"));

        await _sut.ExecuteAsync(CancellationToken.None);

        await _emailService.Received(2).SendAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await _emailMessageRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_PropagatesException()
    {
        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<EmailMessage>>(_ => throw new InvalidOperationException("DB error"));

        Func<Task> act = () => _sut.ExecuteAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*DB error*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_StopsProcessingEarly()
    {
        EmailMessage message1 = CreateFailedEmailMessage();
        EmailMessage message2 = CreateFailedEmailMessage();
        IReadOnlyList<EmailMessage> failedMessages = new List<EmailMessage> { message1, message2 };

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(failedMessages);

        using CancellationTokenSource cts = new();

        _emailService
            .SendAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => cts.Cancel());

        await _sut.ExecuteAsync(cts.Token);

        await _emailService.Received(1).SendAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleSuccessfulRetries_IncrementsRetriedCount()
    {
        EmailMessage message1 = CreateFailedEmailMessage();
        EmailMessage message2 = CreateFailedEmailMessage();
        EmailMessage message3 = CreateFailedEmailMessage();
        IReadOnlyList<EmailMessage> failedMessages = new List<EmailMessage> { message1, message2, message3 };

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(failedMessages);

        await _sut.ExecuteAsync(CancellationToken.None);

        await _emailService.Received(3).SendAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await _emailMessageRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSaveChangesThrows_PropagatesException()
    {
        EmailMessage message = CreateFailedEmailMessage();
        IReadOnlyList<EmailMessage> failedMessages = new List<EmailMessage> { message };

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(failedMessages);

        _emailMessageRepository
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Save failed"));

        Func<Task> act = () => _sut.ExecuteAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Save failed*");
    }

    [Fact]
    public async Task ExecuteAsync_WithDefaultCancellationToken_CompletesSuccessfully()
    {
        IReadOnlyList<EmailMessage> emptyList = new List<EmailMessage>();

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(emptyList);

        await _sut.ExecuteAsync();

        await _emailMessageRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PassesCorrectParametersToRepository()
    {
        IReadOnlyList<EmailMessage> emptyList = new List<EmailMessage>();

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(emptyList);

        await _sut.ExecuteAsync(CancellationToken.None);

        await _emailMessageRepository.Received(1).GetFailedRetryableAsync(3, 100, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllSendsFail_StillSavesChanges()
    {
        EmailMessage message1 = CreateFailedEmailMessage();
        EmailMessage message2 = CreateFailedEmailMessage();
        IReadOnlyList<EmailMessage> failedMessages = new List<EmailMessage> { message1, message2 };

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(failedMessages);

        _emailService
            .SendAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("SMTP down"));

        await _sut.ExecuteAsync(CancellationToken.None);

        await _emailMessageRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private EmailMessage CreateFailedEmailMessage()
    {
        EmailAddress to = EmailAddress.Create($"test-{Guid.NewGuid():N}@example.com");
        EmailAddress from = EmailAddress.Create("noreply@foundry.dev");
        EmailContent content = EmailContent.Create("Test Subject", "Test Body");
        TenantId tenantId = TenantId.New();

        EmailMessage message = EmailMessage.Create(tenantId, to, from, content, _timeProvider);
        message.MarkAsFailed("Initial failure", _timeProvider);
        return message;
    }
}
