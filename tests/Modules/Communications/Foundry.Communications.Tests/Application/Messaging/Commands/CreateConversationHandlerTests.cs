using Foundry.Communications.Application.Messaging.Commands.CreateConversation;
using Foundry.Communications.Application.Messaging.Interfaces;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;
using Conversation = Foundry.Communications.Domain.Messaging.Entities.Conversation;

namespace Foundry.Communications.Tests.Application.Messaging.Commands;

public class CreateConversationHandlerTests
{
    private readonly IConversationRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly CreateConversationHandler _handler;

    public CreateConversationHandlerTests()
    {
        _repository = Substitute.For<IConversationRepository>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(TenantId.Create(Guid.NewGuid()));
        _handler = new CreateConversationHandler(_repository, _tenantContext);
    }

    [Fact]
    public async Task Handle_DirectConversation_ReturnsSuccessWithGuid()
    {
        Guid initiatorId = Guid.NewGuid();
        Guid recipientId = Guid.NewGuid();
        CreateConversationCommand command = new(initiatorId, recipientId, null, "Direct", null);

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_DirectConversation_AddsToRepositoryAndSaves()
    {
        CreateConversationCommand command = new(Guid.NewGuid(), Guid.NewGuid(), null, "Direct", null);

        await _handler.Handle(command, CancellationToken.None);

        _repository.Received(1).Add(Arg.Is<Conversation>(c =>
            !c.IsGroup &&
            c.Participants.Count == 2));
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GroupConversation_ReturnsSuccessWithGuid()
    {
        Guid creatorId = Guid.NewGuid();
        List<Guid> memberIds = [Guid.NewGuid(), Guid.NewGuid()];
        CreateConversationCommand command = new(creatorId, null, memberIds, "Group", "Team Chat");

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_GroupConversation_AddsToRepositoryWithCorrectParticipants()
    {
        Guid creatorId = Guid.NewGuid();
        List<Guid> memberIds = [Guid.NewGuid(), Guid.NewGuid()];
        CreateConversationCommand command = new(creatorId, null, memberIds, "Group", "Team Chat");

        await _handler.Handle(command, CancellationToken.None);

        _repository.Received(1).Add(Arg.Is<Conversation>(c =>
            c.IsGroup &&
            c.Participants.Count == 3 &&
            c.Subject == "Team Chat"));
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        CreateConversationCommand command = new(Guid.NewGuid(), Guid.NewGuid(), null, "Direct", null);

        await _handler.Handle(command, cts.Token);

        await _repository.Received(1).SaveChangesAsync(cts.Token);
    }
}
