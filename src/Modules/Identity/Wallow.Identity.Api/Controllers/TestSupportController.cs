using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Extensions;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/test")]
[Authorize]
[Tags("Test Support")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class TestSupportController(
    ITestSupportService testSupportService,
    IHostEnvironment environment) : ControllerBase
{
    [HttpPost("isolated-org")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> CreateIsolatedOrg(
        [FromBody] CreateIsolatedOrgRequest request, CancellationToken ct)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        Guid userId = Guid.Parse(User.GetUserId()!);
        Guid orgId = await testSupportService.CreateIsolatedOrgAsync(
            userId, request.RequireMfa, request.GracePeriodDays, ct);

        return Ok(new { orgId });
    }
}

public sealed record CreateIsolatedOrgRequest(bool RequireMfa = false, int GracePeriodDays = 0);
