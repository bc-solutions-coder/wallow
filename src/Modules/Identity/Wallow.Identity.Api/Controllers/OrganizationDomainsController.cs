using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/organization-domains")]
[Authorize]
[Tags("Organization Domains")]
[Produces("application/json")]
[Consumes("application/json")]
public class OrganizationDomainsController(
    IDomainAssignmentService domainAssignmentService,
    IOrganizationDomainRepository domainRepository) : ControllerBase
{
    [HttpPost]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult> Register(RegisterDomainRequest request, CancellationToken ct)
    {
        Guid domainId = await domainAssignmentService.RegisterDomainAsync(request.OrganizationId, request.Domain, ct);
        return CreatedAtAction(nameof(GetById), new { id = domainId }, new { id = domainId });
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionType.OrganizationsRead)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetById(Guid id, CancellationToken ct)
    {
        OrganizationDomain? domain = await domainRepository.GetByIdAsync(OrganizationDomainId.Create(id), ct);
        if (domain is null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(domain));
    }

    [HttpGet("by-organization/{organizationId:guid}")]
    [HasPermission(PermissionType.OrganizationsRead)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetByOrganization(Guid organizationId, CancellationToken ct)
    {
        List<OrganizationDomain> domains = await domainRepository.GetByOrganizationIdAsync(
            OrganizationId.Create(organizationId), ct);
        return Ok(domains.Select(MapToResponse));
    }

    [HttpPost("{id:guid}/verify")]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Verify(Guid id, VerifyDomainRequest request, CancellationToken ct)
    {
        await domainAssignmentService.VerifyDomainAsync(id, request.VerificationToken, ct);
        return NoContent();
    }

    [HttpGet("match")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Match([FromQuery] string email, CancellationToken ct)
    {
        int atIndex = email.IndexOf('@', StringComparison.Ordinal);
        if (atIndex < 0)
        {
            return BadRequest("Invalid email format");
        }

        string emailDomain = email[(atIndex + 1)..].ToLowerInvariant();
        OrganizationDomain? domain = await domainRepository.GetByDomainAsync(emailDomain, ct);

        if (domain is null || !domain.IsVerified)
        {
            return NotFound();
        }

        return Ok(new { organizationId = domain.OrganizationId.Value, domain = domain.Domain });
    }

    private static object MapToResponse(OrganizationDomain domain)
    {
        return new
        {
            id = domain.Id.Value,
            organizationId = domain.OrganizationId.Value,
            domain = domain.Domain,
            isVerified = domain.IsVerified,
            createdAt = domain.CreatedAt
        };
    }
}

public record RegisterDomainRequest(Guid OrganizationId, string Domain);
public record VerifyDomainRequest(string VerificationToken);
