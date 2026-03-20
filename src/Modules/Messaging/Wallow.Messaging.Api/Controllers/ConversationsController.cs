using Asp.Versioning;
using Wallow.Messaging.Api.Contracts.Messaging.Requests;
using Wallow.Messaging.Api.Contracts.Messaging.Responses;
using Wallow.Messaging.Application.Conversations.Commands.CreateConversation;
using Wallow.Messaging.Application.Conversations.Commands.MarkConversationRead;
using Wallow.Messaging.Application.Conversations.Commands.SendMessage;
using Wallow.Messaging.Application.Conversations.DTOs;
using Wallow.Messaging.Application.Conversations.Interfaces;
using Wallow.Messaging.Application.Conversations.Queries.GetConversations;
using Wallow.Messaging.Application.Conversations.Queries.GetMessages;
using Wallow.Messaging.Application.Conversations.Queries.GetUnreadConversationCount;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Infrastructure.Core.Services;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.Results;
using Wallow.Shared.Kernel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Wallow.Messaging.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/conversations")]
[Authorize]
[Tags("Conversations")]
[Produces("application/json")]
[Consumes("application/json")]
public class ConversationsController(IMessageBus bus, IHtmlSanitizationService sanitizer, ICurrentUserService currentUserService, IMessagingQueryService messagingQueryService) : ControllerBase
{
    [HttpPost]
    [HasPermission(PermissionType.MessagingAccess)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateConversation(
        [FromBody] CreateConversationRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
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

        Result<Guid> result = await bus.InvokeAsync<Result<Guid>>(command, cancellationToken);

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
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        Result<IReadOnlyList<ConversationDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<ConversationDto>>>(
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
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        bool isParticipant = await messagingQueryService.IsParticipantAsync(id, userId.Value, cancellationToken);
        if (!isParticipant)
        {
            return Problem(statusCode: 403, title: "Forbidden", detail: "Access denied");
        }

        Result<IReadOnlyList<MessageDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<MessageDto>>>(
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendMessage(
        Guid id,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        bool isParticipant = await messagingQueryService.IsParticipantAsync(id, userId.Value, cancellationToken);
        if (!isParticipant)
        {
            return Problem(statusCode: 403, title: "Forbidden", detail: "Access denied");
        }

        string sanitizedBody = sanitizer.Sanitize(request.Body);

        Result<Guid> result = await bus.InvokeAsync<Result<Guid>>(
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
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        Result result = await bus.InvokeAsync<Result>(
            new MarkConversationReadCommand(id, userId.Value), cancellationToken);

        return result.ToNoContentResult();
    }

    [HttpGet("unread-count")]
    [HasPermission(PermissionType.MessagingAccess)]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        Result<int> result = await bus.InvokeAsync<Result<int>>(
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
