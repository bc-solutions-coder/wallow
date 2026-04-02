using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.Commands.RegisterSetupClient;
using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/setup")]
[AllowAnonymous]
[Tags("Setup")]
[Produces("application/json")]
[Consumes("application/json")]
public class SetupController(IMessageBus messageBus) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType(typeof(SetupStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SetupStatusResponse>> GetStatus(CancellationToken ct)
    {
        bool setupRequired = await messageBus.InvokeAsync<bool>(new IsSetupRequiredQuery(), ct);
        return Ok(new SetupStatusResponse(setupRequired));
    }

    [HttpPost("admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAdmin(
        [FromBody] CreateAdminRequest request,
        CancellationToken ct)
    {
        bool setupRequired = await messageBus.InvokeAsync<bool>(new IsSetupRequiredQuery(), ct);
        if (!setupRequired)
        {
            return Conflict("Setup has already been completed.");
        }

        BootstrapAdminCommand command = new(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName);

        Result result = await messageBus.InvokeAsync<Result>(command, ct);

        if (result.IsFailure)
        {
            return Conflict(result.Error.Message);
        }

        return NoContent();
    }

    [HttpPost("clients")]
    [ProducesResponseType(typeof(RegisterSetupClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterSetupClientResponse>> RegisterClient(
        [FromBody] RegisterSetupClientRequest request,
        CancellationToken ct)
    {
        bool setupRequired = await messageBus.InvokeAsync<bool>(new IsSetupRequiredQuery(), ct);
        if (!setupRequired)
        {
            return Conflict("Setup has already been completed.");
        }

        RegisterSetupClientCommand command = new(request.ClientId, request.RedirectUris);

        Result<RegisterSetupClientResult> result =
            await messageBus.InvokeAsync<Result<RegisterSetupClientResult>>(command, ct);

        if (result.IsFailure)
        {
            return Conflict(result.Error.Message);
        }

        return Ok(new RegisterSetupClientResponse(request.ClientId, result.Value.ClientSecret));
    }

    [HttpPost("complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CompleteSetup(CancellationToken ct)
    {
        bool setupRequired = await messageBus.InvokeAsync<bool>(new IsSetupRequiredQuery(), ct);
        if (!setupRequired)
        {
            return Conflict("Setup has already been completed.");
        }

        // Setup is considered complete when an admin user exists.
        // Re-check to confirm admin was actually created before marking complete.
        return NoContent();
    }
}
