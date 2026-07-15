using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Tests.Api.Controllers;

public class OrganizationDomainsControllerTests
{
    private readonly IDomainAssignmentService _domainAssignmentService;
    private readonly IOrganizationDomainRepository _domainRepository;
    private readonly OrganizationDomainsController _controller;

    public OrganizationDomainsControllerTests()
    {
        _domainAssignmentService = Substitute.For<IDomainAssignmentService>();
        _domainRepository = Substitute.For<IOrganizationDomainRepository>();
        _controller = new OrganizationDomainsController(_domainAssignmentService, _domainRepository);
    }

    [Fact]
    public async Task Register_ReturnsCreatedAtAction()
    {
        Guid orgId = Guid.NewGuid();
        Guid domainId = Guid.NewGuid();
        _domainAssignmentService.RegisterDomainAsync(orgId, "example.com", Arg.Any<CancellationToken>())
            .Returns(domainId);

        RegisterDomainRequest request = new(orgId, "example.com");

        ActionResult result = await _controller.Register(request, CancellationToken.None);

        CreatedAtActionResult created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(OrganizationDomainsController.GetById));
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsOk()
    {
        Guid id = Guid.NewGuid();
        OrganizationDomain domain = OrganizationDomain.Create(
            TenantId.New(), OrganizationId.New(), "example.com", "verify-token",
            Guid.NewGuid(), TimeProvider.System);
        _domainRepository.GetByIdAsync(Arg.Any<OrganizationDomainId>(), Arg.Any<CancellationToken>())
            .Returns(domain);

        ActionResult result = await _controller.GetById(id, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        _domainRepository.GetByIdAsync(Arg.Any<OrganizationDomainId>(), Arg.Any<CancellationToken>())
            .Returns((OrganizationDomain?)null);

        ActionResult result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetByOrganization_ReturnsOk()
    {
        Guid orgId = Guid.NewGuid();
        List<OrganizationDomain> domains =
        [
            OrganizationDomain.Create(
                TenantId.New(), OrganizationId.Create(orgId), "a.com", "token1",
                Guid.NewGuid(), TimeProvider.System)
        ];
        _domainRepository.GetByOrganizationIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
            .Returns(domains);

        ActionResult result = await _controller.GetByOrganization(orgId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Verify_ReturnsNoContent()
    {
        Guid id = Guid.NewGuid();
        VerifyDomainRequest request = new("verify-token");

        ActionResult result = await _controller.Verify(id, request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _domainAssignmentService.Received(1).VerifyDomainAsync(id, "verify-token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Match_WithValidEmail_WhenVerifiedDomainExists_ReturnsOk()
    {
        OrganizationDomain domain = OrganizationDomain.Create(
            TenantId.New(), OrganizationId.New(), "example.com", "token",
            Guid.NewGuid(), TimeProvider.System);
        domain.Verify(Guid.NewGuid(), TimeProvider.System);

        _domainRepository.GetByDomainAsync("example.com", Arg.Any<CancellationToken>())
            .Returns(domain);

        ActionResult result = await _controller.Match("user@example.com", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Match_WithInvalidEmail_ReturnsBadRequest()
    {
        ActionResult result = await _controller.Match("not-an-email", CancellationToken.None);

        BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Invalid email format");
    }

    [Fact]
    public async Task Match_WhenDomainNotFound_ReturnsNotFound()
    {
        _domainRepository.GetByDomainAsync("unknown.com", Arg.Any<CancellationToken>())
            .Returns((OrganizationDomain?)null);

        ActionResult result = await _controller.Match("user@unknown.com", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Match_WhenDomainNotVerified_ReturnsNotFound()
    {
        OrganizationDomain domain = OrganizationDomain.Create(
            TenantId.New(), OrganizationId.New(), "example.com", "token",
            Guid.NewGuid(), TimeProvider.System);

        _domainRepository.GetByDomainAsync("example.com", Arg.Any<CancellationToken>())
            .Returns(domain);

        ActionResult result = await _controller.Match("user@example.com", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }
}
