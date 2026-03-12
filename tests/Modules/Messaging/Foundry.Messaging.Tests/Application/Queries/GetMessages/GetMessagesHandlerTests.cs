using Foundry.Messaging.Application.Conversations.DTOs;
using Foundry.Messaging.Application.Conversations.Interfaces;
using Foundry.Messaging.Application.Conversations.Queries.GetMessages;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Messaging.Tests.Application.Queries.GetMessages;

public class GetMessagesHandlerTests
{
    private readonly IMessagingQueryService _queryService = Substitute.For<IMessagingQueryService>();

    [Fact]
    public async Task Handle_ReturnsMessagesFromQueryService()
    {
        Guid conversationId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        List<MessageDto> messages =
        [
            new(Guid.NewGuid(), conversationId, userId, "Hello", DateTime.UtcNow, "Sent")
        ];
        _queryService.GetMessagesAsync(conversationId, userId, null, 50, Arg.Any<CancellationToken>())
            .Returns(messages);

        GetMessagesHandler handler = new(_queryService);
        GetMessagesQuery query = new(conversationId, userId, null, 50);

        Result<IReadOnlyList<MessageDto>> result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WithCursor_PassesCursorToQueryService()
    {
        Guid conversationId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Guid cursorId = Guid.NewGuid();
        _queryService.GetMessagesAsync(conversationId, userId, cursorId, 25, Arg.Any<CancellationToken>())
            .Returns(new List<MessageDto>());

        GetMessagesHandler handler = new(_queryService);
        GetMessagesQuery query = new(conversationId, userId, cursorId, 25);

        Result<IReadOnlyList<MessageDto>> result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _queryService.Received(1).GetMessagesAsync(conversationId, userId, cursorId, 25, Arg.Any<CancellationToken>());
    }
}
