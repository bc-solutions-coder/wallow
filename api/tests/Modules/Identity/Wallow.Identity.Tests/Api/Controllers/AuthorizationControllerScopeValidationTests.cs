using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Contracts.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;

#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()

namespace Wallow.Identity.Tests.Api.Controllers;

/// <summary>
/// The authorize endpoint must not mint an access token carrying scopes the signed-in
/// user's role does not grant. Without this, any user can append
/// "roles.write users.manage" to their own authorize request and
/// <see cref="Wallow.Identity.Infrastructure.Authorization.PermissionExpansionMiddleware"/>
/// will faithfully expand those scopes into permissions — a straight privilege escalation.
/// Two independent gates are asserted here: the requested scopes must be registered for the
/// OIDC client (via <see cref="IScopeSubsetValidator"/>) AND must be covered by the caller's
/// own role permissions.
/// </summary>
public sealed class AuthorizationControllerScopeValidationTests : IDisposable
{
    private static readonly string _testUserId = Guid.NewGuid().ToString();
    private const string FirstPartyClientId = "wallow-web";
    private const string ApplicationId = "app-id-123";

    private readonly UserManager<WallowUser> _userManager;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictAuthorizationManager _authorizationManager;
    private readonly IScopeSubsetValidator _scopeSubsetValidator;
    private readonly IClientTenantResolver _clientTenantResolver;
    private readonly IOrganizationService _organizationService;
    private readonly AuthorizationController _controller;

    public AuthorizationControllerScopeValidationTests()
    {
        IUserStore<WallowUser> userStore = Substitute.For<IUserStore<WallowUser>>();
        _userManager = Substitute.For<UserManager<WallowUser>>(
            userStore, null, null, null, null, null, null, null, null);

        IConfiguration configuration = Substitute.For<IConfiguration>();
        configuration["AuthUrl"].Returns("https://auth.example.com");

        _applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        _authorizationManager = Substitute.For<IOpenIddictAuthorizationManager>();
        _clientTenantResolver = Substitute.For<IClientTenantResolver>();
        _organizationService = Substitute.For<IOrganizationService>();

        // Default: the client is registered for whatever it asks for, so each test
        // exercises only the gate it is about.
        _scopeSubsetValidator = Substitute.For<IScopeSubsetValidator>();
        _scopeSubsetValidator
            .ValidateAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(ScopeValidationResult.Success());

        _controller = new AuthorizationController(
            _userManager,
            configuration,
            _applicationManager,
            _authorizationManager,
            _scopeSubsetValidator,
            _clientTenantResolver,
            _organizationService,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthorizationController>.Instance);
    }

    public void Dispose()
    {
        _controller.Dispose();
        _userManager.Dispose();
    }

    [Fact]
    public async Task Authorize_PlainUserRequestingScopesBeyondTheirRole_IsRejectedWithInvalidScope()
    {
        // Arrange - "user" grants storage and organization reads, never roles.write or
        // users.manage. Asking for them anyway is the escalation attempt.
        ArrangeFlow("openid profile roles.write users.manage", roles: ["user"]);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert
        ForbidResult forbid = result.Should().BeOfType<ForbidResult>().Subject;
        forbid.AuthenticationSchemes.Should().Contain(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        forbid.Properties!.Items[OpenIddictServerAspNetCoreConstants.Properties.Error]
            .Should().Be(Errors.InvalidScope);
    }

    [Fact]
    public async Task Authorize_PlainUserRequestingScopesBeyondTheirRole_NamesTheOffendingScopes()
    {
        // Arrange - the error description is what the caller sees, so it has to say which
        // scopes were refused rather than failing the whole request opaquely.
        ArrangeFlow("openid profile roles.write users.manage", roles: ["user"]);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert
        ForbidResult forbid = result.Should().BeOfType<ForbidResult>().Subject;
        string? description =
            forbid.Properties!.Items[OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription];
        description.Should().Contain("roles.write");
        description.Should().Contain("users.manage");
    }

    [Fact]
    public async Task Authorize_PlainUserRequestingScopesBeyondTheirRole_IssuesNoAuthorizationCode()
    {
        // Arrange - rejection has to happen before the sign-in ticket and before any
        // permanent authorization is persisted, or the escalation just happens later.
        ArrangeFlow("openid roles.write", roles: ["user"]);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert
        result.Should().NotBeOfType<Microsoft.AspNetCore.Mvc.SignInResult>();
        await _authorizationManager.DidNotReceive().CreateAsync(
            Arg.Any<OpenIddictAuthorizationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_UserWithNoRolesRequestingPrivilegedScope_IsRejected()
    {
        // Arrange - a token with no role claims expands to no permissions, so every
        // permission-bearing scope is over-broad. This is the exact hole
        // PermissionExpansionMiddleware's scope expansion leaves open.
        ArrangeFlow("openid storage.write", roles: []);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Authorize_ScopeNotRegisteredForTheClient_IsRejectedEvenForAnAdmin()
    {
        // Arrange - the second gate: an admin's role covers the scope, but the OIDC client
        // itself was never registered for it, so the client must not receive it.
        ArrangeFlow("openid roles.write", roles: ["admin"]);
        _scopeSubsetValidator
            .ValidateAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(ScopeValidationResult.Failure("The following scopes are not permitted for this service account: roles.write"));

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert
        ForbidResult forbid = result.Should().BeOfType<ForbidResult>().Subject;
        forbid.Properties!.Items[OpenIddictServerAspNetCoreConstants.Properties.Error]
            .Should().Be(Errors.InvalidScope);
    }

    [Fact]
    public async Task Authorize_ChecksRequestedScopesAgainstTheRequestingClient()
    {
        // Arrange - the validator is keyed on the client making the authorize request, not
        // on the user, and it must see the scopes actually asked for.
        ArrangeFlow("openid storage.read", roles: ["user"]);

        // Act
        await _controller.Authorize();

        // Assert
        await _scopeSubsetValidator.Received(1).ValidateAsync(
            FirstPartyClientId,
            Arg.Is<IEnumerable<string>>(scopes => scopes.Contains("storage.read")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_PlainUserRequestingScopesWithinTheirRole_StillSignsIn()
    {
        // Arrange - regression guard: "user" really does grant storage.read and
        // organizations.read, so the ordinary case must not become collateral damage.
        ArrangeFlow("openid profile storage.read organizations.read", roles: ["user"]);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert
        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        signIn.Principal.GetScopes().Should().BeEquivalentTo(
            "openid", "profile", "storage.read", "organizations.read");
    }

    [Fact]
    public async Task Authorize_AdminRequestingPrivilegedScopes_StillSignsIn()
    {
        // Arrange - regression guard: "admin" covers RolesUpdate and UsersDelete, so the
        // very scopes refused above must go through for a caller who has earned them.
        ArrangeFlow("openid roles.write users.manage", roles: ["admin"]);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>();
    }

    [Fact]
    public async Task Authorize_StandardOidcScopesOnly_StillSignsInForAPlainUser()
    {
        // Arrange - regression guard: openid/profile/email/offline_access carry no
        // permission at all, so they must never be role-gated.
        ArrangeFlow("openid profile email offline_access", roles: ["user"]);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>();
    }

    /// <summary>
    /// Wires an authenticated authorize request for a first-party client, which skips the
    /// consent branch entirely so each test observes only the scope gates.
    /// </summary>
    private void ArrangeFlow(string scope, IList<string> roles)
    {
        OpenIddictRequest request = new()
        {
            ClientId = FirstPartyClientId,
            Scope = scope
        };

        ClaimsPrincipal user = new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, _testUserId)
        ], "test"));

        DefaultHttpContext httpContext = new() { User = user };
        OpenIddictServerTransaction transaction = new() { Request = request };
        httpContext.Features.Set(new OpenIddictServerAspNetCoreFeature { Transaction = transaction });
        httpContext.Request.Path = "/connect/authorize";
        httpContext.Request.QueryString = new QueryString("?client_id=" + FirstPartyClientId);

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        IUrlHelper urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.IsLocalUrl(Arg.Any<string>()).Returns(true);
        _controller.Url = urlHelper;

        WallowUser wallowUser = WallowUser.Create(
            Guid.NewGuid(), "Test", "User", "test@example.com", TimeProvider.System);

        _userManager.GetUserId(Arg.Any<ClaimsPrincipal>()).Returns(_testUserId);
        _userManager.FindByIdAsync(_testUserId).Returns(wallowUser);
        _userManager.GetUserNameAsync(wallowUser).Returns("testuser");
        _userManager.GetEmailAsync(wallowUser).Returns("test@example.com");
        _userManager.GetRolesAsync(wallowUser).Returns(roles);
        _userManager.GetClaimsAsync(wallowUser).Returns(new List<Claim>());

        object application = new();
        _applicationManager.FindByClientIdAsync(FirstPartyClientId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(application));
        _applicationManager.GetClientIdAsync(application, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(FirstPartyClientId));
        _applicationManager.GetIdAsync(application, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(ApplicationId));

        _clientTenantResolver.ResolveAsync(FirstPartyClientId, Arg.Any<CancellationToken>())
            .Returns((ClientTenantInfo?)null);
    }
}
