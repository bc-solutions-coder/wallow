using Wallow.Notifications.Domain.Channels.Sms.Entities;
using Wallow.Notifications.Domain.Channels.Sms.Enums;
using Wallow.Notifications.Domain.Channels.Sms.ValueObjects;
using Wallow.Notifications.Infrastructure.Persistence.Repositories;

namespace Wallow.Notifications.Tests.Infrastructure.Persistence;

public sealed class SmsMessageRepositoryTests : RepositoryTestBase
{
    private readonly SmsMessageRepository _repository;

    public SmsMessageRepositoryTests()
    {
        _repository = new SmsMessageRepository(Context);
    }

    private SmsMessage CreateSmsMessage(SmsStatus? status = null)
    {
        SmsMessage message = SmsMessage.Create(
            TestTenantId,
            PhoneNumber.Create("+15551234567"),
            PhoneNumber.Create("+15559876543"),
            "Test SMS body",
            TimeProvider.System);
        message.ClearDomainEvents();

        if (status == SmsStatus.Sent)
        {
            message.MarkAsSent(TimeProvider.System);
            message.ClearDomainEvents();
        }
        else if (status == SmsStatus.Failed)
        {
            message.MarkAsFailed("test failure", TimeProvider.System);
            message.ClearDomainEvents();
        }

        return message;
    }

    [Fact]
    public async Task Add_PersistsSmsMessage()
    {
        SmsMessage message = CreateSmsMessage();

        _repository.Add(message);
        await Context.SaveChangesAsync();

        SmsMessage? result = await Context.SmsMessages.FindAsync(message.Id);
        result.Should().NotBeNull();
        result!.Status.Should().Be(SmsStatus.Pending);
    }

    [Fact]
    public async Task GetPendingAsync_ReturnsOnlyPendingMessages()
    {
        _repository.Add(CreateSmsMessage());
        _repository.Add(CreateSmsMessage());
        _repository.Add(CreateSmsMessage(status: SmsStatus.Sent));
        _repository.Add(CreateSmsMessage(status: SmsStatus.Failed));
        await Context.SaveChangesAsync();

        IReadOnlyList<SmsMessage> result = await _repository.GetPendingAsync(10);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(m => m.Status == SmsStatus.Pending);
    }

    [Fact]
    public async Task GetPendingAsync_RespectsLimit()
    {
        for (int i = 0; i < 5; i++)
        {
            _repository.Add(CreateSmsMessage());
        }

        await Context.SaveChangesAsync();

        IReadOnlyList<SmsMessage> result = await _repository.GetPendingAsync(3);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetPendingAsync_WhenNoPending_ReturnsEmpty()
    {
        _repository.Add(CreateSmsMessage(status: SmsStatus.Sent));
        await Context.SaveChangesAsync();

        IReadOnlyList<SmsMessage> result = await _repository.GetPendingAsync(10);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsChanges()
    {
        SmsMessage message = CreateSmsMessage();
        _repository.Add(message);

        await _repository.SaveChangesAsync();

        SmsMessage? result = await Context.SmsMessages.FindAsync(message.Id);
        result.Should().NotBeNull();
    }
}
