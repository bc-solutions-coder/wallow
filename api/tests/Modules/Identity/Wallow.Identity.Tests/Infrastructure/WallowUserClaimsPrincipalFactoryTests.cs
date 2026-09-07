using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Checks that the auth-cookie principal has no organization ID or organization roles.
/// </summary>
public sealed class WallowUserClaimsPrincipalFactoryTests
{
    private readonly UserManager<WallowUser> _userManager;
    private readonly WallowUserClaimsPrincipalFactory _sut;

    public WallowUserClaimsPrincipalFactoryTests()
    {
        IUserStore<WallowUser> userStore = Substitute.For<IUserStore<WallowUser>>();
        _userManager = Substitute.For<UserManager<WallowUser>>(
            userStore, null, null, null, null, null, null, null, null);
        _userManager.SupportsUserRole.Returns(true);

        _sut = new WallowUserClaimsPrincipalFactory(
            _userManager, Options.Create(new IdentityOptions()));
    }

    [Fact]
    public async Task CreateAsync_ForAnyUser_StampsNoRoleClaim()
    {
        WallowUser user = ArrangeUser();

        ClaimsPrincipal principal = await _sut.CreateAsync(user);

        principal.FindAll(ClaimTypes.Role).Should().BeEmpty();
        principal.IsInRole("admin").Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_ForAnyUser_NeverAsksForRoles()
    {
        WallowUser user = ArrangeUser();

        await _sut.CreateAsync(user);

        // Organization roles live on memberships, not the ASP.NET Identity user-role join.
        await _userManager.DidNotReceive().GetRolesAsync(Arg.Any<WallowUser>());
    }

    [Fact]
    public async Task CreateAsync_ForAnyUser_StampsNoOrgId()
    {
        WallowUser user = ArrangeUser();

        ClaimsPrincipal principal = await _sut.CreateAsync(user);

        principal.FindFirst("org_id").Should().BeNull();
    }

    private WallowUser ArrangeUser()
    {
        WallowUser user = WallowUser.Create(
            "Test", "User", "cookie@wallow.dev", TimeProvider.System);

        _userManager.GetUserIdAsync(user).Returns(user.Id.ToString());
        _userManager.GetUserNameAsync(user).Returns(user.Email);

        return user;
    }
}
