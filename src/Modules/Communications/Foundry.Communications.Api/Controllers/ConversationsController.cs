using Asp.Versioning;
using Foundry.Communications.Api.Contracts.Messaging.Requests;
using Foundry.Communications.Api.Contracts.Messaging.Responses;
using Foundry.Shared.Api.Extensions;
using Foundry.Communications.Application.Messaging.Commands.CreateConversation;
using Foundry.Communications.Application.Messaging.Commands.MarkConversationRead;
using Foundry.Communications.Application.Messaging.Commands.SendMessage;
using Foundry.Communications.Application.Messaging.DTOs;
using Foundry.Communications.Application.Messaging.Queries.GetConversations;
using Foundry.Communications.Application.Messaging.Queries.GetMessages;
using Foundry.Communications.Application.Messaging.Queries.GetUnreadConversationCount;
using Foundry.Communications.Application.Messaging.Interfaces;
using Foundry.Shared.Infrastructure.Services;
using Foundry.Shared.Kernel.Results;
using Foundry.Shared.Kernel.Services;
using Foundry.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Communications.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/conversations")]
[Authorize]
[Tags("Conversations")]
[Produces("application/json")]
[Consumes("application/json")]
public class ConversationsController : ControllerBase
{
    private readonly IMessageBus _bus;
    private readonly IHtmlSanitizationService _sanitizer;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMessagingQueryService _messagingQueryService;

    public ConversationsController(IMessageBus bus, IHtmlSanitizationService sanitizer, ICurrentUserService currentUserService, IMessagingQueryService messagingQueryService)
    {
        _bus = bus;
        _sanitizer = sanitizer;
        _currentUserService = currentUserService;
        _messagingQueryService = messagingQueryService;
    }

    [HttpPost]
    [HasPermission(PermissionType.MessagingAccess)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateConversation(
        [FromBody] CreateConversationRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = _currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        bool isDirect = request.ParticipantIds.Count == 1;
        string type = isDirect ? "Direct" : "Group";

        CreateConversationCommand command = new(
            userId.Value,
            isDirect ? request.ParticipantIds[0] : null,
            isDirect ? null : request.ParticipantIds,
            type,
            request.Subject);

        Result<Guid> result = await _bus.InvokeAsync<Result<Guid>>(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return result.ToCreatedResult($"/api/v1/conversations/{result.Value}");
    }

    [HttpGet]
    [HasPermission(PermissionType.MessagingAccess)]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        Guid? userId = _currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        Result<IReadOnlyList<ConversationDto>> result = await _bus.InvokeAsync<Result<IReadOnlyList<ConversationDto>>>(
            new GetConversationsQuery(userId.Value, page, pageSize), cancellationToken);

        return result.Map(conversations =>
            conversations.Select(ToConversationResponse).ToList() as IReadOnlyList<ConversationResponse>)
            .ToActionResult();
    }

    [HttpGet("{id:guid}/messages")]
    [HasPermission(PermissionType.MessagingAccess)]
    [ProducesResponseType(typeof(MessagePageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMessages(
        Guid id,
        [FromQuery] Guid? cursor = null,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        Guid? userId = _currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        bool isParticipant = await _messagingQueryService.IsParticipantAsync(id, userId.Value, cancellationToken);
        if (!isParticipant)
        {
            return Problem(statusCode: 403, title: "Forbidden", detail: "Access denied");
        }

        Result<IReadOnlyList<MessageDto>> result = await _bus.InvokeAsync<Result<IReadOnlyList<MessageDto>>>(
            new GetMessagesQuery(id, userId.Value, cursor, pageSize), cancellationToken);

        return result.Map(messages =>
        {
            List<MessageResponse> items = messages.Select(ToMessageResponse).ToList();
            bool hasMore = items.Count == pageSize;
            Guid? nextCursor = hasMore && items.Count > 0 ? items[^1].Id : null;
            return new MessagePageResponse(items, nextCursor, hasMore);
        }).ToActionResult();
    }

    [HttpPost("{id:guid}/messages")]
    [HasPermission(PermissionType.MessagingAccess)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendMessage(
        Guid id,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = _currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        string sanitizedBody = _sanitizer.Sanitize(request.Body);

        Result<Guid> result = await _bus.InvokeAsync<Result<Guid>>(
            new SendMessageCommand(id, userId.Value, sanitizedBody), cancellationToken);

        return result.ToCreatedResult($"/api/v1/conversations/{id}/messages");
    }

    [HttpPost("{id:guid}/read")]
    [HasPermission(PermissionType.MessagingAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        Guid? userId = _currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        Result result = await _bus.InvokeAsync<Result>(
            new MarkConversationReadCommand(id, userId.Value), cancellationToken);

        return result.ToNoContentResult();
    }

    [HttpGet("unread-count")]
    [HasPermission(PermissionType.MessagingAccess)]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        Guid? userId = _currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        Result<int> result = await _bus.InvokeAsync<Result<int>>(
            new GetUnreadConversationCountQuery(userId.Value), cancellationToken);

        return result.Map(count => new UnreadCountResponse(count)).ToActionResult();
    }

    private static ConversationResponse ToConversationResponse(ConversationDto dto) => new(
        dto.Id,
        dto.Type,
        dto.Participants,
        dto.LastMessage,
        dto.UnreadCount,
        dto.LastActivityAt);

    private static MessageResponse ToMessageResponse(MessageDto dto) => new(
        dto.Id,
        dto.ConversationId,
        dto.SenderId,
        dto.Body,
        dto.Status,
        dto.SentAt);
}
