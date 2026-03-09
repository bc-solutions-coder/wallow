using Asp.Versioning;
using Foundry.Showcases.Api.Contracts.Requests;
using Foundry.Showcases.Application.Commands.CreateShowcase;
using Foundry.Showcases.Application.Commands.DeleteShowcase;
using Foundry.Showcases.Application.Commands.UpdateShowcase;
using Foundry.Showcases.Application.Contracts;
using Foundry.Showcases.Application.Queries.GetShowcase;
using Foundry.Showcases.Application.Queries.GetShowcases;
using Foundry.Showcases.Domain.Enums;
using Foundry.Showcases.Domain.Identity;
using Foundry.Shared.Api.Extensions;
using Foundry.Shared.Kernel.Identity.Authorization;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Showcases.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/showcases")]
[Authorize]
[Tags("Showcases")]
[Produces("application/json")]
[Consumes("application/json")]
public class ShowcasesController(IMessageBus bus) : ControllerBase
{

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<ShowcaseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] ShowcaseCategory? category,
        [FromQuery] string? tag,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<ShowcaseDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<ShowcaseDto>>>(
            new GetShowcasesQuery(category, tag), cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ShowcaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Result<ShowcaseDto> result = await bus.InvokeAsync<Result<ShowcaseDto>>(
            new GetShowcaseQuery(new ShowcaseId(id)), cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost]
    [HasPermission(PermissionType.ShowcasesManage)]
    [ProducesResponseType(typeof(ShowcaseId), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateShowcaseRequest request,
        CancellationToken cancellationToken)
    {
        CreateShowcaseCommand command = new(
            request.Title,
            request.Description,
            request.Category,
            request.DemoUrl,
            request.GitHubUrl,
            request.VideoUrl,
            request.Tags,
            request.DisplayOrder,
            request.IsPublished);

        Result<ShowcaseId> result = await bus.InvokeAsync<Result<ShowcaseId>>(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return result.ToCreatedResult($"/api/v1/showcases/{result.Value.Value}");
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionType.ShowcasesManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateShowcaseRequest request,
        CancellationToken cancellationToken)
    {
        UpdateShowcaseCommand command = new(
            new ShowcaseId(id),
            request.Title,
            request.Description,
            request.Category,
            request.DemoUrl,
            request.GitHubUrl,
            request.VideoUrl,
            request.Tags,
            request.DisplayOrder,
            request.IsPublished);

        Result result = await bus.InvokeAsync<Result>(command, cancellationToken);

        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionType.ShowcasesManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        DeleteShowcaseCommand command = new(new ShowcaseId(id));

        Result result = await bus.InvokeAsync<Result>(command, cancellationToken);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return result.ToActionResult();
    }
}
