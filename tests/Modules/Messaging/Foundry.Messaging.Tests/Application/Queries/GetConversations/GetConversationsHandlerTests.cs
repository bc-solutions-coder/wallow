using Foundry.Messaging.Application.Conversations.DTOs;
using Foundry.Messaging.Application.Conversations.Interfaces;
using Foundry.Messaging.Application.Conversations.Queries.GetConversations;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Messaging.Tests.Application.Queries.GetConversations;

public class GetConversationsHandlerTests
{
    private readonly IMessagingQueryService _queryService = Substitute.For<IMessagingQueryService>();

    [Fact]
    public async Task Handle_ReturnsConversationsFromQueryService()
    {
        Guid userId = Guid.NewGuid();
        List<ConversationDto> conversations =
        [
            new(Guid.NewGuid(), "Direct", [], null, 0, DateTime.UtcNow)
        ];
        _queryService.GetConversationsAsync(userId, 1, 20, Arg.Any<CancellationToken>())
            .Returns(conversations);

        GetConversationsHandler handler = new(_queryService);
        GetConversationsQuery query = new(userId, 1, 20);

        Result<IReadOnlyList<ConversationDto>> result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WhenNoConversations_ReturnsEmptyList()
    {
        Guid userId = Guid.NewGuid();
        _queryService.GetConversationsAsync(userId, 1, 20, Arg.Any<CancellationToken>())
            .Returns(new List<ConversationDto>());

        GetConversationsHandler handler = new(_queryService);
        GetConversationsQuery query = new(userId, 1, 20);

        Result<IReadOnlyList<ConversationDto>> result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
