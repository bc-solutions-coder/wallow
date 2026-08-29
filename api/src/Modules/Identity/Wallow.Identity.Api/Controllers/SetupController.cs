using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
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
public class SetupController(IMessageBus messageBus, IOrganizationService organizationService) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType(typeof(SetupStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SetupStatusResponse>> GetStatus(CancellationToken ct)
    {
        bool setupRequired = await messageBus.InvokeAsync<bool>(new IsSetupRequiredQuery(), ct);
        if (!setupRequired)
        {
            return Ok(new SetupStatusResponse(SetupRequired: false));
        }

        // Only while setup is open, and only when there is exactly one: this endpoint is
        // anonymous, and a single seeded name is the whole of what the page needs to keep the
        // administrator out of a sibling organization. Two would be a choice nobody unauthenticated
        // should be offered.
        IReadOnlyList<OrganizationDto> organizations = await organizationService.GetOrganizationsAsync(max: 2, ct: ct);
        string? organizationName = organizations.Count == 1 ? organizations[0].Name : null;

        return Ok(new SetupStatusResponse(SetupRequired: true, organizationName));
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
            request.LastName,
            request.OrganizationName);

        Result result = await messageBus.InvokeAsync<Result>(command, ct);

        if (result.IsFailure)
        {
            return Conflict(result.Error.Message);
        }

        return NoContent();
    }
}
