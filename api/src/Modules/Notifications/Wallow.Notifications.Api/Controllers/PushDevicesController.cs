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
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Kernel.Errors;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Results;
using Wallow.Shared.Kernel.Services;
using Wolverine;

namespace Wallow.Notifications.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/push")]
[Authorize]
[Tags("Push Devices")]
[Produces("application/json")]
[Consumes("application/json")]
public class PushDevicesController(
    IMessageBus bus,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext,
    Application.Channels.Push.Interfaces.IWebPushConfiguration webPushConfiguration) : ControllerBase
{
    /// <summary>
    /// Get the current Web Push public key.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user and an organization tenant. Returns the current signing key ID and public
    /// key for browser subscription creation; private keys are never returned. Returns 409 when Web Push is
    /// disabled, unconfigured, or has no usable current key.
    /// </remarks>
    [HttpGet("web-push/public-key")]
    [ProducesResponseType(typeof(Application.Channels.Push.Interfaces.WebPushPublicKey), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetWebPushPublicKey(CancellationToken cancellationToken)
    {
        if (currentUserService.GetCurrentUserId() is null) { return this.Problem(SharedErrors.Unauthenticated); }
        _ = TenantScope.Require(tenantContext.TenantId);
        Application.Channels.Push.Interfaces.WebPushPublicKey? key = await webPushConfiguration.GetCurrentKeyAsync(cancellationToken);
        return key is null ? this.Problem(Domain.Errors.NotificationsErrors.WebPushUnavailable) : Ok(key);
    }

    /// <summary>
    /// Register a push device for the current user.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user in the current tenant. Web Push registrations require a browser
    /// subscription and signing key ID with no token; other platforms require a token and no subscription or
    /// signing key ID. An active token owned by another user or registered for another platform returns 409.
    /// Re-registering an owned device updates it, while an inactive registration can be claimed by a new owner.
    /// </remarks>
    [HttpPost("devices")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterDevice(
        [FromBody] RegisterDeviceRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        Result result = await bus.InvokeAsync<Result>(
            new RegisterDeviceCommand(
                new UserId(userId.Value),
                new TenantId(tenantContext.TenantId.Value),
                request.Platform,
                request.Token, request.Subscription, request.SigningKeyId),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return NoContent();
    }

    /// <summary>
    /// Deactivate a push device.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user who owns the registration in the current tenant. Stops future delivery to
    /// that registration and removes it from the active device list. Returns no content for an owned
    /// registration, including one already inactive, or 404 for an unknown or foreign registration.
    /// </remarks>
    [HttpDelete("devices/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeregisterDevice(Guid id, CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        Result result = await bus.InvokeAsync<Result>(
            new DeregisterDeviceCommand(new DeviceRegistrationId(id), new UserId(userId.Value)),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return NoContent();
    }

    /// <summary>
    /// List the current user's active push devices.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user in the current tenant. Returns active registrations owned by that user,
    /// including platform, token, registration ID, and Web Push signing key ID where applicable. Inactive
    /// registrations are excluded.
    /// </remarks>
    [HttpGet("devices")]
    [ProducesResponseType(typeof(List<DeviceRegistrationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUserDevices(CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        Result<IReadOnlyList<DeviceRegistrationDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<DeviceRegistrationDto>>>(
            new GetUserDevicesQuery(userId.Value),
            cancellationToken);

        return result.Map(devices => devices.Select(ToResponse).ToList()).ToActionResult();
    }

    /// <summary>
    /// Send a push notification to the current user.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user in the current tenant and targets only that user's eligible active
    /// devices. Delivery is queued; a successful response does not confirm device receipt and also covers
    /// disabled preferences or no registered devices. Returns 409 when registered devices exist but none has an
    /// available Web Push key. ClickPath, when supplied, must be a local absolute path such as /notifications.
    /// </remarks>
    [HttpPost("send")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SendPush(
        [FromBody] SendPushRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        Result result = await bus.InvokeAsync<Result>(
            new SendPushCommand(
                new UserId(userId.Value),
                new TenantId(tenantContext.TenantId.Value),
                request.Title,
                request.Body,
                request.NotificationType, request.ClickPath),
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
        dto.RegisteredAt, dto.SigningKeyId);
}
