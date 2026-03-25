using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Notifications.Api.Contracts.Push;
using Wallow.Notifications.Application.Channels.Push.Commands.DeregisterDevice;
using Wallow.Notifications.Application.Channels.Push.Commands.RegisterDevice;
using Wallow.Notifications.Application.Channels.Push.Commands.SendPush;
using Wallow.Notifications.Application.Channels.Push.DTOs;
using Wallow.Notifications.Application.Channels.Push.Queries.GetUserDevices;
using Wallow.Notifications.Domain.Channels.Push.Identity;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Results;
using Wallow.Shared.Kernel.Services;
using Wolverine;

namespace Wallow.Notifications.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/push")]
[Authorize]
[Tags("Push Devices")]
[Produces("application/json")]
[Consumes("application/json")]
public class PushDevicesController(
    IMessageBus bus,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext) : ControllerBase
{
    [HttpPost("devices")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterDevice(
        [FromBody] RegisterDeviceRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "User context is required");
        }

        Result result = await bus.InvokeAsync<Result>(
            new RegisterDeviceCommand(
                new UserId(userId.Value),
                new TenantId(tenantContext.TenantId.Value),
                request.Platform,
                request.Token),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return NoContent();
    }

    [HttpDelete("devices/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeregisterDevice(Guid id, CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "User context is required");
        }

        Result result = await bus.InvokeAsync<Result>(
            new DeregisterDeviceCommand(new DeviceRegistrationId(id)),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return NoContent();
    }

    [HttpGet("devices")]
    [ProducesResponseType(typeof(List<DeviceRegistrationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUserDevices(CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "User context is required");
        }

        Result<IReadOnlyList<DeviceRegistrationDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<DeviceRegistrationDto>>>(
            new GetUserDevicesQuery(userId.Value, tenantContext.TenantId.Value),
            cancellationToken);

        return result.Map(devices => devices.Select(ToResponse).ToList()).ToActionResult();
    }

    [HttpPost("send")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SendPush(
        [FromBody] SendPushRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "User context is required");
        }

        Result result = await bus.InvokeAsync<Result>(
            new SendPushCommand(
                new UserId(request.RecipientId),
                new TenantId(tenantContext.TenantId.Value),
                request.Title,
                request.Body,
                request.NotificationType),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return NoContent();
    }

    private static DeviceRegistrationResponse ToResponse(DeviceRegistrationDto dto) => new(
        dto.Id,
        dto.UserId,
        dto.Platform,
        dto.Token,
        dto.IsActive,
        dto.RegisteredAt);
}
