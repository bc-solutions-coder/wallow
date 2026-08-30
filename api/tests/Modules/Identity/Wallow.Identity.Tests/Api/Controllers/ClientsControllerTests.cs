using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Infrastructure.Extensions;

#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()

namespace Wallow.Identity.Tests.Api.Controllers;

public class ClientsControllerTests
{
    private static readonly Guid _testUserId = Guid.NewGuid();

    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly ClientsController _controller;

    public ClientsControllerTests()
    {
        _applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        _controller = new ClientsController(_applicationManager);

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
    public async Task GetById_ReportsTheScopesTheClientMayRequest()
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
                descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
                descriptor.Permissions.Add("scp:openid");
                descriptor.Permissions.Add("scp:storage.read");
                return ValueTask.CompletedTask;
            });

        ActionResult<ClientResponse> result = await _controller.GetById("id-1", CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ClientResponse client = ok.Value.Should().BeOfType<ClientResponse>().Subject;
        client.Scopes.Should().BeEquivalentTo("openid", "storage.read");
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

    [Theory]
    [InlineData("http://app.example.com/callback")]
    [InlineData("https://app.example.com/callback#fragment")]
    [InlineData("/relative/callback")]
    public async Task Create_WithARefusedRedirectUri_ReturnsValidationProblem(string redirectUri)
    {
        CreateClientRequest request = new("My App", [redirectUri], ["https://example.com/logout"]);

        ActionResult<ClientResponse> result = await _controller.Create(request, CancellationToken.None);

        ObjectResult problem = result.Result.Should().BeOfType<ObjectResult>().Subject;
        ValidationProblemDetails details = problem.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        details.Errors.Should().ContainKey(nameof(CreateClientRequest.RedirectUris));
        await _applicationManager.DidNotReceive()
            .CreateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithLoopbackHttpRedirectUri_IsAccepted()
    {
        StubCreate();
        CreateClientRequest request = new("Local App", ["http://localhost:3000/callback"], []);

        ActionResult<ClientResponse> result = await _controller.Create(request, CancellationToken.None);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task Create_WithNoScopesRequested_PermitsTheSignInBaseline()
    {
        List<OpenIddictApplicationDescriptor> captured = StubCreate();

        CreateClientRequest request = new(
            "My App",
            ["https://example.com/callback"],
            ["https://example.com/logout"]);

        ActionResult<ClientResponse> result = await _controller.Create(request, CancellationToken.None);

        ClientResponse client = result.Result.Should().BeOfType<CreatedAtActionResult>()
            .Which.Value.Should().BeOfType<ClientResponse>().Subject;
        client.Scopes.Should().BeEquivalentTo("openid", "profile", "email", "roles", "offline_access");
        ScopePermissionsOf(captured.Single()).Should().BeEquivalentTo(
            "scp:openid", "scp:profile", "scp:email", "scp:roles", "scp:offline_access");
    }

    [Fact]
    public async Task Create_WithScopesRequested_PermitsExactlyThose()
    {
        List<OpenIddictApplicationDescriptor> captured = StubCreate();

        CreateClientRequest request = new(
            "My App",
            ["https://example.com/callback"],
            ["https://example.com/logout"],
            Scopes: ["openid", "storage.read"]);

        ActionResult<ClientResponse> result = await _controller.Create(request, CancellationToken.None);

        ClientResponse client = result.Result.Should().BeOfType<CreatedAtActionResult>()
            .Which.Value.Should().BeOfType<ClientResponse>().Subject;
        client.Scopes.Should().BeEquivalentTo("openid", "storage.read");
        ScopePermissionsOf(captured.Single()).Should().BeEquivalentTo("scp:openid", "scp:storage.read");
    }

    [Fact]
    public async Task Create_WithAScopeTheServerDoesNotIssue_ReturnsValidationProblem()
    {
        CreateClientRequest request = new(
            "My App",
            ["https://example.com/callback"],
            ["https://example.com/logout"],
            Scopes: ["openid", "billing.read"]);

        ActionResult<ClientResponse> result = await _controller.Create(request, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.Value.Should().BeOfType<ValidationProblemDetails>();
        await _applicationManager.DidNotReceive()
            .CreateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>());
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

    #region Front-Channel Logout URI

    [Fact]
    public async Task Create_WithFrontchannelLogoutUri_SetsItOnTheDescriptorAndEchoesIt()
    {
        List<OpenIddictApplicationDescriptor> captured = StubCreate();

        CreateClientRequest request = new(
            "My App",
            ["https://example.com/callback"],
            ["https://example.com/logout"],
            FrontchannelLogoutUri: "https://example.com/bff/frontchannel-logout");

        ActionResult<ClientResponse> result = await _controller.Create(request, CancellationToken.None);

        CreatedAtActionResult created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        ClientResponse client = created.Value.Should().BeOfType<ClientResponse>().Subject;
        client.FrontchannelLogoutUri.Should().Be("https://example.com/bff/frontchannel-logout");

        captured.Should().ContainSingle();
        captured[0].GetFrontchannelLogoutUri()!.AbsoluteUri
            .Should().Be("https://example.com/bff/frontchannel-logout");
    }

    [Fact]
    public async Task Create_WithoutFrontchannelLogoutUri_LeavesTheDescriptorWithoutOne()
    {
        List<OpenIddictApplicationDescriptor> captured = StubCreate();

        CreateClientRequest request = new(
            "My App",
            ["https://example.com/callback"],
            ["https://example.com/logout"]);

        ActionResult<ClientResponse> result = await _controller.Create(request, CancellationToken.None);

        CreatedAtActionResult created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        ClientResponse client = created.Value.Should().BeOfType<ClientResponse>().Subject;
        client.FrontchannelLogoutUri.Should().BeNull();

        captured.Should().ContainSingle();
        captured[0].GetFrontchannelLogoutUri().Should().BeNull();
    }

    [Theory]
    [InlineData("/bff/frontchannel-logout")]
    [InlineData("not a uri")]
    [InlineData("javascript:alert(1)")]
    public async Task Create_WithAFrontchannelLogoutUriThatIsNotAbsoluteHttp_ReturnsValidationProblem(string uri)
    {
        // The OP loads this URI in an iframe on its own logout page, so anything that is not an
        // absolute http(s) location is either unloadable or a script-injection vector.
        CreateClientRequest request = new(
            "My App",
            ["https://example.com/callback"],
            ["https://example.com/logout"],
            FrontchannelLogoutUri: uri);

        ActionResult<ClientResponse> result = await _controller.Create(request, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.Value.Should().BeOfType<ValidationProblemDetails>();
        await _applicationManager.DidNotReceive()
            .CreateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithFrontchannelLogoutUri_SetsItOnTheDescriptor()
    {
        object app = new object();
        OpenIddictApplicationDescriptor? captured = null;
        _applicationManager.FindByIdAsync("id-1", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(app));
        _applicationManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app, Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);
        _applicationManager.UpdateAsync(app, Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.ArgAt<OpenIddictApplicationDescriptor>(1);
                return ValueTask.CompletedTask;
            });
        _applicationManager.GetClientIdAsync(app, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("client-1"));

        UpdateClientRequest request = new(
            "Updated Name",
            ["https://new.example.com/callback"],
            ["https://new.example.com/logout"],
            FrontchannelLogoutUri: "https://new.example.com/bff/frontchannel-logout");

        ActionResult<ClientResponse> result = await _controller.Update("id-1", request, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ClientResponse client = ok.Value.Should().BeOfType<ClientResponse>().Subject;
        client.FrontchannelLogoutUri.Should().Be("https://new.example.com/bff/frontchannel-logout");

        captured.Should().NotBeNull();
        captured!.GetFrontchannelLogoutUri()!.AbsoluteUri
            .Should().Be("https://new.example.com/bff/frontchannel-logout");
    }

    [Fact]
    public async Task Update_WithoutFrontchannelLogoutUri_ClearsAnExistingOne()
    {
        // Update is a full replace of the client's registration, matching how the redirect URI
        // lists behave: omitting the field un-registers the RP from logout notifications.
        object app = new object();
        OpenIddictApplicationDescriptor? captured = null;
        _applicationManager.FindByIdAsync("id-1", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(app));
        _applicationManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                OpenIddictApplicationDescriptor descriptor = callInfo.ArgAt<OpenIddictApplicationDescriptor>(0);
                descriptor.SetFrontchannelLogoutUri(new Uri("https://old.example.com/bff/frontchannel-logout"));
                return ValueTask.CompletedTask;
            });
        _applicationManager.UpdateAsync(app, Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.ArgAt<OpenIddictApplicationDescriptor>(1);
                return ValueTask.CompletedTask;
            });
        _applicationManager.GetClientIdAsync(app, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("client-1"));

        UpdateClientRequest request = new("Updated Name", [], []);

        await _controller.Update("id-1", request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.GetFrontchannelLogoutUri().Should().BeNull();
    }

    [Fact]
    public async Task GetById_ReportsTheFrontchannelLogoutUri()
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
                descriptor.DisplayName = "App One";
                descriptor.SetFrontchannelLogoutUri(new Uri("https://example.com/bff/frontchannel-logout"));
                return ValueTask.CompletedTask;
            });

        ActionResult<ClientResponse> result = await _controller.GetById("id-1", CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ClientResponse client = ok.Value.Should().BeOfType<ClientResponse>().Subject;
        client.FrontchannelLogoutUri.Should().Be("https://example.com/bff/frontchannel-logout");
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

    private static IEnumerable<string> ScopePermissionsOf(OpenIddictApplicationDescriptor descriptor) =>
        descriptor.Permissions.Where(p =>
            p.StartsWith(OpenIddictConstants.Permissions.Prefixes.Scope, StringComparison.Ordinal));

    private List<OpenIddictApplicationDescriptor> StubCreate()
    {
        List<OpenIddictApplicationDescriptor> captured = [];
        object createdApp = new object();

        _applicationManager.CreateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured.Add(callInfo.ArgAt<OpenIddictApplicationDescriptor>(0));
                return ValueTask.FromResult(createdApp);
            });
        _applicationManager.GetIdAsync(createdApp, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("new-id"));

        return captured;
    }

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
