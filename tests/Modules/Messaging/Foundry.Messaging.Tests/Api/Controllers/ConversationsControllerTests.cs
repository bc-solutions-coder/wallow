using System.Security.Claims;
using Foundry.Messaging.Api.Contracts.Messaging.Requests;
using Foundry.Messaging.Api.Contracts.Messaging.Responses;
using Foundry.Messaging.Api.Controllers;
using Foundry.Messaging.Application.Conversations.Commands.CreateConversation;
using Foundry.Messaging.Application.Conversations.Commands.MarkConversationRead;
using Foundry.Messaging.Application.Conversations.Commands.SendMessage;
using Foundry.Messaging.Application.Conversations.DTOs;
using Foundry.Messaging.Application.Conversations.Interfaces;
using Foundry.Messaging.Application.Conversations.Queries.GetConversations;
using Foundry.Messaging.Application.Conversations.Queries.GetMessages;
using Foundry.Messaging.Application.Conversations.Queries.GetUnreadConversationCount;
using Foundry.Shared.Infrastructure.Core.Services;
using Foundry.Shared.Kernel.Results;
using Foundry.Shared.Kernel.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Messaging.Tests.Api.Controllers;

public class ConversationsControllerTests
{
    private readonly IMessageBus _bus;
    private readonly IHtmlSanitizationService _sanitizer;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMessagingQueryService _messagingQueryService;
    private readonly ConversationsController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public ConversationsControllerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _sanitizer = Substitute.For<IHtmlSanitizationService>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _messagingQueryService = Substitute.For<IMessagingQueryService>();

        _currentUserService.GetCurrentUserId().Returns(_userId);
        _sanitizer.Sanitize(Arg.Any<string>()).Returns(x => x.Arg<string>());

        _controller = new ConversationsController(_bus, _sanitizer, _currentUserService, _messagingQueryService);

        ClaimsPrincipal user = new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
        ], "TestAuth"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    #region CreateConversation

    [Fact]
    public async Task CreateConversation_WithSingleParticipant_CreatesDirect_Returns201()
    {
        Guid conversationId = Guid.NewGuid();
        CreateConversationRequest request = new([Guid.NewGuid()], null);
        _bus.InvokeAsync<Result<Guid>>(Arg.Any<CreateConversationCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(conversationId));

        IActionResult result = await _controller.CreateConversation(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.Value.Should().Be(conversationId);
    }

    [Fact]
    public async Task CreateConversation_WithMultipleParticipants_CreatesGroup_Returns201()
    {
        Guid conversationId = Guid.NewGuid();
        CreateConversationRequest request = new([Guid.NewGuid(), Guid.NewGuid()], "Test Group");
        _bus.InvokeAsync<Result<Guid>>(Arg.Any<CreateConversationCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(conversationId));

        IActionResult result = await _controller.CreateConversation(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task CreateConversation_SendsCorrectDirectCommandToBus()
    {
        Guid recipientId = Guid.NewGuid();
        CreateConversationRequest request = new([recipientId], null);
        _bus.InvokeAsync<Result<Guid>>(Arg.Any<CreateConversationCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Guid.NewGuid()));

        await _controller.CreateConversation(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<Guid>>(
            Arg.Is<CreateConversationCommand>(c =>
                c.InitiatorId == _userId &&
                c.RecipientId == recipientId &&
                c.MemberIds == null &&
                c.Type == "Direct"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateConversation_SendsCorrectGroupCommandToBus()
    {
        Guid member1 = Guid.NewGuid();
        Guid member2 = Guid.NewGuid();
        CreateConversationRequest request = new([member1, member2], "My Group");
        _bus.InvokeAsync<Result<Guid>>(Arg.Any<CreateConversationCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Guid.NewGuid()));

        await _controller.CreateConversation(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<Guid>>(
            Arg.Is<CreateConversationCommand>(c =>
                c.InitiatorId == _userId &&
                c.RecipientId == null &&
                c.Type == "Group" &&
                c.Name == "My Group"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateConversation_WhenUserIdNull_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        CreateConversationRequest request = new([Guid.NewGuid()], null);

        IActionResult result = await _controller.CreateConversation(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task CreateConversation_WhenValidationFails_Returns400()
    {
        CreateConversationRequest request = new([Guid.NewGuid()], null);
        _bus.InvokeAsync<Result<Guid>>(Arg.Any<CreateConversationCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<Guid>(Error.Validation("Invalid conversation")));

        IActionResult result = await _controller.CreateConversation(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    #endregion

    #region GetConversations

    [Fact]
    public async Task GetConversations_WhenSuccess_ReturnsOkWithList()
    {
        List<ConversationDto> conversations =
        [
            new(Guid.NewGuid(), "Direct", [], null, 0, DateTime.UtcNow),
            new(Guid.NewGuid(), "Group", [], null, 2, DateTime.UtcNow)
        ];
        _bus.InvokeAsync<Result<IReadOnlyList<ConversationDto>>>(Arg.Any<GetConversationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<ConversationDto>>(conversations));

        IActionResult result = await _controller.GetConversations(1, 20, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<ConversationResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<ConversationResponse>>().Subject;
        responses.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetConversations_PassesCorrectQueryToBus()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<ConversationDto>>>(Arg.Any<GetConversationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<ConversationDto>>([]));

        await _controller.GetConversations(2, 10, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<ConversationDto>>>(
            Arg.Is<GetConversationsQuery>(q => q.UserId == _userId && q.Page == 2 && q.PageSize == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetConversations_WhenUserIdNull_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        IActionResult result = await _controller.GetConversations(1, 20, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task GetConversations_MapsConversationResponseFields()
    {
        Guid conversationId = Guid.NewGuid();
        DateTime activityAt = DateTime.UtcNow;
        List<ConversationDto> conversations =
        [
            new(conversationId, "Direct", [], null, 3, activityAt)
        ];
        _bus.InvokeAsync<Result<IReadOnlyList<ConversationDto>>>(Arg.Any<GetConversationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<ConversationDto>>(conversations));

        IActionResult result = await _controller.GetConversations(1, 20, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<ConversationResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<ConversationResponse>>().Subject;
        ConversationResponse response = responses[0];
        response.Id.Should().Be(conversationId);
        response.Type.Should().Be("Direct");
        response.UnreadCount.Should().Be(3);
    }

    #endregion

    #region GetMessages

    [Fact]
    public async Task GetMessages_WhenParticipant_ReturnsOkWithPageResponse()
    {
        Guid conversationId = Guid.NewGuid();
        _messagingQueryService.IsParticipantAsync(conversationId, _userId, Arg.Any<CancellationToken>())
            .Returns(true);

        List<MessageDto> messages =
        [
            new(Guid.NewGuid(), conversationId, _userId, "Hello", DateTime.UtcNow, "Sent")
        ];
        _bus.InvokeAsync<Result<IReadOnlyList<MessageDto>>>(Arg.Any<GetMessagesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<MessageDto>>(messages));

        IActionResult result = await _controller.GetMessages(conversationId, null, 50, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        MessagePageResponse pageResponse = ok.Value.Should().BeOfType<MessagePageResponse>().Subject;
        pageResponse.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetMessages_WhenNotParticipant_Returns403()
    {
        Guid conversationId = Guid.NewGuid();
        _messagingQueryService.IsParticipantAsync(conversationId, _userId, Arg.Any<CancellationToken>())
            .Returns(false);

        IActionResult result = await _controller.GetMessages(conversationId, null, 50, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task GetMessages_WhenUserIdNull_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        IActionResult result = await _controller.GetMessages(Guid.NewGuid(), null, 50, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task GetMessages_HasMoreFlag_WhenPageFull()
    {
        Guid conversationId = Guid.NewGuid();
        int pageSize = 2;
        _messagingQueryService.IsParticipantAsync(conversationId, _userId, Arg.Any<CancellationToken>())
            .Returns(true);

        List<MessageDto> messages =
        [
            new(Guid.NewGuid(), conversationId, _userId, "Msg1", DateTime.UtcNow, "Sent"),
            new(Guid.NewGuid(), conversationId, _userId, "Msg2", DateTime.UtcNow.AddSeconds(-1), "Sent")
        ];
        _bus.InvokeAsync<Result<IReadOnlyList<MessageDto>>>(Arg.Any<GetMessagesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<MessageDto>>(messages));

        IActionResult result = await _controller.GetMessages(conversationId, null, pageSize, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        MessagePageResponse pageResponse = ok.Value.Should().BeOfType<MessagePageResponse>().Subject;
        pageResponse.HasMore.Should().BeTrue();
        pageResponse.NextCursor.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMessages_HasMoreFlag_WhenPageNotFull()
    {
        Guid conversationId = Guid.NewGuid();
        _messagingQueryService.IsParticipantAsync(conversationId, _userId, Arg.Any<CancellationToken>())
            .Returns(true);

        List<MessageDto> messages =
        [
            new(Guid.NewGuid(), conversationId, _userId, "Msg1", DateTime.UtcNow, "Sent")
        ];
        _bus.InvokeAsync<Result<IReadOnlyList<MessageDto>>>(Arg.Any<GetMessagesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<MessageDto>>(messages));

        IActionResult result = await _controller.GetMessages(conversationId, null, 50, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        MessagePageResponse pageResponse = ok.Value.Should().BeOfType<MessagePageResponse>().Subject;
        pageResponse.HasMore.Should().BeFalse();
    }

    #endregion

    #region SendMessage

    [Fact]
    public async Task SendMessage_WhenParticipant_Returns201()
    {
        Guid conversationId = Guid.NewGuid();
        Guid messageId = Guid.NewGuid();
        _messagingQueryService.IsParticipantAsync(conversationId, _userId, Arg.Any<CancellationToken>())
            .Returns(true);
        _bus.InvokeAsync<Result<Guid>>(Arg.Any<SendMessageCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(messageId));

        SendMessageRequest request = new("Hello!");

        IActionResult result = await _controller.SendMessage(conversationId, request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task SendMessage_WhenNotParticipant_Returns403()
    {
        Guid conversationId = Guid.NewGuid();
        _messagingQueryService.IsParticipantAsync(conversationId, _userId, Arg.Any<CancellationToken>())
            .Returns(false);

        SendMessageRequest request = new("Hello!");

        IActionResult result = await _controller.SendMessage(conversationId, request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task SendMessage_WhenUserIdNull_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        SendMessageRequest request = new("Hello!");

        IActionResult result = await _controller.SendMessage(Guid.NewGuid(), request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task SendMessage_SanitizesBodyBeforeSending()
    {
        Guid conversationId = Guid.NewGuid();
        _messagingQueryService.IsParticipantAsync(conversationId, _userId, Arg.Any<CancellationToken>())
            .Returns(true);
        _sanitizer.Sanitize("<script>alert('xss')</script>Hello").Returns("Hello");
        _bus.InvokeAsync<Result<Guid>>(Arg.Any<SendMessageCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Guid.NewGuid()));

        SendMessageRequest request = new("<script>alert('xss')</script>Hello");

        await _controller.SendMessage(conversationId, request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<Guid>>(
            Arg.Is<SendMessageCommand>(c => c.Body == "Hello"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessage_PassesCorrectCommandToBus()
    {
        Guid conversationId = Guid.NewGuid();
        _messagingQueryService.IsParticipantAsync(conversationId, _userId, Arg.Any<CancellationToken>())
            .Returns(true);
        _bus.InvokeAsync<Result<Guid>>(Arg.Any<SendMessageCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Guid.NewGuid()));

        SendMessageRequest request = new("Test body");

        await _controller.SendMessage(conversationId, request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<Guid>>(
            Arg.Is<SendMessageCommand>(c =>
                c.ConversationId == conversationId &&
                c.SenderId == _userId &&
                c.Body == "Test body"),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region MarkAsRead

    [Fact]
    public async Task MarkAsRead_WhenSuccess_Returns204()
    {
        Guid conversationId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<MarkConversationReadCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult result = await _controller.MarkAsRead(conversationId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task MarkAsRead_WhenUserIdNull_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        IActionResult result = await _controller.MarkAsRead(Guid.NewGuid(), CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task MarkAsRead_WhenNotFound_Returns404()
    {
        Guid conversationId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<MarkConversationReadCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.NotFound("Conversation", conversationId)));

        IActionResult result = await _controller.MarkAsRead(conversationId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task MarkAsRead_PassesCorrectCommandToBus()
    {
        Guid conversationId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<MarkConversationReadCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.MarkAsRead(conversationId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<MarkConversationReadCommand>(c =>
                c.ConversationId == conversationId &&
                c.UserId == _userId),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetUnreadCount

    [Fact]
    public async Task GetUnreadCount_WhenSuccess_ReturnsOkWithCount()
    {
        _bus.InvokeAsync<Result<int>>(Arg.Any<GetUnreadConversationCountQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(7));

        IActionResult result = await _controller.GetUnreadCount(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        UnreadCountResponse response = ok.Value.Should().BeOfType<UnreadCountResponse>().Subject;
        response.Count.Should().Be(7);
    }

    [Fact]
    public async Task GetUnreadCount_WhenUserIdNull_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        IActionResult result = await _controller.GetUnreadCount(CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task GetUnreadCount_PassesCorrectQueryToBus()
    {
        _bus.InvokeAsync<Result<int>>(Arg.Any<GetUnreadConversationCountQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(0));

        await _controller.GetUnreadCount(CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<int>>(
            Arg.Is<GetUnreadConversationCountQuery>(q => q.UserId == _userId),
            Arg.Any<CancellationToken>());
    }

    #endregion
}
