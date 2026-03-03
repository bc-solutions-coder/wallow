using System.Security.Claims;
using Foundry.Identity.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Foundry.Identity.Tests.Infrastructure;

public class PermissionAuthorizationHandlerTests
{
    private readonly PermissionAuthorizationHandler _handler = new();

    [Fact]
    public async Task HandleRequirementAsync_UserHasPermission_Succeeds()
    {
        ClaimsPrincipal user = new(new ClaimsIdentity(new[]
        {
            new Claim("permission", "UsersRead"),
            new Claim("permission", "UsersCreate")
        }, "test"));

        PermissionRequirement requirement = new("UsersRead");
        AuthorizationHandlerContext context = new(
            new[] { requirement }, user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_UserLacksPermission_DoesNotSucceed()
    {
        ClaimsPrincipal user = new(new ClaimsIdentity(new[]
        {
            new Claim("permission", "UsersRead")
        }, "test"));

        PermissionRequirement requirement = new("UsersCreate");
        AuthorizationHandlerContext context = new(
            new[] { requirement }, user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_UserHasNoPermissions_DoesNotSucceed()
    {
        ClaimsPrincipal user = new(new ClaimsIdentity(Array.Empty<Claim>(), "test"));

        PermissionRequirement requirement = new("UsersRead");
        AuthorizationHandlerContext context = new(
            new[] { requirement }, user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }
}
