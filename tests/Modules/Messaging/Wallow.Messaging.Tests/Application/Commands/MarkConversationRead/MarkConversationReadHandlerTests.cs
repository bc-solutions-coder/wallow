using Wallow.Messaging.Application.Conversations.Commands.MarkConversationRead;
using Wallow.Messaging.Application.Conversations.Interfaces;
using Wallow.Messaging.Domain.Conversations.Entities;
using Wallow.Messaging.Domain.Conversations.Identity;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Messaging.Tests.Application.Commands.MarkConversationRead;

public class MarkConversationReadHandlerTests
{
    private readonly IConversationRepository _repository = Substitute.For<IConversationRepository>();
    private readonly IMessagingQueryService _queryService = Substitute.For<IMessagingQueryService>();

    [Fact]
    public async Task Handle_WhenConversationNotFound_ReturnsNotFoundError()
    {
        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        MarkConversationReadHandler handler = new(_repository, _queryService, TimeProvider.System);
        MarkConversationReadCommand command = new(Guid.NewGuid(), Guid.NewGuid());

        Result result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_WhenUserIsNotParticipant_ReturnsUnauthorizedError()
    {
        Guid userId = Guid.NewGuid();
        Conversation conversation = Conversation.CreateDirect(
            TenantId.New(), Guid.NewGuid(), Guid.NewGuid(), TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns(conversation);
        _queryService.IsParticipantAsync(Arg.Any<Guid>(), userId, Arg.Any<CancellationToken>())
            .Returns(false);

        MarkConversationReadHandler handler = new(_repository, _queryService, TimeProvider.System);
        MarkConversationReadCommand command = new(conversation.Id.Value, userId);

        Result result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task Handle_WhenUserIsParticipant_ReturnsSuccess()
    {
        Guid initiatorId = Guid.NewGuid();
        Conversation conversation = Conversation.CreateDirect(
            TenantId.New(), initiatorId, Guid.NewGuid(), TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns(conversation);
        _queryService.IsParticipantAsync(Arg.Any<Guid>(), initiatorId, Arg.Any<CancellationToken>())
            .Returns(true);

        MarkConversationReadHandler handler = new(_repository, _queryService, TimeProvider.System);
        MarkConversationReadCommand command = new(conversation.Id.Value, initiatorId);

        Result result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
