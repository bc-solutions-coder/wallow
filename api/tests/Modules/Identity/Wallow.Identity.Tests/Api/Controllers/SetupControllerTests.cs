using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Shared.Kernel.Errors;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Identity.Tests.Api.Controllers;

public class SetupControllerTests
{
    private readonly IMessageBus _messageBus;
    private readonly IOrganizationService _organizationService;
    private readonly SetupController _controller;

    public SetupControllerTests()
    {
        _messageBus = Substitute.For<IMessageBus>();
        _organizationService = Substitute.For<IOrganizationService>();
        _organizationService
            .GetOrganizationsAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _controller = new SetupController(_messageBus, _organizationService);
    }

    private static OrganizationDto Organization(string name) => new(Guid.NewGuid(), name, null, 0);

    #region GetStatus

    [Fact]
    public async Task GetStatus_WhenSetupRequired_ReturnsTrue()
    {
        _messageBus.InvokeAsync<bool>(Arg.Any<IsSetupRequiredQuery>(), Arg.Any<CancellationToken>())
            .Returns(true);

        ActionResult<SetupStatusResponse> result = await _controller.GetStatus(CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        SetupStatusResponse response = ok.Value.Should().BeOfType<SetupStatusResponse>().Subject;
        response.SetupRequired.Should().BeTrue();
    }

    [Fact]
    public async Task GetStatus_WhenSetupRequiredAndOneOrganizationIsSeeded_OffersItsName()
    {
        _messageBus.InvokeAsync<bool>(Arg.Any<IsSetupRequiredQuery>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _organizationService
            .GetOrganizationsAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Organization("Wallow")]);

        ActionResult<SetupStatusResponse> result = await _controller.GetStatus(CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        SetupStatusResponse response = ok.Value.Should().BeOfType<SetupStatusResponse>().Subject;
        response.SetupRequired.Should().BeTrue();
        response.OrganizationName.Should().Be("Wallow");
    }

    [Fact]
    public async Task GetStatus_WhenSetupRequiredAndNoOrganizationExists_OffersNoName()
    {
        _messageBus.InvokeAsync<bool>(Arg.Any<IsSetupRequiredQuery>(), Arg.Any<CancellationToken>())
            .Returns(true);

        ActionResult<SetupStatusResponse> result = await _controller.GetStatus(CancellationToken.None);

        SetupStatusResponse response = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<SetupStatusResponse>().Subject;
        response.OrganizationName.Should().BeNull();
    }

    [Fact]
    public async Task GetStatus_WhenSetupRequiredAndSeveralOrganizationsExist_OffersNoName()
    {
        _messageBus.InvokeAsync<bool>(Arg.Any<IsSetupRequiredQuery>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _organizationService
            .GetOrganizationsAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Organization("Wallow"), Organization("Contoso")]);

        ActionResult<SetupStatusResponse> result = await _controller.GetStatus(CancellationToken.None);

        SetupStatusResponse response = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<SetupStatusResponse>().Subject;
        response.OrganizationName.Should().BeNull();
    }

    [Fact]
    public async Task GetStatus_WhenSetupNotRequired_DoesNotDiscloseAnOrganizationName()
    {
        _messageBus.InvokeAsync<bool>(Arg.Any<IsSetupRequiredQuery>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _organizationService
            .GetOrganizationsAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Organization("Wallow")]);

        ActionResult<SetupStatusResponse> result = await _controller.GetStatus(CancellationToken.None);

        SetupStatusResponse response = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<SetupStatusResponse>().Subject;
        response.OrganizationName.Should().BeNull();
        await _organizationService.DidNotReceive()
            .GetOrganizationsAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetStatus_WhenSetupNotRequired_ReturnsFalse()
    {
        _messageBus.InvokeAsync<bool>(Arg.Any<IsSetupRequiredQuery>(), Arg.Any<CancellationToken>())
            .Returns(false);

        ActionResult<SetupStatusResponse> result = await _controller.GetStatus(CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        SetupStatusResponse response = ok.Value.Should().BeOfType<SetupStatusResponse>().Subject;
        response.SetupRequired.Should().BeFalse();
    }

    #endregion

    #region CreateAdmin

    [Fact]
    public async Task CreateAdmin_WhenSetupNotRequired_ReturnsConflict()
    {
        _messageBus.InvokeAsync<bool>(Arg.Any<IsSetupRequiredQuery>(), Arg.Any<CancellationToken>())
            .Returns(false);

        CreateAdminRequest request = new("admin@test.com", "P@ssword1", "Admin", "User", "Acme Inc");

        IActionResult result = await _controller.CreateAdmin(request, CancellationToken.None);

        ConflictObjectResult conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.Value.Should().Be("Setup has already been completed.");
    }

    [Fact]
    public async Task CreateAdmin_WhenSetupRequired_AndCommandSucceeds_ReturnsNoContent()
    {
        _messageBus.InvokeAsync<bool>(Arg.Any<IsSetupRequiredQuery>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _messageBus.InvokeAsync<Result>(Arg.Any<BootstrapAdminCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        CreateAdminRequest request = new("admin@test.com", "P@ssword1", "Admin", "User", "Acme Inc");

        IActionResult result = await _controller.CreateAdmin(request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task CreateAdmin_ForwardsTheOrganizationNameToTheCommand()
    {
        _messageBus.InvokeAsync<bool>(Arg.Any<IsSetupRequiredQuery>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _messageBus.InvokeAsync<Result>(Arg.Any<BootstrapAdminCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        CreateAdminRequest request = new("admin@test.com", "P@ssword1", "Admin", "User", "Contoso");

        await _controller.CreateAdmin(request, CancellationToken.None);

        // Dropping it here would recreate the dead end this endpoint had: an administrator with
        // no organization holds no permission anywhere and never closes the setup gate.
        await _messageBus.Received(1).InvokeAsync<Result>(
            Arg.Is<BootstrapAdminCommand>(c => c.OrganizationName == "Contoso"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAdmin_WhenSetupRequired_AndCommandFails_ReturnsConflict()
    {
        _messageBus.InvokeAsync<bool>(Arg.Any<IsSetupRequiredQuery>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _messageBus.InvokeAsync<Result>(Arg.Any<BootstrapAdminCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new ErrorCatalogEntry("Admin.Exists", ErrorKind.BusinessRule, "Admin already exists")));

        CreateAdminRequest request = new("admin@test.com", "P@ssword1", "Admin", "User", "Acme Inc");

        IActionResult result = await _controller.CreateAdmin(request, CancellationToken.None);

        ConflictObjectResult conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.Value.Should().Be("Admin already exists");
    }

    #endregion
}
