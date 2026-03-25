using Wallow.Messaging.Application.Conversations.DTOs;
using Wallow.Messaging.Domain.Conversations.Entities;
using Wallow.Messaging.Infrastructure.Persistence;
using Wallow.Messaging.Infrastructure.Persistence.Repositories;
using Wallow.Messaging.Infrastructure.Services;
using Wallow.Shared.Infrastructure.Core.Persistence;
using Wallow.Shared.Kernel.Persistence;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Fixtures;

namespace Wallow.Messaging.Tests.Infrastructure.Services;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class MessagingQueryServiceTests(PostgresContainerFixture fixture)
    : DbContextIntegrationTestBase<MessagingDbContext>(fixture)
{
    protected override bool UseMigrateAsync => true;

    private EfMessagingQueryService CreateService()
    {
        IReadDbContext<MessagingDbContext> readDbContext = new ReadDbContext<MessagingDbContext>(DbContext);
        return new EfMessagingQueryService(readDbContext);
    }

    private ConversationRepository CreateRepository() => new(DbContext);

    [Fact]
    public async Task IsParticipantAsync_WhenUserIsActiveParticipant_ReturnsTrue()
    {
        Guid senderId = TestUserId;
        Guid recipientId = Guid.NewGuid();
        Conversation conversation = Conversation.CreateDirect(
            TestTenantId, senderId, recipientId, TimeProvider.System);
        ConversationRepository repository = CreateRepository();
        repository.Add(conversation);
        await repository.SaveChangesAsync();

        EfMessagingQueryService service = CreateService();

        bool result = await service.IsParticipantAsync(conversation.Id.Value, senderId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsParticipantAsync_WhenUserIsNotParticipant_ReturnsFalse()
    {
        Guid senderId = TestUserId;
        Conversation conversation = Conversation.CreateDirect(
            TestTenantId, senderId, Guid.NewGuid(), TimeProvider.System);
        ConversationRepository repository = CreateRepository();
        repository.Add(conversation);
        await repository.SaveChangesAsync();

        EfMessagingQueryService service = CreateService();

        bool result = await service.IsParticipantAsync(conversation.Id.Value, Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetUnreadConversationCountAsync_WhenUnreadMessagesExist_ReturnsOne()
    {
        Guid senderId = Guid.NewGuid();
        Guid recipientId = TestUserId;
        Conversation conversation = Conversation.CreateDirect(
            TestTenantId, senderId, recipientId, TimeProvider.System);
        ConversationRepository repository = CreateRepository();
        repository.Add(conversation);
        await repository.SaveChangesAsync();

        Conversation? loaded = await repository.GetByIdAsync(conversation.Id);
        loaded!.SendMessage(senderId, "Hello!", TimeProvider.System);
        loaded.SendMessage(senderId, "How are you?", TimeProvider.System);
        await repository.SaveChangesAsync();

        EfMessagingQueryService service = CreateService();

        int count = await service.GetUnreadConversationCountAsync(recipientId);

        count.Should().Be(1);
    }

    [Fact]
    public async Task GetUnreadConversationCountAsync_AfterMarkingRead_ReturnsZero()
    {
        Guid senderId = Guid.NewGuid();
        Guid recipientId = TestUserId;
        Conversation conversation = Conversation.CreateDirect(
            TestTenantId, senderId, recipientId, TimeProvider.System);
        ConversationRepository repository = CreateRepository();
        repository.Add(conversation);
        await repository.SaveChangesAsync();

        Conversation? loaded = await repository.GetByIdAsync(conversation.Id);
        loaded!.SendMessage(senderId, "Hello!", TimeProvider.System);
        await repository.SaveChangesAsync();

        // Simulate reading the conversation
        Conversation? toRead = await repository.GetByIdAsync(conversation.Id);
        toRead!.MarkReadBy(recipientId, TimeProvider.System);
        await repository.SaveChangesAsync();

        EfMessagingQueryService service = CreateService();

        int count = await service.GetUnreadConversationCountAsync(recipientId);

        count.Should().Be(0);
    }

    [Fact]
    public async Task GetMessagesAsync_WithoutCursor_ReturnsPageSizeMessagesInDescendingSentAtOrder()
    {
        Guid senderId = TestUserId;
        Conversation conversation = Conversation.CreateDirect(
            TestTenantId, senderId, Guid.NewGuid(), TimeProvider.System);
        ConversationRepository repository = CreateRepository();
        repository.Add(conversation);
        await repository.SaveChangesAsync();

        Conversation? loaded = await repository.GetByIdAsync(conversation.Id);
        loaded!.SendMessage(senderId, "Message 1", TimeProvider.System);
        loaded.SendMessage(senderId, "Message 2", TimeProvider.System);
        loaded.SendMessage(senderId, "Message 3", TimeProvider.System);
        await repository.SaveChangesAsync();

        EfMessagingQueryService service = CreateService();

        IReadOnlyList<MessageDto> messages = await service.GetMessagesAsync(
            conversation.Id.Value, senderId, null, 10);

        messages.Should().HaveCount(3);
        messages[0].SentAt.Should().BeOnOrAfter(messages[1].SentAt);
        messages[1].SentAt.Should().BeOnOrAfter(messages[2].SentAt);
    }

    [Fact]
    public async Task GetMessagesAsync_WithCursor_ReturnsOnlyMessagesOlderThanCursorMessage()
    {
        Guid senderId = TestUserId;
        Conversation conversation = Conversation.CreateDirect(
            TestTenantId, senderId, Guid.NewGuid(), TimeProvider.System);
        ConversationRepository repository = CreateRepository();
        repository.Add(conversation);
        await repository.SaveChangesAsync();

        Conversation? loaded = await repository.GetByIdAsync(conversation.Id);
        loaded!.SendMessage(senderId, "Message 1", TimeProvider.System);
        loaded.SendMessage(senderId, "Message 2", TimeProvider.System);
        loaded.SendMessage(senderId, "Message 3", TimeProvider.System);
        await repository.SaveChangesAsync();

        EfMessagingQueryService service = CreateService();

        IReadOnlyList<MessageDto> allMessages = await service.GetMessagesAsync(
            conversation.Id.Value, senderId, null, 50);

        // Use the second message (index 1, which is middle in desc order) as cursor
        Guid cursorId = allMessages[1].Id;
        DateTime cursorSentAt = allMessages[1].SentAt;

        IReadOnlyList<MessageDto> paged = await service.GetMessagesAsync(
            conversation.Id.Value, senderId, cursorId, 50);

        paged.Should().HaveCount(1);
        paged.Should().OnlyContain(m => m.SentAt < cursorSentAt);
    }

    [Fact]
    public async Task GetConversationsAsync_ReturnsConversationsInDescendingLastActivityOrder()
    {
        Guid userId = TestUserId;
        Conversation conversation1 = Conversation.CreateDirect(
            TestTenantId, userId, Guid.NewGuid(), TimeProvider.System);
        ConversationRepository repository = CreateRepository();
        repository.Add(conversation1);
        await repository.SaveChangesAsync();

        Conversation? loaded1 = await repository.GetByIdAsync(conversation1.Id);
        loaded1!.SendMessage(userId, "Old message", TimeProvider.System);
        await repository.SaveChangesAsync();

        Conversation conversation2 = Conversation.CreateDirect(
            TestTenantId, userId, Guid.NewGuid(), TimeProvider.System);
        repository.Add(conversation2);
        await repository.SaveChangesAsync();

        Conversation? loaded2 = await repository.GetByIdAsync(conversation2.Id);
        loaded2!.SendMessage(userId, "New message", TimeProvider.System);
        await repository.SaveChangesAsync();

        EfMessagingQueryService service = CreateService();

        IReadOnlyList<ConversationDto> conversations = await service.GetConversationsAsync(userId, 1, 20);

        conversations.Should().HaveCount(2);
        conversations[0].LastActivityAt.Should().BeOnOrAfter(conversations[1].LastActivityAt);
    }

    [Fact]
    public async Task GetConversationsAsync_PopulatesParticipantLists()
    {
        Guid userId = TestUserId;
        Guid otherId = Guid.NewGuid();
        Conversation conversation = Conversation.CreateDirect(
            TestTenantId, userId, otherId, TimeProvider.System);
        ConversationRepository repository = CreateRepository();
        repository.Add(conversation);
        await repository.SaveChangesAsync();

        EfMessagingQueryService service = CreateService();

        IReadOnlyList<ConversationDto> conversations = await service.GetConversationsAsync(userId, 1, 20);

        conversations.Should().HaveCount(1);
        conversations[0].Participants.Should().HaveCount(2);
        conversations[0].Participants.Should().Contain(p => p.UserId == userId);
        conversations[0].Participants.Should().Contain(p => p.UserId == otherId);
    }

    [Fact]
    public async Task GetConversationsAsync_PaginationSkipsCorrectly()
    {
        Guid userId = TestUserId;
        Conversation conversation1 = Conversation.CreateDirect(
            TestTenantId, userId, Guid.NewGuid(), TimeProvider.System);
        Conversation conversation2 = Conversation.CreateDirect(
            TestTenantId, userId, Guid.NewGuid(), TimeProvider.System);
        Conversation conversation3 = Conversation.CreateDirect(
            TestTenantId, userId, Guid.NewGuid(), TimeProvider.System);
        ConversationRepository repository = CreateRepository();
        repository.Add(conversation1);
        repository.Add(conversation2);
        repository.Add(conversation3);
        await repository.SaveChangesAsync();

        EfMessagingQueryService service = CreateService();

        IReadOnlyList<ConversationDto> page1 = await service.GetConversationsAsync(userId, 1, 2);
        IReadOnlyList<ConversationDto> page2 = await service.GetConversationsAsync(userId, 2, 2);

        page1.Should().HaveCount(2);
        page2.Should().HaveCount(1);
        page1.Select(c => c.Id).Should().NotIntersectWith(page2.Select(c => c.Id));
    }
}
