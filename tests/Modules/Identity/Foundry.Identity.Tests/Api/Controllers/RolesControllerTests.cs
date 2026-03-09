using System.Net;
using System.Net.Http.Json;
using Foundry.Identity.Api.Controllers;
using Foundry.Identity.Application.Interfaces;
using Foundry.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Foundry.Identity.Tests.Api.Controllers;

public class RolesControllerTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRolePermissionLookup _rolePermissionLookup;
    private readonly RolesController _controller;

    public RolesControllerTests()
    {
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _rolePermissionLookup = Substitute.For<IRolePermissionLookup>();
        _controller = new RolesController(_httpClientFactory, _rolePermissionLookup);
    }

    #region GetRoles

    [Fact]
    public async Task GetRoles_WithValidResponse_ReturnsFilteredRoles()
    {
        List<object> keycloakRoles =
        [
            new { Name = "admin", Description = "Admin role" },
            new { Name = "user", Description = "User role" },
            new { Name = "uma_protection", Description = "UMA" },
            new { Name = "offline_access", Description = "Offline" },
            new { Name = "default-roles-foundry", Description = "Default" }
        ];
        using HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(keycloakRoles)
        };
        using FakeHttpMessageHandler handler = new(response);
        using HttpClient client = new(handler);
        client.BaseAddress = new Uri("https://keycloak");
        _httpClientFactory.CreateClient("KeycloakAdminClient").Returns(client);

        ActionResult result = await _controller.GetRoles(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IEnumerable<object>? roles = ok.Value as IEnumerable<object>;
        roles.Should().NotBeNull();
    }

    #endregion

    #region GetRolePermissions

    [Fact]
    public void GetRolePermissions_ReturnsPermissionsForRole()
    {
        IReadOnlyCollection<string> permissions = new[] { PermissionType.UsersRead, PermissionType.UsersCreate };
        _rolePermissionLookup.GetPermissions(Arg.Is<IEnumerable<string>>(r => r.Contains("admin")))
            .Returns(permissions);

        ActionResult result = _controller.GetRolePermissions("admin");

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }

    [Fact]
    public void GetRolePermissions_WithNoPermissions_ReturnsEmptyList()
    {
        _rolePermissionLookup.GetPermissions(Arg.Any<IEnumerable<string>>())
            .Returns([]);

        ActionResult result = _controller.GetRolePermissions("guest");

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }

    #endregion
}

internal sealed class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(response);
    }
}
