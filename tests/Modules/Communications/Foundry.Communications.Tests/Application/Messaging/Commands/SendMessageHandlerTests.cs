using Foundry.Communications.Application.Messaging.Commands.SendMessage;
using Foundry.Communications.Application.Messaging.Interfaces;
using Foundry.Communications.Domain.Messaging.Entities;
using Foundry.Communications.Domain.Messaging.Identity;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Messaging.Commands;

public class SendMessageHandlerTests
{
    private readonly IConversationRepository _repository;
    private readonly SendMessageHandler _handler;

    public SendMessageHandlerTests()
    {
        _repository = Substitute.For<IConversationRepository>();
        _handler = new SendMessageHandler(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessWithMessageId()
    {
        Guid senderId = Guid.NewGuid();
        TenantId tenantId = TenantId.New();
        Conversation conversation = Conversation.CreateDirect(tenantId, senderId, Guid.NewGuid(), TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns(conversation);

        SendMessageCommand command = new(conversation.Id.Value, senderId, "Hello world");

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ValidCommand_SavesChanges()
    {
        Guid senderId = Guid.NewGuid();
        TenantId tenantId = TenantId.New();
        Conversation conversation = Conversation.CreateDirect(tenantId, senderId, Guid.NewGuid(), TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns(conversation);

        SendMessageCommand command = new(conversation.Id.Value, senderId, "Hello world");

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConversationNotFound_ReturnsFailure()
    {
        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        SendMessageCommand command = new(Guid.NewGuid(), Guid.NewGuid(), "Hello");

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Conversation.NotFound");
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        Guid senderId = Guid.NewGuid();
        TenantId tenantId = TenantId.New();
        Conversation conversation = Conversation.CreateDirect(tenantId, senderId, Guid.NewGuid(), TimeProvider.System);
        using CancellationTokenSource cts = new();
        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns(conversation);

        SendMessageCommand command = new(conversation.Id.Value, senderId, "Hello");

        await _handler.Handle(command, cts.Token);

        await _repository.Received(1).SaveChangesAsync(cts.Token);
    }
}
