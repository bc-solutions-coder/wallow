using Foundry.Messaging.Application.Conversations.Commands.SendMessage;
using Foundry.Messaging.Application.Conversations.Interfaces;
using Foundry.Messaging.Domain.Conversations.Entities;
using Foundry.Messaging.Domain.Conversations.Identity;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Messaging.Tests.Application.Commands.SendMessage;

public class SendMessageHandlerTests
{
    private readonly IConversationRepository _repository = Substitute.For<IConversationRepository>();

    [Fact]
    public async Task Handle_WithValidConversation_ReturnsSuccessWithMessageId()
    {
        Guid initiatorId = Guid.NewGuid();
        Conversation conversation = Conversation.CreateDirect(
            TenantId.New(), initiatorId, Guid.NewGuid(), TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns(conversation);

        SendMessageHandler handler = new(_repository, TimeProvider.System);
        SendMessageCommand command = new(conversation.Id.Value, initiatorId, "Hello!");

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenConversationNotFound_ReturnsNotFoundError()
    {
        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        SendMessageHandler handler = new(_repository, TimeProvider.System);
        SendMessageCommand command = new(Guid.NewGuid(), Guid.NewGuid(), "Hello!");

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("NotFound");
    }
}
