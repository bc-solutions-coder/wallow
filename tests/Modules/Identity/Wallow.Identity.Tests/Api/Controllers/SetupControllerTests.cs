using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.Commands.RegisterSetupClient;
using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Identity.Tests.Api.Controllers;

public class SetupControllerTests
{
    private readonly IMessageBus _messageBus;
    private readonly SetupController _controller;

    public SetupControllerTests()
    {
        _messageBus = Substitute.For<IMessageBus>();
        _controller = new SetupController(_messageBus);
    }

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

        CreateAdminRequest request = new("admin@test.com", "P@ssword1", "Admin", "User");

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

        CreateAdminRequest request = new("admin@test.com", "P@ssword1", "Admin", "User");

        IActionResult result = await _controller.CreateAdmin(request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task CreateAdmin_WhenSetupRequired_AndCommandFails_ReturnsConflict()
    {
        _messageBus.InvokeAsync<bool>(Arg.Any<IsSetupRequiredQuery>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _messageBus.InvokeAsync<Result>(Arg.Any<BootstrapAdminCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new Error("Admin.Exists", "Admin already exists")));

        CreateAdminRequest request = new("admin@test.com", "P@ssword1", "Admin", "User");

        IActionResult result = await _controller.CreateAdmin(request, CancellationToken.None);

        ConflictObjectResult conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.Value.Should().Be("Admin already exists");
    }

    #endregion

    #region RegisterClient

    [Fact]
    public async Task RegisterClient_WhenSetupNotRequired_ReturnsConflict()
    {
        _messageBus.InvokeAsync<bool>(Arg.Any<IsSetupRequiredQuery>(), Arg.Any<CancellationToken>())
            .Returns(false);

        RegisterSetupClientRequest request = new("my-client", ["http://localhost:3000/callback"]);

        ActionResult<RegisterSetupClientResponse> result = await _controller.RegisterClient(request, CancellationToken.None);

        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task RegisterClient_WhenSetupRequired_AndCommandSucceeds_ReturnsOk()
    {
        _messageBus.InvokeAsync<bool>(Arg.Any<IsSetupRequiredQuery>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _messageBus.InvokeAsync<Result<RegisterSetupClientResult>>(
                Arg.Any<RegisterSetupClientCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new RegisterSetupClientResult("generated-secret")));

        RegisterSetupClientRequest request = new("my-client", ["http://localhost:3000/callback"]);

        ActionResult<RegisterSetupClientResponse> result = await _controller.RegisterClient(request, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        RegisterSetupClientResponse response = ok.Value.Should().BeOfType<RegisterSetupClientResponse>().Subject;
        response.ClientId.Should().Be("my-client");
        response.ClientSecret.Should().Be("generated-secret");
    }

    [Fact]
    public async Task RegisterClient_WhenSetupRequired_AndCommandFails_ReturnsConflict()
    {
        _messageBus.InvokeAsync<bool>(Arg.Any<IsSetupRequiredQuery>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _messageBus.InvokeAsync<Result<RegisterSetupClientResult>>(
                Arg.Any<RegisterSetupClientCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<RegisterSetupClientResult>(new Error("Client.Exists", "Client already exists")));

        RegisterSetupClientRequest request = new("my-client", ["http://localhost:3000/callback"]);

        ActionResult<RegisterSetupClientResponse> result = await _controller.RegisterClient(request, CancellationToken.None);

        ConflictObjectResult conflict = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.Value.Should().Be("Client already exists");
    }

    #endregion

    #region CompleteSetup

    [Fact]
    public async Task CompleteSetup_WhenSetupNotRequired_ReturnsConflict()
    {
        _messageBus.InvokeAsync<bool>(Arg.Any<IsSetupRequiredQuery>(), Arg.Any<CancellationToken>())
            .Returns(false);

        IActionResult result = await _controller.CompleteSetup(CancellationToken.None);

        ConflictObjectResult conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.Value.Should().Be("Setup has already been completed.");
    }

    [Fact]
    public async Task CompleteSetup_WhenSetupRequired_ReturnsNoContent()
    {
        _messageBus.InvokeAsync<bool>(Arg.Any<IsSetupRequiredQuery>(), Arg.Any<CancellationToken>())
            .Returns(true);

        IActionResult result = await _controller.CompleteSetup(CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    #endregion
}
