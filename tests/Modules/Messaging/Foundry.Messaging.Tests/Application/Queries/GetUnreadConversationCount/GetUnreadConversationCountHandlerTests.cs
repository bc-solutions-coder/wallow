using Foundry.Messaging.Application.Conversations.Interfaces;
using Foundry.Messaging.Application.Conversations.Queries.GetUnreadConversationCount;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Messaging.Tests.Application.Queries.GetUnreadConversationCount;

public class GetUnreadConversationCountHandlerTests
{
    private readonly IMessagingQueryService _queryService = Substitute.For<IMessagingQueryService>();

    [Fact]
    public async Task Handle_ReturnsCountFromQueryService()
    {
        Guid userId = Guid.NewGuid();
        _queryService.GetUnreadConversationCountAsync(userId, Arg.Any<CancellationToken>())
            .Returns(5);

        GetUnreadConversationCountHandler handler = new(_queryService);
        GetUnreadConversationCountQuery query = new(userId);

        Result<int> result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(5);
    }

    [Fact]
    public async Task Handle_WhenNoUnread_ReturnsZero()
    {
        Guid userId = Guid.NewGuid();
        _queryService.GetUnreadConversationCountAsync(userId, Arg.Any<CancellationToken>())
            .Returns(0);

        GetUnreadConversationCountHandler handler = new(_queryService);
        GetUnreadConversationCountQuery query = new(userId);

        Result<int> result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }
}
