using Foundry.Communications.Application.Channels.InApp.Interfaces;
using Foundry.Communications.Application.Messaging.EventHandlers;
using Foundry.Communications.Application.Messaging.Interfaces;
using Foundry.Communications.Domain.Messaging.Entities;
using Foundry.Communications.Domain.Messaging.Events;
using Foundry.Communications.Domain.Messaging.Identity;
using Foundry.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Foundry.Communications.Tests.Application.Messaging.EventHandlers;

public class MessageSentEventHandlerTests
{
    private readonly IConversationRepository _repository;
    private readonly INotificationService _notificationService;
    private readonly IMessageBus _bus;
    private readonly ILogger<MessageSentEventHandler> _logger;

    public MessageSentEventHandlerTests()
    {
        _repository = Substitute.For<IConversationRepository>();
        _notificationService = Substitute.For<INotificationService>();
        _bus = Substitute.For<IMessageBus>();
        _logger = Substitute.For<ILogger<MessageSentEventHandler>>();
    }

    [Fact]
    public async Task HandleAsync_WhenConversationNotFound_DoesNotSendNotifications()
    {
        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        MessageSentDomainEvent domainEvent = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await MessageSentEventHandler.HandleAsync(
            domainEvent, _repository, _notificationService, _bus, _logger, CancellationToken.None);

        await _notificationService.DidNotReceive().SendToUserAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithDirectConversation_NotifiesNonSenderParticipant()
    {
        Guid senderId = Guid.NewGuid();
        Guid recipientId = Guid.NewGuid();
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        Conversation conversation = Conversation.CreateDirect(tenantId, senderId, recipientId, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns(conversation);

        MessageSentDomainEvent domainEvent = new(conversation.Id.Value, Guid.NewGuid(), senderId, tenantId.Value);

        await MessageSentEventHandler.HandleAsync(
            domainEvent, _repository, _notificationService, _bus, _logger, CancellationToken.None);

        await _notificationService.Received(1).SendToUserAsync(
            recipientId, "New message", "You have a new message.", "Message",
            Arg.Any<CancellationToken>());

        await _notificationService.DidNotReceive().SendToUserAsync(
            senderId, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithGroupConversation_NotifiesAllNonSenderParticipants()
    {
        Guid senderId = Guid.NewGuid();
        Guid member1 = Guid.NewGuid();
        Guid member2 = Guid.NewGuid();
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        Conversation conversation = Conversation.CreateGroup(tenantId, senderId, "Team Chat", [member1, member2], TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns(conversation);

        MessageSentDomainEvent domainEvent = new(conversation.Id.Value, Guid.NewGuid(), senderId, tenantId.Value);

        await MessageSentEventHandler.HandleAsync(
            domainEvent, _repository, _notificationService, _bus, _logger, CancellationToken.None);

        await _notificationService.Received(1).SendToUserAsync(
            member1, "New message in Team Chat", "You have a new message.", "Message",
            Arg.Any<CancellationToken>());

        await _notificationService.Received(1).SendToUserAsync(
            member2, "New message in Team Chat", "You have a new message.", "Message",
            Arg.Any<CancellationToken>());

        await _notificationService.DidNotReceive().SendToUserAsync(
            senderId, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithGroupConversation_UsesSubjectInTitle()
    {
        Guid senderId = Guid.NewGuid();
        Guid recipientId = Guid.NewGuid();
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        Conversation conversation = Conversation.CreateGroup(tenantId, senderId, "Project Alpha", [recipientId], TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns(conversation);

        MessageSentDomainEvent domainEvent = new(conversation.Id.Value, Guid.NewGuid(), senderId, tenantId.Value);

        await MessageSentEventHandler.HandleAsync(
            domainEvent, _repository, _notificationService, _bus, _logger, CancellationToken.None);

        await _notificationService.Received(1).SendToUserAsync(
            recipientId, "New message in Project Alpha", "You have a new message.", "Message",
            Arg.Any<CancellationToken>());
    }
}
