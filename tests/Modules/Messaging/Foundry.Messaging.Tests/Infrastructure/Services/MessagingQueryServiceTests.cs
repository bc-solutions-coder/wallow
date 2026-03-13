using Foundry.Messaging.Application.Conversations.DTOs;
using Foundry.Messaging.Domain.Conversations.Entities;
using Foundry.Messaging.Infrastructure.Persistence;
using Foundry.Messaging.Infrastructure.Persistence.Repositories;
using Foundry.Messaging.Infrastructure.Services;
using Foundry.Tests.Common.Bases;
using Foundry.Tests.Common.Fixtures;

namespace Foundry.Messaging.Tests.Infrastructure.Services;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class MessagingQueryServiceTests(PostgresContainerFixture fixture)
    : DbContextIntegrationTestBase<MessagingDbContext>(fixture)
{
    protected override bool UseMigrateAsync => true;

    private MessagingQueryService CreateService() =>
        new(DbContext, TenantContext);

    private ConversationRepository CreateRepository() => new(DbContext);

    [Fact]
    public async Task IsParticipantAsync_WhenUserIsParticipant_ReturnsTrue()
    {
        Guid senderId = TestUserId;
        Guid recipientId = Guid.NewGuid();
        Conversation conversation = Conversation.CreateDirect(
            TestTenantId, senderId, recipientId, TimeProvider.System);
        ConversationRepository repository = CreateRepository();
        repository.Add(conversation);
        await repository.SaveChangesAsync();

        MessagingQueryService service = CreateService();

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

        MessagingQueryService service = CreateService();

        bool result = await service.IsParticipantAsync(conversation.Id.Value, Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsParticipantAsync_WhenConversationDoesNotExist_ReturnsFalse()
    {
        MessagingQueryService service = CreateService();

        bool result = await service.IsParticipantAsync(Guid.NewGuid(), TestUserId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetUnreadConversationCountAsync_WhenNoMessages_ReturnsZero()
    {
        Guid userId = TestUserId;
        Conversation conversation = Conversation.CreateDirect(
            TestTenantId, userId, Guid.NewGuid(), TimeProvider.System);
        ConversationRepository repository = CreateRepository();
        repository.Add(conversation);
        await repository.SaveChangesAsync();

        MessagingQueryService service = CreateService();

        int count = await service.GetUnreadConversationCountAsync(userId);

        count.Should().Be(0);
    }

    [Fact]
    public async Task GetUnreadConversationCountAsync_WhenUserHasUnreadMessages_ReturnsCount()
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

        MessagingQueryService service = CreateService();

        int count = await service.GetUnreadConversationCountAsync(recipientId);

        count.Should().Be(1);
    }

    [Fact]
    public async Task GetMessagesAsync_WhenConversationHasMessages_ReturnsMessages()
    {
        Guid senderId = TestUserId;
        Conversation conversation = Conversation.CreateDirect(
            TestTenantId, senderId, Guid.NewGuid(), TimeProvider.System);
        ConversationRepository repository = CreateRepository();
        repository.Add(conversation);
        await repository.SaveChangesAsync();

        Conversation? loaded = await repository.GetByIdAsync(conversation.Id);
        loaded!.SendMessage(senderId, "Hello!", TimeProvider.System);
        loaded.SendMessage(senderId, "World!", TimeProvider.System);
        await repository.SaveChangesAsync();

        MessagingQueryService service = CreateService();

        IReadOnlyList<MessageDto> messages = await service.GetMessagesAsync(
            conversation.Id.Value, senderId, null, 50);

        messages.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetMessagesAsync_WithCursor_ReturnsPaginatedMessages()
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

        MessagingQueryService service = CreateService();

        IReadOnlyList<MessageDto> allMessages = await service.GetMessagesAsync(
            conversation.Id.Value, senderId, null, 50);

        Guid cursorId = allMessages[1].Id;
        IReadOnlyList<MessageDto> paged = await service.GetMessagesAsync(
            conversation.Id.Value, senderId, cursorId, 50);

        paged.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetMessagesAsync_WhenNoMessages_ReturnsEmpty()
    {
        Guid senderId = TestUserId;
        Conversation conversation = Conversation.CreateDirect(
            TestTenantId, senderId, Guid.NewGuid(), TimeProvider.System);
        ConversationRepository repository = CreateRepository();
        repository.Add(conversation);
        await repository.SaveChangesAsync();

        MessagingQueryService service = CreateService();

        IReadOnlyList<MessageDto> messages = await service.GetMessagesAsync(
            conversation.Id.Value, senderId, null, 50);

        messages.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConversationsAsync_WhenUserHasConversations_ReturnsConversations()
    {
        Guid userId = TestUserId;
        Conversation conversation = Conversation.CreateDirect(
            TestTenantId, userId, Guid.NewGuid(), TimeProvider.System);
        ConversationRepository repository = CreateRepository();
        repository.Add(conversation);
        await repository.SaveChangesAsync();

        MessagingQueryService service = CreateService();

        IReadOnlyList<ConversationDto> conversations = await service.GetConversationsAsync(
            userId, 1, 20);

        conversations.Should().HaveCount(1);
        conversations[0].Participants.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task GetConversationsAsync_WhenUserHasNoConversations_ReturnsEmpty()
    {
        MessagingQueryService service = CreateService();

        IReadOnlyList<ConversationDto> conversations = await service.GetConversationsAsync(
            Guid.NewGuid(), 1, 20);

        conversations.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConversationsAsync_ReturnsGroupConversationWithCorrectType()
    {
        Guid userId = TestUserId;
        Conversation conversation = Conversation.CreateGroup(
            TestTenantId, userId, "My Group", [Guid.NewGuid(), Guid.NewGuid()], TimeProvider.System);
        ConversationRepository repository = CreateRepository();
        repository.Add(conversation);
        await repository.SaveChangesAsync();

        MessagingQueryService service = CreateService();

        IReadOnlyList<ConversationDto> conversations = await service.GetConversationsAsync(
            userId, 1, 20);

        conversations.Should().HaveCount(1);
        conversations[0].Type.Should().Be("Group");
    }

    [Fact]
    public async Task GetConversationsAsync_WithPagination_ReturnsCorrectPage()
    {
        Guid userId = TestUserId;
        Conversation conversation1 = Conversation.CreateDirect(
            TestTenantId, userId, Guid.NewGuid(), TimeProvider.System);
        Conversation conversation2 = Conversation.CreateDirect(
            TestTenantId, userId, Guid.NewGuid(), TimeProvider.System);
        ConversationRepository repository = CreateRepository();
        repository.Add(conversation1);
        repository.Add(conversation2);
        await repository.SaveChangesAsync();

        MessagingQueryService service = CreateService();

        IReadOnlyList<ConversationDto> page1 = await service.GetConversationsAsync(userId, 1, 1);
        IReadOnlyList<ConversationDto> page2 = await service.GetConversationsAsync(userId, 2, 1);

        page1.Should().HaveCount(1);
        page2.Should().HaveCount(1);
        page1[0].Id.Should().NotBe(page2[0].Id);
    }

    [Fact]
    public async Task GetConversationsAsync_WithLastMessage_PopulatesLastMessageData()
    {
        Guid userId = TestUserId;
        Conversation conversation = Conversation.CreateDirect(
            TestTenantId, userId, Guid.NewGuid(), TimeProvider.System);
        ConversationRepository repository = CreateRepository();
        repository.Add(conversation);
        await repository.SaveChangesAsync();

        Conversation? loaded = await repository.GetByIdAsync(conversation.Id);
        loaded!.SendMessage(userId, "Hi there!", TimeProvider.System);
        await repository.SaveChangesAsync();

        MessagingQueryService service = CreateService();

        IReadOnlyList<ConversationDto> conversations = await service.GetConversationsAsync(userId, 1, 20);

        conversations.Should().HaveCount(1);
        conversations[0].LastMessage.Should().NotBeNull();
        conversations[0].LastMessage!.Body.Should().Be("Hi there!");
    }

    [Fact]
    public async Task GetConversationsAsync_UnreadCount_IsCorrect()
    {
        Guid otherId = Guid.NewGuid();
        Guid userId = TestUserId;
        Conversation conversation = Conversation.CreateDirect(
            TestTenantId, otherId, userId, TimeProvider.System);
        ConversationRepository repository = CreateRepository();
        repository.Add(conversation);
        await repository.SaveChangesAsync();

        Conversation? loaded = await repository.GetByIdAsync(conversation.Id);
        loaded!.SendMessage(otherId, "Unread message", TimeProvider.System);
        await repository.SaveChangesAsync();

        MessagingQueryService service = CreateService();

        IReadOnlyList<ConversationDto> conversations = await service.GetConversationsAsync(userId, 1, 20);

        conversations.Should().HaveCount(1);
        conversations[0].UnreadCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetConversationsAsync_ParticipantsArePopulated()
    {
        Guid userId = TestUserId;
        Guid otherId = Guid.NewGuid();
        Conversation conversation = Conversation.CreateDirect(
            TestTenantId, userId, otherId, TimeProvider.System);
        ConversationRepository repository = CreateRepository();
        repository.Add(conversation);
        await repository.SaveChangesAsync();

        MessagingQueryService service = CreateService();

        IReadOnlyList<ConversationDto> conversations = await service.GetConversationsAsync(userId, 1, 20);

        conversations[0].Participants.Should().HaveCount(2);
        ParticipantDto participant = conversations[0].Participants.First(p => p.UserId == userId);
        participant.IsActive.Should().BeTrue();
        participant.JoinedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
