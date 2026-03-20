using Wallow.Messaging.Application.Conversations.Commands.CreateConversation;
using Wallow.Messaging.Application.Conversations.Interfaces;
using Wallow.Messaging.Domain.Conversations.Entities;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Messaging.Tests.Application.Commands.CreateConversation;

public class CreateConversationHandlerTests
{
    private readonly IConversationRepository _repository = Substitute.For<IConversationRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    public CreateConversationHandlerTests()
    {
        _tenantContext.TenantId.Returns(TenantId.New());
    }

    [Fact]
    public async Task Handle_DirectConversation_ReturnsSuccessWithGuid()
    {
        CreateConversationHandler handler = new(_repository, _tenantContext, TimeProvider.System);
        CreateConversationCommand command = new(Guid.NewGuid(), Guid.NewGuid(), null, "Direct", null);

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        _repository.Received(1).Add(Arg.Any<Conversation>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GroupConversation_ReturnsSuccessWithGuid()
    {
        CreateConversationHandler handler = new(_repository, _tenantContext, TimeProvider.System);
        List<Guid> memberIds = [Guid.NewGuid(), Guid.NewGuid()];
        CreateConversationCommand command = new(Guid.NewGuid(), null, memberIds, "Group", "Test Group");

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        _repository.Received(1).Add(Arg.Any<Conversation>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownType_ThrowsArgumentException()
    {
        CreateConversationHandler handler = new(_repository, _tenantContext, TimeProvider.System);
        CreateConversationCommand command = new(Guid.NewGuid(), null, null, "Unknown", null);

        Func<Task> act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Unknown conversation type*");
    }
}
