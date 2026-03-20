using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Notifications.Domain.Channels.Email.Entities;
using Wallow.Notifications.Domain.Channels.Email.Enums;
using Wallow.Notifications.Domain.Channels.Email.ValueObjects;
using Wallow.Notifications.Infrastructure.Jobs;
using Wallow.Shared.Contracts.Communications.Email;
using Wallow.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging;

namespace Wallow.Notifications.Tests.Infrastructure.Jobs;

public class RetryFailedEmailsJobTests
{
    private readonly IEmailMessageRepository _emailMessageRepository = Substitute.For<IEmailMessageRepository>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly RetryFailedEmailsJob _sut;

#pragma warning disable CA2000 // LoggerFactory disposal not needed in tests
    public RetryFailedEmailsJobTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        ILogger<RetryFailedEmailsJob> logger = LoggerFactory.Create(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Trace))
            .CreateLogger<RetryFailedEmailsJob>();
        _sut = new RetryFailedEmailsJob(_emailMessageRepository, _emailService, _timeProvider, logger);
    }
#pragma warning restore CA2000

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

    [Fact]
    public async Task ExecuteAsync_SuccessfulSend_MarksMessageAsSent()
    {
        EmailMessage message = CreateFailedEmailMessage();
        IReadOnlyList<EmailMessage> failedMessages = new List<EmailMessage> { message };

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(failedMessages);

        await _sut.ExecuteAsync(CancellationToken.None);

        message.Status.Should().Be(EmailStatus.Sent);
    }

    [Fact]
    public async Task ExecuteAsync_FailedSend_MarksMessageAsFailed()
    {
        EmailMessage message = CreateFailedEmailMessage();
        IReadOnlyList<EmailMessage> failedMessages = new List<EmailMessage> { message };

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(failedMessages);

        _emailService
            .SendAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Connection refused"));

        await _sut.ExecuteAsync(CancellationToken.None);

        message.Status.Should().Be(EmailStatus.Failed);
    }

    [Fact]
    public async Task ExecuteAsync_FailedSend_RecordsErrorMessage()
    {
        EmailMessage message = CreateFailedEmailMessage();
        IReadOnlyList<EmailMessage> failedMessages = new List<EmailMessage> { message };

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(failedMessages);

        _emailService
            .SendAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Timeout exceeded"));

        await _sut.ExecuteAsync(CancellationToken.None);

        message.FailureReason.Should().Contain("Timeout exceeded");
    }

    [Fact]
    public async Task ExecuteAsync_MixedResults_OnlySuccessfulMarkedAsSent()
    {
        EmailMessage successMessage = CreateFailedEmailMessage();
        EmailMessage failMessage = CreateFailedEmailMessage();
        IReadOnlyList<EmailMessage> failedMessages = new List<EmailMessage> { successMessage, failMessage };

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(failedMessages);

        int callCount = 0;
        _emailService
            .SendAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 2)
                {
                    throw new InvalidOperationException("Fail on second");
                }
                return Task.CompletedTask;
            });

        await _sut.ExecuteAsync(CancellationToken.None);

        successMessage.Status.Should().Be(EmailStatus.Sent);
        failMessage.Status.Should().Be(EmailStatus.Failed);
    }

    [Fact]
    public async Task ExecuteAsync_LogsStartAndCompletion()
    {
        FakeLogger<RetryFailedEmailsJob> fakeLogger = new();
        RetryFailedEmailsJob sut = new(_emailMessageRepository, _emailService, _timeProvider, fakeLogger);
        IReadOnlyList<EmailMessage> emptyList = new List<EmailMessage>();

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(emptyList);

        await sut.ExecuteAsync(CancellationToken.None);

        fakeLogger.LogEntries.Should().HaveCount(2);
        fakeLogger.LogEntries[0].LogLevel.Should().Be(LogLevel.Information);
        fakeLogger.LogEntries[0].FormattedMessage.Should().Contain("Starting");
        fakeLogger.LogEntries[1].LogLevel.Should().Be(LogLevel.Information);
        fakeLogger.LogEntries[1].FormattedMessage.Should().Contain("completed");
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulRetry_LogsDebugMessage()
    {
        FakeLogger<RetryFailedEmailsJob> fakeLogger = new();
        RetryFailedEmailsJob sut = new(_emailMessageRepository, _emailService, _timeProvider, fakeLogger);
        EmailMessage message = CreateFailedEmailMessage();
        IReadOnlyList<EmailMessage> failedMessages = new List<EmailMessage> { message };

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(failedMessages);

        await sut.ExecuteAsync(CancellationToken.None);

        fakeLogger.LogEntries.Should().Contain(e => e.LogLevel == LogLevel.Debug && e.FormattedMessage.Contains("succeeded"));
    }

    [Fact]
    public async Task ExecuteAsync_FailedRetry_LogsWarning()
    {
        FakeLogger<RetryFailedEmailsJob> fakeLogger = new();
        RetryFailedEmailsJob sut = new(_emailMessageRepository, _emailService, _timeProvider, fakeLogger);
        EmailMessage message = CreateFailedEmailMessage();
        IReadOnlyList<EmailMessage> failedMessages = new List<EmailMessage> { message };

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(failedMessages);

        _emailService
            .SendAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("SMTP error"));

        await sut.ExecuteAsync(CancellationToken.None);

        fakeLogger.LogEntries.Should().Contain(e => e.LogLevel == LogLevel.Warning && e.FormattedMessage.Contains("failed"));
    }

    [Fact]
    public async Task ExecuteAsync_RepositoryError_LogsError()
    {
        FakeLogger<RetryFailedEmailsJob> fakeLogger = new();
        RetryFailedEmailsJob sut = new(_emailMessageRepository, _emailService, _timeProvider, fakeLogger);

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<EmailMessage>>(_ => throw new InvalidOperationException("DB error"));

        Func<Task> act = () => sut.ExecuteAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        fakeLogger.LogEntries.Should().Contain(e => e.LogLevel == LogLevel.Error);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationTokenToRepository()
    {
        IReadOnlyList<EmailMessage> emptyList = new List<EmailMessage>();
        using CancellationTokenSource cts = new();
        CancellationToken token = cts.Token;

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(emptyList);

        await _sut.ExecuteAsync(token);

        await _emailMessageRepository.Received(1).GetFailedRetryableAsync(3, 100, token);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationTokenToSaveChanges()
    {
        IReadOnlyList<EmailMessage> emptyList = new List<EmailMessage>();
        using CancellationTokenSource cts = new();
        CancellationToken token = cts.Token;

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(emptyList);

        await _sut.ExecuteAsync(token);

        await _emailMessageRepository.Received(1).SaveChangesAsync(token);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationTokenToEmailService()
    {
        EmailMessage message = CreateFailedEmailMessage();
        IReadOnlyList<EmailMessage> failedMessages = new List<EmailMessage> { message };
        using CancellationTokenSource cts = new();
        CancellationToken token = cts.Token;

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(failedMessages);

        await _sut.ExecuteAsync(token);

        await _emailService.Received(1).SendAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            token);
    }

    [Fact]
    public async Task ExecuteAsync_SendsCorrectEmailContent()
    {
        EmailMessage message = CreateFailedEmailMessage();
        IReadOnlyList<EmailMessage> failedMessages = new List<EmailMessage> { message };

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(failedMessages);

        await _sut.ExecuteAsync(CancellationToken.None);

        await _emailService.Received(1).SendAsync(
            message.To.Value,
            message.From!.Value,
            message.Content.Subject,
            message.Content.Body,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ResetForRetry_CalledBeforeSend()
    {
        EmailMessage message = CreateFailedEmailMessage();
        EmailStatus statusDuringSend = EmailStatus.Failed;
        IReadOnlyList<EmailMessage> failedMessages = new List<EmailMessage> { message };

        _emailMessageRepository
            .GetFailedRetryableAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(failedMessages);

        _emailService
            .SendAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                statusDuringSend = message.Status;
                return Task.CompletedTask;
            });

        await _sut.ExecuteAsync(CancellationToken.None);

        statusDuringSend.Should().Be(EmailStatus.Pending);
    }

    private EmailMessage CreateFailedEmailMessage()
    {
        EmailAddress to = EmailAddress.Create($"test-{Guid.NewGuid():N}@example.com");
        EmailAddress from = EmailAddress.Create("noreply@wallow.dev");
        EmailContent content = EmailContent.Create("Test Subject", "Test Body");
        TenantId tenantId = TenantId.New();

        EmailMessage message = EmailMessage.Create(tenantId, to, from, content, _timeProvider);
        message.MarkAsFailed("Initial failure", _timeProvider);
        return message;
    }

    private sealed class FakeLogEntry
    {
        public LogLevel LogLevel { get; init; }
        public string FormattedMessage { get; init; } = string.Empty;
    }

    private sealed class FakeLogger<T> : ILogger<T>
    {
        public List<FakeLogEntry> LogEntries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LogEntries.Add(new FakeLogEntry
            {
                LogLevel = logLevel,
                FormattedMessage = formatter(state, exception)
            });
        }
    }
}
