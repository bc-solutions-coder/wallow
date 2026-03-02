using Asp.Versioning;
using Foundry.Communications.Api.Contracts.Email.Requests;
using Foundry.Communications.Api.Contracts.Email.Responses;
using Foundry.Communications.Api.Extensions;
using Foundry.Communications.Api.Mappings;
using Foundry.Communications.Application.Channels.Email.Commands.UpdateEmailPreferences;
using Foundry.Communications.Application.Channels.Email.DTOs;
using Foundry.Communications.Application.Channels.Email.Queries.GetEmailPreferences;
using Foundry.Shared.Kernel.Results;
using Foundry.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Communications.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/email/preferences")]
[Authorize]
[Tags("Email Preferences")]
[Produces("application/json")]
[Consumes("application/json")]
public class EmailPreferencesController : ControllerBase
{
    private readonly IMessageBus _bus;

    public EmailPreferencesController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// Get the current user's email preferences.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionType.EmailPreferenceManage)]
    [ProducesResponseType(typeof(IReadOnlyList<EmailPreferenceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        Result<IReadOnlyList<EmailPreferenceDto>> result = await _bus.InvokeAsync<Result<IReadOnlyList<EmailPreferenceDto>>>(
            new GetEmailPreferencesQuery(userId.Value), cancellationToken);

        return result.Map(prefs => (IReadOnlyList<EmailPreferenceResponse>)prefs.Select(ToResponse).ToList()).ToActionResult();
    }

    /// <summary>
    /// Update the current user's email preference for a specific notification type.
    /// </summary>
    [HttpPut]
    [HasPermission(PermissionType.EmailPreferenceManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdatePreference(
        [FromBody] UpdateEmailPreferenceRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        UpdateEmailPreferencesCommand command = new UpdateEmailPreferencesCommand(
            userId.Value,
            request.NotificationType.ToDomain(),
            request.IsEnabled);

        Result result = await _bus.InvokeAsync<Result>(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        string? userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out Guid userId))
        {
            return null;
        }

        return userId;
    }

    private static EmailPreferenceResponse ToResponse(EmailPreferenceDto dto) => new(
        dto.Id,
        dto.UserId,
        dto.NotificationType.ToApi(),
        dto.IsEnabled,
        dto.CreatedAt,
        dto.UpdatedAt);
}
