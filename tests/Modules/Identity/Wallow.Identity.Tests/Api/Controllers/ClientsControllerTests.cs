using System.Security.Claims;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()

namespace Wallow.Identity.Tests.Api.Controllers;

public class ClientsControllerTests
{
    private static readonly Guid _testUserId = Guid.NewGuid();
    private static readonly Guid _testOrgId = Guid.NewGuid();

    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOrganizationService _organizationService;
    private readonly ClientsController _controller;

    public ClientsControllerTests()
    {
        _applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        _organizationService = Substitute.For<IOrganizationService>();
        _controller = new ClientsController(_applicationManager, _organizationService);

        ClaimsPrincipal user = new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _testUserId.ToString())
        }, "test"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    #region GetAll

    [Fact]
    public async Task GetAll_ReturnsOkWithClients()
    {
        object app1 = new object();
        object app2 = new object();

        _applicationManager.ListAsync(int.MaxValue, 0, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(app1, app2));

        _applicationManager.GetIdAsync(app1, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("id-1"));
        _applicationManager.GetClientIdAsync(app1, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("client-1"));
        _applicationManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app1, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                OpenIddictApplicationDescriptor descriptor = callInfo.ArgAt<OpenIddictApplicationDescriptor>(0);
                descriptor.DisplayName = "App One";
                descriptor.RedirectUris.Add(new Uri("https://example.com/callback"));
                descriptor.PostLogoutRedirectUris.Add(new Uri("https://example.com/logout"));
                return ValueTask.CompletedTask;
            });

        _applicationManager.GetIdAsync(app2, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("id-2"));
        _applicationManager.GetClientIdAsync(app2, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("client-2"));
        _applicationManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app2, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                OpenIddictApplicationDescriptor descriptor = callInfo.ArgAt<OpenIddictApplicationDescriptor>(0);
                descriptor.DisplayName = "App Two";
                return ValueTask.CompletedTask;
            });

        ActionResult<IReadOnlyList<ClientResponse>> result = await _controller.GetAll(CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        List<ClientResponse> clients = ok.Value.Should().BeOfType<List<ClientResponse>>().Subject;
        clients.Should().HaveCount(2);
        clients[0].Id.Should().Be("id-1");
        clients[0].ClientId.Should().Be("client-1");
        clients[0].Name.Should().Be("App One");
        clients[0].RedirectUris.Should().ContainSingle("https://example.com/callback");
        clients[1].Id.Should().Be("id-2");
        clients[1].Name.Should().Be("App Two");
    }

    [Fact]
    public async Task GetAll_WhenEmpty_ReturnsEmptyList()
    {
        _applicationManager.ListAsync(int.MaxValue, 0, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable());

        ActionResult<IReadOnlyList<ClientResponse>> result = await _controller.GetAll(CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        List<ClientResponse> clients = ok.Value.Should().BeOfType<List<ClientResponse>>().Subject;
        clients.Should().BeEmpty();
    }

    #endregion

    #region GetById

    [Fact]
    public async Task GetById_WhenExists_ReturnsOkWithClient()
    {
        object app = new object();
        _applicationManager.FindByIdAsync("id-1", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(app));
        _applicationManager.GetClientIdAsync(app, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("client-1"));
        _applicationManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                OpenIddictApplicationDescriptor descriptor = callInfo.ArgAt<OpenIddictApplicationDescriptor>(0);
                descriptor.DisplayName = "Test App";
                descriptor.RedirectUris.Add(new Uri("https://example.com/callback"));
                return ValueTask.CompletedTask;
            });

        ActionResult<ClientResponse> result = await _controller.GetById("id-1", CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ClientResponse client = ok.Value.Should().BeOfType<ClientResponse>().Subject;
        client.Id.Should().Be("id-1");
        client.ClientId.Should().Be("client-1");
        client.Name.Should().Be("Test App");
        client.RedirectUris.Should().ContainSingle("https://example.com/callback");
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        _applicationManager.FindByIdAsync("missing", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(null));

        ActionResult<ClientResponse> result = await _controller.GetById("missing", CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Create

    [Fact]
    public async Task Create_ReturnsCreatedWithClientAndSecret()
    {
        object createdApp = new object();
        _applicationManager.CreateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(createdApp));
        _applicationManager.GetIdAsync(createdApp, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("new-id"));

        CreateClientRequest request = new(
            "My App",
            ["https://example.com/callback"],
            ["https://example.com/logout"]);

        ActionResult<ClientResponse> result = await _controller.Create(request, CancellationToken.None);

        CreatedAtActionResult created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(ClientsController.GetById));
        ClientResponse client = created.Value.Should().BeOfType<ClientResponse>().Subject;
        client.Id.Should().Be("new-id");
        client.Name.Should().Be("My App");
        client.ClientId.Should().NotBeNullOrEmpty();
        client.ClientSecret.Should().NotBeNullOrEmpty();
        client.RedirectUris.Should().ContainSingle("https://example.com/callback");
        client.PostLogoutRedirectUris.Should().ContainSingle("https://example.com/logout");

        await _applicationManager.Received(1)
            .CreateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithTenantId_WhenMember_SetsTenantIdOnDescriptor()
    {
        _organizationService.GetUserOrganizationsAsync(_testUserId, Arg.Any<CancellationToken>())
            .Returns([new OrganizationDto(_testOrgId, "Test Org", null, 1)]);

        OpenIddictApplicationDescriptor? capturedDescriptor = null;
        object createdApp = new object();
        _applicationManager.CreateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedDescriptor = callInfo.ArgAt<OpenIddictApplicationDescriptor>(0);
                return ValueTask.FromResult(createdApp);
            });
        _applicationManager.GetIdAsync(createdApp, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("new-id"));

        CreateClientRequest request = new(
            "Tenant App",
            ["https://example.com/callback"],
            ["https://example.com/logout"],
            _testOrgId);

        ActionResult<ClientResponse> result = await _controller.Create(request, CancellationToken.None);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        capturedDescriptor.Should().NotBeNull();
        capturedDescriptor!.GetTenantId().Should().Be(_testOrgId.ToString());
    }

    [Fact]
    public async Task Create_WithTenantId_WhenNotMember_ReturnsForbid()
    {
        _organizationService.GetUserOrganizationsAsync(_testUserId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto>());

        CreateClientRequest request = new(
            "Tenant App",
            ["https://example.com/callback"],
            ["https://example.com/logout"],
            _testOrgId);

        ActionResult<ClientResponse> result = await _controller.Create(request, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
        await _applicationManager.DidNotReceive()
            .CreateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetByTenant

    [Fact]
    public async Task GetByTenant_WhenMember_ReturnsMatchingClients()
    {
        _organizationService.GetUserOrganizationsAsync(_testUserId, Arg.Any<CancellationToken>())
            .Returns([new OrganizationDto(_testOrgId, "Test Org", null, 1)]);

        object app1 = new object();
        object app2 = new object();

        _applicationManager.ListAsync(int.MaxValue, 0, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(app1, app2));

        _applicationManager.GetIdAsync(app1, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("id-1"));
        _applicationManager.GetClientIdAsync(app1, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("client-1"));
        _applicationManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app1, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                OpenIddictApplicationDescriptor descriptor = callInfo.ArgAt<OpenIddictApplicationDescriptor>(0);
                descriptor.DisplayName = "Tenant App";
                descriptor.SetTenantId(_testOrgId.ToString());
                return ValueTask.CompletedTask;
            });

        // app2 has no tenant - should be filtered out
        _applicationManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app2, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                OpenIddictApplicationDescriptor descriptor = callInfo.ArgAt<OpenIddictApplicationDescriptor>(0);
                descriptor.DisplayName = "Other App";
                return ValueTask.CompletedTask;
            });

        ActionResult<IReadOnlyList<ClientResponse>> result = await _controller.GetByTenant(_testOrgId, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        List<ClientResponse> clients = ok.Value.Should().BeOfType<List<ClientResponse>>().Subject;
        clients.Should().HaveCount(1);
        clients[0].Id.Should().Be("id-1");
        clients[0].Name.Should().Be("Tenant App");
    }

    [Fact]
    public async Task GetByTenant_WhenNotMember_ReturnsForbid()
    {
        _organizationService.GetUserOrganizationsAsync(_testUserId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto>());

        ActionResult<IReadOnlyList<ClientResponse>> result = await _controller.GetByTenant(_testOrgId, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    #endregion

    #region Update

    [Fact]
    public async Task Update_WhenExists_ReturnsOkWithUpdatedClient()
    {
        object app = new object();
        _applicationManager.FindByIdAsync("id-1", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(app));
        _applicationManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app, Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);
        _applicationManager.GetClientIdAsync(app, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("client-1"));

        UpdateClientRequest request = new(
            "Updated Name",
            ["https://new.example.com/callback"],
            ["https://new.example.com/logout"]);

        ActionResult<ClientResponse> result = await _controller.Update("id-1", request, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ClientResponse client = ok.Value.Should().BeOfType<ClientResponse>().Subject;
        client.Id.Should().Be("id-1");
        client.Name.Should().Be("Updated Name");
        client.RedirectUris.Should().ContainSingle("https://new.example.com/callback");
        client.PostLogoutRedirectUris.Should().ContainSingle("https://new.example.com/logout");

        await _applicationManager.Received(1)
            .UpdateAsync(app, Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WhenNotFound_ReturnsNotFound()
    {
        _applicationManager.FindByIdAsync("missing", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(null));

        UpdateClientRequest request = new("Name", [], []);

        ActionResult<ClientResponse> result = await _controller.Update("missing", request, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Delete

    [Fact]
    public async Task Delete_WhenExists_ReturnsNoContent()
    {
        object app = new object();
        _applicationManager.FindByIdAsync("id-1", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(app));

        ActionResult result = await _controller.Delete("id-1", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _applicationManager.Received(1).DeleteAsync(app, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WhenNotFound_ReturnsNotFound()
    {
        _applicationManager.FindByIdAsync("missing", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(null));

        ActionResult result = await _controller.Delete("missing", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region RotateSecret

    [Fact]
    public async Task RotateSecret_WhenExists_ReturnsOkWithNewSecret()
    {
        object app = new object();
        _applicationManager.FindByIdAsync("id-1", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(app));
        _applicationManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                OpenIddictApplicationDescriptor descriptor = callInfo.ArgAt<OpenIddictApplicationDescriptor>(0);
                descriptor.DisplayName = "Test App";
                descriptor.ClientSecret = "old-secret";
                return ValueTask.CompletedTask;
            });
        _applicationManager.GetClientIdAsync(app, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("client-1"));

        ActionResult<ClientResponse> result = await _controller.RotateSecret("id-1", CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ClientResponse client = ok.Value.Should().BeOfType<ClientResponse>().Subject;
        client.Id.Should().Be("id-1");
        client.ClientSecret.Should().NotBeNullOrEmpty();
        client.ClientSecret.Should().NotBe("old-secret");

        await _applicationManager.Received(1)
            .UpdateAsync(app, Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RotateSecret_WhenNotFound_ReturnsNotFound()
    {
        _applicationManager.FindByIdAsync("missing", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(null));

        ActionResult<ClientResponse> result = await _controller.RotateSecret("missing", CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Helpers

    private static async IAsyncEnumerable<object> ToAsyncEnumerable(params object[] items)
    {
        foreach (object item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    #endregion
}
