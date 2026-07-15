using Wallow.Notifications.Domain.Channels.Email.Entities;
using Wallow.Notifications.Domain.Channels.Email.Enums;
using Wallow.Notifications.Domain.Channels.Email.ValueObjects;
using Wallow.Notifications.Infrastructure.Persistence.Repositories;

namespace Wallow.Notifications.Tests.Infrastructure.Persistence;

public sealed class EmailMessageRepositoryTests : RepositoryTestBase
{
    private readonly EmailMessageRepository _repository;

    public EmailMessageRepositoryTests()
    {
        _repository = new EmailMessageRepository(Context);
    }

    private EmailMessage CreateEmailMessage(EmailStatus? status = null)
    {
        EmailMessage message = EmailMessage.Create(
            TestTenantId,
            EmailAddress.Create("to@example.com"),
            EmailAddress.Create("from@example.com"),
            EmailContent.Create("Subject", "Body"),
            TimeProvider.System);
        message.ClearDomainEvents();

        if (status == EmailStatus.Failed)
        {
            message.MarkAsFailed("test failure", TimeProvider.System);
            message.ClearDomainEvents();
        }
        else if (status == EmailStatus.Sent)
        {
            message.MarkAsSent(TimeProvider.System);
            message.ClearDomainEvents();
        }

        return message;
    }

    [Fact]
    public async Task Add_PersistsEmailMessage()
    {
        EmailMessage message = CreateEmailMessage();

        _repository.Add(message);
        await Context.SaveChangesAsync();

        EmailMessage? result = await Context.EmailMessages.FindAsync(message.Id);
        result.Should().NotBeNull();
        result!.Status.Should().Be(EmailStatus.Pending);
    }

    [Fact]
    public async Task GetPendingAsync_ReturnsOnlyPendingMessages()
    {
        _repository.Add(CreateEmailMessage());
        _repository.Add(CreateEmailMessage());
        _repository.Add(CreateEmailMessage(status: EmailStatus.Sent));
        _repository.Add(CreateEmailMessage(status: EmailStatus.Failed));
        await Context.SaveChangesAsync();

        IReadOnlyList<EmailMessage> result = await _repository.GetPendingAsync(10);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(m => m.Status == EmailStatus.Pending);
    }

    [Fact]
    public async Task GetPendingAsync_RespectsLimit()
    {
        for (int i = 0; i < 5; i++)
        {
            _repository.Add(CreateEmailMessage());
        }

        await Context.SaveChangesAsync();

        IReadOnlyList<EmailMessage> result = await _repository.GetPendingAsync(3);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetPendingAsync_WhenNoPending_ReturnsEmpty()
    {
        _repository.Add(CreateEmailMessage(status: EmailStatus.Sent));
        await Context.SaveChangesAsync();

        IReadOnlyList<EmailMessage> result = await _repository.GetPendingAsync(10);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFailedRetryableAsync_ReturnsFailedUnderMaxRetries()
    {
        EmailMessage failed = CreateEmailMessage(status: EmailStatus.Failed);
        _repository.Add(failed);
        _repository.Add(CreateEmailMessage()); // pending, should be excluded
        _repository.Add(CreateEmailMessage(status: EmailStatus.Sent)); // sent, should be excluded
        await Context.SaveChangesAsync();

        IReadOnlyList<EmailMessage> result = await _repository.GetFailedRetryableAsync(maxRetries: 3, limit: 10);

        result.Should().HaveCount(1);
        result[0].Status.Should().Be(EmailStatus.Failed);
    }

    [Fact]
    public async Task GetFailedRetryableAsync_ExcludesMessagesAtMaxRetries()
    {
        EmailMessage message = CreateEmailMessage();
        // Fail 3 times to reach maxRetries
        message.MarkAsFailed("fail1", TimeProvider.System);
        message.MarkAsFailed("fail2", TimeProvider.System);
        message.MarkAsFailed("fail3", TimeProvider.System);
        message.ClearDomainEvents();
        _repository.Add(message);
        await Context.SaveChangesAsync();

        IReadOnlyList<EmailMessage> result = await _repository.GetFailedRetryableAsync(maxRetries: 3, limit: 10);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFailedRetryableAsync_RespectsLimit()
    {
        for (int i = 0; i < 5; i++)
        {
            _repository.Add(CreateEmailMessage(status: EmailStatus.Failed));
        }

        await Context.SaveChangesAsync();

        IReadOnlyList<EmailMessage> result = await _repository.GetFailedRetryableAsync(maxRetries: 3, limit: 2);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsChanges()
    {
        EmailMessage message = CreateEmailMessage();
        _repository.Add(message);

        await _repository.SaveChangesAsync();

        EmailMessage? result = await Context.EmailMessages.FindAsync(message.Id);
        result.Should().NotBeNull();
    }
}
