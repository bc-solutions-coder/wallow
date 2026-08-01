using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.DTOs;
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
/// The two gates fail differently: a scope the OIDC client is not registered for
/// (<see cref="IScopeSubsetValidator"/>) refuses the request, while a scope the caller's role
/// does not cover is dropped from the grant and the rest is issued. The roles in question are
/// the ones the client's own organization grants, never the caller's global role rows.
/// </summary>
public sealed class AuthorizationControllerScopeValidationTests : IDisposable
{
    private static readonly string _testUserId = Guid.NewGuid().ToString();
    private static readonly Guid _testOrganizationId = Guid.NewGuid();
    private const string FirstPartyClientId = "wallow-web";
    private const string ThirdPartyClientId = "partner-portal";
    private const string ApplicationId = "app-id-123";

    private readonly UserManager<WallowUser> _userManager;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictAuthorizationManager _authorizationManager;
    private readonly IScopeSubsetValidator _scopeSubsetValidator;
    private readonly IClientTenantResolver _clientTenantResolver;
    private readonly IOrganizationService _organizationService;
    private readonly IMembershipRoleResolver _membershipRoleResolver;
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
        _membershipRoleResolver = Substitute.For<IMembershipRoleResolver>();

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
            _membershipRoleResolver,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthorizationController>.Instance);
    }

    public void Dispose()
    {
        _controller.Dispose();
        _userManager.Dispose();
    }

    [Fact]
    public async Task Authorize_PlainUserRequestingScopesBeyondTheirRole_IssuesOnlyWhatTheRoleCovers()
    {
        // Arrange - "user" grants storage and organization reads, never roles.write or
        // users.manage. Asking for them anyway is the escalation attempt.
        ArrangeFlow("openid profile roles.write users.manage", roles: ["user"]);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert
        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        signIn.Principal.GetScopes().Should().BeEquivalentTo("openid", "profile");
    }

    [Fact]
    public async Task Authorize_ConsentGrantedForScopesBeyondTheCallersRole_PersistsOnlyTheGranted()
    {
        // Arrange - a stored authorization outlives the request that created it, so a refused
        // scope recorded here is an escalation the caller can redeem on any later request.
        ArrangeFlow(
            "openid profile roles.write",
            roles: ["user"],
            clientId: ThirdPartyClientId,
            consentGranted: true);

        // Act
        await _controller.Authorize();

        // Assert
        await _authorizationManager.Received().CreateAsync(
            Arg.Is<OpenIddictAuthorizationDescriptor>(descriptor =>
                descriptor.Scopes.Contains("openid")
                && descriptor.Scopes.Contains("profile")
                && !descriptor.Scopes.Contains("roles.write")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_UserWithNoRolesRequestingPrivilegedScope_IsNarrowedToNothing()
    {
        // Arrange - a token with no role claims expands to no permissions, so every
        // permission-bearing scope is over-broad. This is the exact hole
        // PermissionExpansionMiddleware's scope expansion leaves open.
        ArrangeFlow("openid storage.write", roles: []);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert
        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        signIn.Principal.GetScopes().Should().BeEquivalentTo("openid");
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
    /// Wires an authenticated authorize request. A first-party client skips the consent branch
    /// entirely, so each test observes only the scope gates; naming a third-party client with
    /// consent already granted is how a test reaches the stored-authorization branch instead.
    /// </summary>
    private void ArrangeFlow(
        string scope,
        IList<string> roles,
        string clientId = FirstPartyClientId,
        bool consentGranted = false)
    {
        OpenIddictRequest request = new()
        {
            ClientId = clientId,
            Scope = scope
        };

        if (consentGranted)
        {
            request.SetParameter("consent_granted", "true");
        }

        ClaimsPrincipal user = new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, _testUserId)
        ], "test"));

        DefaultHttpContext httpContext = new() { User = user };
        OpenIddictServerTransaction transaction = new() { Request = request };
        httpContext.Features.Set(new OpenIddictServerAspNetCoreFeature { Transaction = transaction });
        httpContext.Request.Path = "/connect/authorize";
        httpContext.Request.QueryString = new QueryString("?client_id=" + clientId);

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
        _userManager.GetClaimsAsync(wallowUser).Returns(new List<Claim>());

        object application = new();
        _applicationManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(application));
        _applicationManager.GetClientIdAsync(application, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(clientId));
        _applicationManager.GetIdAsync(application, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(ApplicationId));

        _authorizationManager.FindBySubjectAsync(_testUserId, Arg.Any<CancellationToken>())
            .Returns(Empty());

        _clientTenantResolver.ResolveAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(new ClientTenantInfo(_testOrganizationId, "Test Org"));

        _organizationService.GetUserOrganizationsAsync(
                Guid.Parse(_testUserId), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<OrganizationDto>>(
                [new OrganizationDto(_testOrganizationId, "Test Org", null, 1)]);

        // The only roles that decide anything here are the ones this organization grants;
        // whatever AspNetUserRoles holds globally is not consulted.
        _membershipRoleResolver.GetRoleNamesAsync(
                Guid.Parse(_testUserId), _testOrganizationId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<string>>([.. roles]);
    }

    private static async IAsyncEnumerable<object> Empty()
    {
        await Task.CompletedTask;
        yield break;
    }
}
