using Foundry.Identity.Api.Controllers;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;

namespace Foundry.Identity.Tests.Api.Controllers;

public class ScimControllerTests
{
    private readonly IScimService _scimService;
    private readonly ScimController _controller;

    public ScimControllerTests()
    {
        _scimService = Substitute.For<IScimService>();
        ILogger<ScimController> logger = Substitute.For<ILogger<ScimController>>();
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Development");
        _controller = new ScimController(_scimService, logger, environment);
    }

    #region ListUsers

    [Fact]
    public async Task ListUsers_ReturnsOkWithScimListResponse()
    {
        ScimListResponse<ScimUser> listResponse = new()
        {
            TotalResults = 1,
            StartIndex = 1,
            ItemsPerPage = 100,
            Resources = new List<ScimUser> { CreateScimUser("user-1") }
        };
        _scimService.ListUsersAsync(Arg.Any<ScimListRequest>(), Arg.Any<CancellationToken>())
            .Returns(listResponse);

        ActionResult<ScimListResponse<ScimUser>> result = await _controller.ListUsers(null, 1, 100, null, null, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ScimListResponse<ScimUser> response = ok.Value.Should().BeOfType<ScimListResponse<ScimUser>>().Subject;
        response.TotalResults.Should().Be(1);
    }

    [Fact]
    public async Task ListUsers_PassesParametersToService()
    {
        ScimListResponse<ScimUser> listResponse = new() { Resources = new List<ScimUser>() };
        ScimListRequest? capturedRequest = null;
        _scimService.ListUsersAsync(Arg.Do<ScimListRequest>(r => capturedRequest = r), Arg.Any<CancellationToken>())
            .Returns(listResponse);

        await _controller.ListUsers("userName eq \"alice\"", 5, 50, "userName", "ascending", CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Filter.Should().Be("userName eq \"alice\"");
        capturedRequest.StartIndex.Should().Be(5);
        capturedRequest.Count.Should().Be(50);
    }

    #endregion

    #region GetScimUser

    [Fact]
    public async Task GetScimUser_WhenFound_ReturnsOk()
    {
        ScimUser user = CreateScimUser("user-1");
        _scimService.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(user);

        ActionResult<ScimUser> result = await _controller.GetScimUser("user-1", CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ScimUser>();
    }

    [Fact]
    public async Task GetScimUser_WhenNotFound_ReturnsNotFoundWithScimError()
    {
        _scimService.GetUserAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((ScimUser?)null);

        ActionResult<ScimUser> result = await _controller.GetScimUser("nonexistent", CancellationToken.None);

        NotFoundObjectResult notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        ScimError error = notFound.Value.Should().BeOfType<ScimError>().Subject;
        error.Status.Should().Be(404);
        error.Detail.Should().Contain("nonexistent");
    }

    #endregion

    #region CreateUser

    [Fact]
    public async Task CreateUser_WithValidRequest_ReturnsCreated()
    {
        ScimUserRequest request = new();
        ScimUser createdUser = CreateScimUser("new-user");
        _scimService.CreateUserAsync(Arg.Any<ScimUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(createdUser);

        ActionResult<ScimUser> result = await _controller.CreateUser(request, CancellationToken.None);

        CreatedResult created = result.Result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be("/scim/v2/Users/new-user");
    }

    [Fact]
    public async Task CreateUser_WhenServiceThrows_ReturnsBadRequest()
    {
        ScimUserRequest request = new();
        _scimService.CreateUserAsync(Arg.Any<ScimUserRequest>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Duplicate user"));

        ActionResult<ScimUser> result = await _controller.CreateUser(request, CancellationToken.None);

        BadRequestObjectResult badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ScimError error = badRequest.Value.Should().BeOfType<ScimError>().Subject;
        error.Status.Should().Be(400);
        error.ScimType.Should().Be("invalidValue");
        error.Detail.Should().Be("Duplicate user");
    }

    #endregion

    #region UpdateUser

    [Fact]
    public async Task UpdateUser_WhenSuccess_ReturnsOk()
    {
        ScimUserRequest request = new();
        ScimUser updatedUser = CreateScimUser("user-1");
        _scimService.UpdateUserAsync("user-1", Arg.Any<ScimUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(updatedUser);

        ActionResult<ScimUser> result = await _controller.UpdateUser("user-1", request, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateUser_WhenServiceThrows_ReturnsBadRequest()
    {
        ScimUserRequest request = new();
        _scimService.UpdateUserAsync("user-1", Arg.Any<ScimUserRequest>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Invalid update"));

        ActionResult<ScimUser> result = await _controller.UpdateUser("user-1", request, CancellationToken.None);

        BadRequestObjectResult badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ScimError error = badRequest.Value.Should().BeOfType<ScimError>().Subject;
        error.Detail.Should().Be("Invalid update");
    }

    #endregion

    #region PatchUser

    [Fact]
    public async Task PatchUser_WhenSuccess_ReturnsOk()
    {
        ScimPatchRequest request = new();
        ScimUser patchedUser = CreateScimUser("user-1");
        _scimService.PatchUserAsync("user-1", Arg.Any<ScimPatchRequest>(), Arg.Any<CancellationToken>())
            .Returns(patchedUser);

        ActionResult<ScimUser> result = await _controller.PatchUser("user-1", request, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task PatchUser_WhenServiceThrows_ReturnsBadRequest()
    {
        ScimPatchRequest request = new();
        _scimService.PatchUserAsync("user-1", Arg.Any<ScimPatchRequest>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Patch failed"));

        ActionResult<ScimUser> result = await _controller.PatchUser("user-1", request, CancellationToken.None);

        BadRequestObjectResult badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ScimError error = badRequest.Value.Should().BeOfType<ScimError>().Subject;
        error.Detail.Should().Be("Patch failed");
    }

    #endregion

    #region DeleteUser

    [Fact]
    public async Task DeleteUser_WhenSuccess_ReturnsNoContent()
    {
        IActionResult result = await _controller.DeleteUser("user-1", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _scimService.Received(1).DeleteUserAsync("user-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteUser_WhenServiceThrows_ReturnsBadRequest()
    {
        _scimService.DeleteUserAsync("user-1", Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Cannot delete"));

        IActionResult result = await _controller.DeleteUser("user-1", CancellationToken.None);

        BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ScimError error = badRequest.Value.Should().BeOfType<ScimError>().Subject;
        error.Detail.Should().Be("Cannot delete");
    }

    #endregion

    #region ListGroups

    [Fact]
    public async Task ListGroups_ReturnsOkWithGroups()
    {
        ScimListResponse<ScimGroup> listResponse = new()
        {
            TotalResults = 0,
            Resources = new List<ScimGroup>()
        };
        _scimService.ListGroupsAsync(Arg.Any<ScimListRequest>(), Arg.Any<CancellationToken>())
            .Returns(listResponse);

        ActionResult<ScimListResponse<ScimGroup>> result = await _controller.ListGroups(null, 1, 100, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region GetGroup

    [Fact]
    public async Task GetGroup_WhenFound_ReturnsOk()
    {
        ScimGroup group = new() { Id = "group-1", DisplayName = "Test Group" };
        _scimService.GetGroupAsync("group-1", Arg.Any<CancellationToken>())
            .Returns(group);

        ActionResult<ScimGroup> result = await _controller.GetGroup("group-1", CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ScimGroup>();
    }

    [Fact]
    public async Task GetGroup_WhenNotFound_ReturnsNotFoundWithScimError()
    {
        _scimService.GetGroupAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((ScimGroup?)null);

        ActionResult<ScimGroup> result = await _controller.GetGroup("nonexistent", CancellationToken.None);

        NotFoundObjectResult notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        ScimError error = notFound.Value.Should().BeOfType<ScimError>().Subject;
        error.Status.Should().Be(404);
        error.Detail.Should().Contain("nonexistent");
    }

    #endregion

    #region CreateGroup

    [Fact]
    public async Task CreateGroup_WhenSuccess_ReturnsCreated()
    {
        ScimGroupRequest request = new();
        ScimGroup group = new() { Id = "group-new", DisplayName = "Developers" };
        _scimService.CreateGroupAsync(Arg.Any<ScimGroupRequest>(), Arg.Any<CancellationToken>())
            .Returns(group);

        ActionResult<ScimGroup> result = await _controller.CreateGroup(request, CancellationToken.None);

        CreatedResult created = result.Result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be("/scim/v2/Groups/group-new");
    }

    [Fact]
    public async Task CreateGroup_WhenServiceThrows_ReturnsBadRequest()
    {
        ScimGroupRequest request = new();
        _scimService.CreateGroupAsync(Arg.Any<ScimGroupRequest>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Group exists"));

        ActionResult<ScimGroup> result = await _controller.CreateGroup(request, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region UpdateGroup

    [Fact]
    public async Task UpdateGroup_WhenSuccess_ReturnsOk()
    {
        ScimGroupRequest request = new();
        ScimGroup group = new() { Id = "group-1" };
        _scimService.UpdateGroupAsync("group-1", Arg.Any<ScimGroupRequest>(), Arg.Any<CancellationToken>())
            .Returns(group);

        ActionResult<ScimGroup> result = await _controller.UpdateGroup("group-1", request, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateGroup_WhenServiceThrows_ReturnsBadRequest()
    {
        ScimGroupRequest request = new();
        _scimService.UpdateGroupAsync("group-1", Arg.Any<ScimGroupRequest>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Update failed"));

        ActionResult<ScimGroup> result = await _controller.UpdateGroup("group-1", request, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region DeleteGroup

    [Fact]
    public async Task DeleteGroup_WhenSuccess_ReturnsNoContent()
    {
        IActionResult result = await _controller.DeleteGroup("group-1", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _scimService.Received(1).DeleteGroupAsync("group-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteGroup_WhenServiceThrows_ReturnsBadRequest()
    {
        _scimService.DeleteGroupAsync("group-1", Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Delete failed"));

        IActionResult result = await _controller.DeleteGroup("group-1", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region GetServiceProviderConfig

    [Fact]
    public void GetServiceProviderConfig_ReturnsOkWithConfig()
    {
        ActionResult<ScimServiceProviderConfig> result = _controller.GetServiceProviderConfig();

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ScimServiceProviderConfig config = ok.Value.Should().BeOfType<ScimServiceProviderConfig>().Subject;
        config.Patch!.Supported.Should().BeTrue();
        config.Bulk!.Supported.Should().BeFalse();
        config.Filter!.Supported.Should().BeTrue();
        config.Filter.MaxResults.Should().Be(200);
        config.ChangePassword!.Supported.Should().BeFalse();
        config.Sort!.Supported.Should().BeTrue();
        config.Etag!.Supported.Should().BeFalse();
        config.DocumentationUri.Should().Be("https://docs.foundry.dev/scim");
        config.AuthenticationSchemes.Should().HaveCount(1);
        config.AuthenticationSchemes![0].Type.Should().Be("oauthbearertoken");
        config.AuthenticationSchemes[0].Primary.Should().BeTrue();
    }

    #endregion

    #region GetSchemas

    [Fact]
    public void GetSchemas_ReturnsOkWithSchemas()
    {
        ActionResult<ScimListResponse<ScimSchema>> result = _controller.GetSchemas();

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ScimListResponse<ScimSchema> response = ok.Value.Should().BeOfType<ScimListResponse<ScimSchema>>().Subject;
        response.TotalResults.Should().Be(2);
        response.Resources.Should().Contain(s => s.Name == "User");
        response.Resources.Should().Contain(s => s.Name == "Group");
    }

    #endregion

    #region GetResourceTypes

    [Fact]
    public void GetResourceTypes_ReturnsOkWithResourceTypes()
    {
        ActionResult<ScimListResponse<ScimResourceType>> result = _controller.GetResourceTypes();

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ScimListResponse<ScimResourceType> response = ok.Value.Should().BeOfType<ScimListResponse<ScimResourceType>>().Subject;
        response.TotalResults.Should().Be(2);
        response.Resources.Should().Contain(r => r.Name == "User" && r.Endpoint == "/Users");
        response.Resources.Should().Contain(r => r.Name == "Group" && r.Endpoint == "/Groups");
    }

    #endregion

    #region Helpers

    private static ScimUser CreateScimUser(string id)
    {
        return new ScimUser { Id = id, UserName = $"{id}@test.com" };
    }

    #endregion
}
