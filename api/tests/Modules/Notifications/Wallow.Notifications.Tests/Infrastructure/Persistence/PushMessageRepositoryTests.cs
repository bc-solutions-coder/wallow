using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Notifications.Domain.Channels.Push.Identity;
using Wallow.Notifications.Infrastructure.Persistence.Repositories;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Tests.Infrastructure.Persistence;

public sealed class PushMessageRepositoryTests : RepositoryTestBase
{
    private readonly PushMessageRepository _repository;

    public PushMessageRepositoryTests()
    {
        _repository = new PushMessageRepository(Context);
    }

    private PushMessage CreatePushMessage(string title = "Test Push", string body = "Push body")
    {
        PushMessage message = PushMessage.Create(
            TestTenantId,
            UserId.New(),
            title,
            body,
            TimeProvider.System);
        message.ClearDomainEvents();
        return message;
    }

    [Fact]
    public async Task Add_And_GetByIdAsync_ReturnsPushMessage()
    {
        PushMessage message = CreatePushMessage(title: "Hello Push");

        _repository.Add(message);
        await Context.SaveChangesAsync();

        PushMessage? result = await _repository.GetByIdAsync(message.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Hello Push");
        result.Body.Should().Be("Push body");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        PushMessage? result = await _repository.GetByIdAsync(PushMessageId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task Update_PersistsChanges()
    {
        PushMessage message = CreatePushMessage();
        _repository.Add(message);
        await Context.SaveChangesAsync();

        message.MarkDelivered(TimeProvider.System);
        message.ClearDomainEvents();
        _repository.Update(message);
        await Context.SaveChangesAsync();

        PushMessage? result = await _repository.GetByIdAsync(message.Id);
        result.Should().NotBeNull();
        result!.Status.Should().Be(PushStatus.Delivered);
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsChanges()
    {
        PushMessage message = CreatePushMessage();
        _repository.Add(message);

        await _repository.SaveChangesAsync();

        PushMessage? result = await _repository.GetByIdAsync(message.Id);
        result.Should().NotBeNull();
    }
}
