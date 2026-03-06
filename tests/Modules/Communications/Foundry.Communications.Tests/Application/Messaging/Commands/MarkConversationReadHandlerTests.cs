using Foundry.Communications.Application.Messaging.Commands.MarkConversationRead;
using Foundry.Communications.Application.Messaging.Interfaces;
using Foundry.Communications.Domain.Messaging.Entities;
using Foundry.Communications.Domain.Messaging.Identity;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Messaging.Commands;

public class MarkConversationReadHandlerTests
{
    private readonly IConversationRepository _repository;
    private readonly IMessagingQueryService _messagingQueryService;
    private readonly MarkConversationReadHandler _handler;

    public MarkConversationReadHandlerTests()
    {
        _repository = Substitute.For<IConversationRepository>();
        _messagingQueryService = Substitute.For<IMessagingQueryService>();
        _handler = new MarkConversationReadHandler(_repository, _messagingQueryService, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WhenConversationExists_MarksReadAndReturnsSuccess()
    {
        Guid userId = Guid.NewGuid();
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        Conversation conversation = Conversation.CreateDirect(tenantId, userId, Guid.NewGuid(), TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns(conversation);
        _messagingQueryService.IsParticipantAsync(Arg.Any<Guid>(), userId, Arg.Any<CancellationToken>())
            .Returns(true);

        MarkConversationReadCommand command = new(conversation.Id.Value, userId);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenConversationNotFound_ReturnsNotFoundFailure()
    {
        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        MarkConversationReadCommand command = new(Guid.NewGuid(), Guid.NewGuid());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenConversationExists_CallsMarkReadByWithCorrectUserId()
    {
        Guid userId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        Conversation conversation = Conversation.CreateDirect(tenantId, userId, otherUserId, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns(conversation);
        _messagingQueryService.IsParticipantAsync(Arg.Any<Guid>(), userId, Arg.Any<CancellationToken>())
            .Returns(true);

        MarkConversationReadCommand command = new(conversation.Id.Value, userId);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Participant participant = conversation.Participants.First(p => p.UserId == userId);
        participant.LastReadAt.Should().NotBeNull();
    }
}
