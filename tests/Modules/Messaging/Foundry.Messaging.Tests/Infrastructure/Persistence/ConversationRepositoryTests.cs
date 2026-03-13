using Foundry.Messaging.Domain.Conversations.Entities;
using Foundry.Messaging.Domain.Conversations.Identity;
using Foundry.Messaging.Infrastructure.Persistence;
using Foundry.Messaging.Infrastructure.Persistence.Repositories;
using Foundry.Shared.Kernel.Identity;
using Foundry.Tests.Common.Bases;
using Foundry.Tests.Common.Fixtures;

namespace Foundry.Messaging.Tests.Infrastructure.Persistence;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class ConversationRepositoryTests(PostgresContainerFixture fixture)
    : DbContextIntegrationTestBase<MessagingDbContext>(fixture)
{
    protected override bool UseMigrateAsync => true;

    private ConversationRepository CreateRepository() => new(DbContext);

    [Fact]
    public async Task Add_AndGetByIdAsync_ReturnsConversationWithParticipants()
    {
        ConversationRepository repository = CreateRepository();
        Conversation conversation = Conversation.CreateDirect(
            TestTenantId, TestUserId, Guid.NewGuid(), TimeProvider.System);

        repository.Add(conversation);
        await repository.SaveChangesAsync();

        Conversation? result = await repository.GetByIdAsync(conversation.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(conversation.Id);
        result.Participants.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        ConversationRepository repository = CreateRepository();

        Conversation? result = await repository.GetByIdAsync(ConversationId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task Add_GroupConversation_PersistsAllParticipants()
    {
        ConversationRepository repository = CreateRepository();
        Guid member1 = Guid.NewGuid();
        Guid member2 = Guid.NewGuid();
        Conversation conversation = Conversation.CreateGroup(
            TestTenantId, TestUserId, "Test Group", [member1, member2], TimeProvider.System);

        repository.Add(conversation);
        await repository.SaveChangesAsync();

        Conversation? result = await repository.GetByIdAsync(conversation.Id);

        result.Should().NotBeNull();
        result!.IsGroup.Should().BeTrue();
        result.Participants.Should().HaveCount(3);
    }

    [Fact]
    public async Task SaveChangesAsync_AfterSendMessage_PersistsMessage()
    {
        ConversationRepository repository = CreateRepository();
        Conversation conversation = Conversation.CreateDirect(
            TestTenantId, TestUserId, Guid.NewGuid(), TimeProvider.System);

        repository.Add(conversation);
        await repository.SaveChangesAsync();

        Conversation? loaded = await repository.GetByIdAsync(conversation.Id);
        loaded!.SendMessage(TestUserId, "Hello!", TimeProvider.System);
        await repository.SaveChangesAsync();

        Conversation? result = await repository.GetByIdAsync(conversation.Id);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithDifferentTenant_ReturnsNull()
    {
        ConversationRepository repository = CreateRepository();
        Conversation conversation = Conversation.CreateDirect(
            TestTenantId, TestUserId, Guid.NewGuid(), TimeProvider.System);

        repository.Add(conversation);
        await repository.SaveChangesAsync();

        MessagingDbContext otherContext = CreateDbContextForTenant(TenantId.New());
        ConversationRepository otherRepository = new(otherContext);

        Conversation? result = await otherRepository.GetByIdAsync(conversation.Id);

        result.Should().BeNull();
    }
}
