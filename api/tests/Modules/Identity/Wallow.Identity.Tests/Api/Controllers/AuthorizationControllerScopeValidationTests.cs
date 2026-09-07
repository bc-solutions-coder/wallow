using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
/// Checks client scope validation and narrowing to organization-role permissions.
/// <see cref="IScopeSubsetValidator"/> rejects unregistered client scopes; role-based narrowing
/// removes permission scopes the user cannot receive.
/// </summary>
public sealed class AuthorizationControllerScopeValidationTests : IDisposable
{
    private static readonly string _testUserId = Guid.NewGuid().ToString();
    private static readonly Guid _testOrganizationId = Guid.NewGuid();
    private const string FirstPartyClientId = "first-party-web";
    private const string ThirdPartyClientId = "partner-portal";
    private const string ApplicationId = "app-id-123";

    private readonly UserManager<WallowUser> _userManager;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictAuthorizationManager _authorizationManager;
    private readonly IScopeSubsetValidator _scopeSubsetValidator;
    private readonly IClientTenantResolver _clientTenantResolver;
    private readonly IUserEnrollmentService _enrollment;
    private readonly IMembershipRoleResolver _membershipRoleResolver;
    private readonly ISsoClientSessionService _ssoClientSessionService;
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
        _enrollment = Substitute.For<IUserEnrollmentService>();
        _membershipRoleResolver = Substitute.For<IMembershipRoleResolver>();
        _ssoClientSessionService = Substitute.For<ISsoClientSessionService>();

        // Accept consent tokens so these tests can inspect granted scopes.
        IConsentTokenService consentTokens = Substitute.For<IConsentTokenService>();
        consentTokens.Issue(Arg.Any<string>(), Arg.Any<string>()).Returns("consent-token");
        consentTokens
            .RedeemAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ConsentTokenOutcome.Redeemed);

        // Allow client scopes by default; rejection tests override this result.
        _scopeSubsetValidator = Substitute.For<IScopeSubsetValidator>();
        _scopeSubsetValidator
            .ValidateAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(ScopeValidationResult.Success());

        IOrganizationService organizations = Substitute.For<IOrganizationService>();
        organizations.GetMyOrganizationsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);

        _controller = new AuthorizationController(
            _userManager,
            configuration,
            _applicationManager,
            _authorizationManager,
            _scopeSubsetValidator,
            _clientTenantResolver,
            _enrollment,
            _membershipRoleResolver,
            organizations,
            _ssoClientSessionService,
            consentTokens,
            Substitute.For<IClientAccessPolicy>(),
            SessionServiceStub.Create(),
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
        // These permission scopes exceed the user role.
        ArrangeFlow("openid profile roles.write users.manage", roles: ["user"]);


        IActionResult result = await _controller.Authorize();


        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        signIn.Principal.GetScopes().Should().BeEquivalentTo("openid", "profile");
    }

    [Fact]
    public async Task Authorize_ConsentGrantedForScopesBeyondTheCallersRole_PersistsOnlyTheGranted()
    {
        // Persist only granted scopes so stored consent cannot retain refused permissions.
        ArrangeFlow(
            "openid profile roles.write",
            roles: ["user"],
            clientId: ThirdPartyClientId,
            consentGranted: true);


        await _controller.Authorize();


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
        // With no organization roles, permission-bearing scopes must be removed.
        ArrangeFlow("openid storage.write", roles: []);


        IActionResult result = await _controller.Authorize();


        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        signIn.Principal.GetScopes().Should().BeEquivalentTo("openid");
    }

    [Fact]
    public async Task Authorize_ScopeNotRegisteredForTheClient_IsRejectedEvenForAnAdmin()
    {
        // Role permission cannot substitute for client scope registration.
        ArrangeFlow("openid roles.write", roles: ["admin"]);
        _scopeSubsetValidator
            .ValidateAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(ScopeValidationResult.Failure("The following scopes are not permitted for this service account: roles.write"));


        IActionResult result = await _controller.Authorize();


        ForbidResult forbid = result.Should().BeOfType<ForbidResult>().Subject;
        forbid.Properties!.Items[OpenIddictServerAspNetCoreConstants.Properties.Error]
            .Should().Be(Errors.InvalidScope);
    }

    [Fact]
    public async Task Authorize_ChecksRequestedScopesAgainstTheRequestingClient()
    {

        ArrangeFlow("openid storage.read", roles: ["user"]);


        await _controller.Authorize();


        await _scopeSubsetValidator.Received(1).ValidateAsync(
            FirstPartyClientId,
            Arg.Is<IEnumerable<string>>(scopes => scopes.Contains("storage.read")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_PlainUserRequestingScopesWithinTheirRole_StillSignsIn()
    {

        ArrangeFlow("openid profile storage.read organizations.read", roles: ["user"]);


        IActionResult result = await _controller.Authorize();


        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        signIn.Principal.GetScopes().Should().BeEquivalentTo(
            "openid", "profile", "storage.read", "organizations.read");
    }

    [Fact]
    public async Task Authorize_AdminRequestingPrivilegedScopes_StillSignsIn()
    {

        ArrangeFlow("openid roles.write users.manage", roles: ["admin"]);


        IActionResult result = await _controller.Authorize();


        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>();
    }

    [Fact]
    public async Task Authorize_StandardOidcScopesOnly_StillSignsInForAPlainUser()
    {
        // Standard OIDC scopes do not map to application permissions.
        ArrangeFlow("openid profile email offline_access", roles: ["user"]);


        IActionResult result = await _controller.Authorize();


        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>();
    }

    /// <summary>
    /// Builds an authenticated request with implicit consent by default.
    /// Explicit-consent cases submit a granted decision to exercise authorization persistence.
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
            request.SetParameter(AuthorizationController.ConsentDecisionParameter, AuthorizationController.ConsentGranted);
            request.SetParameter(AuthorizationController.ConsentTokenParameter, "consent-token");
        }

        ClaimsPrincipal user = new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, _testUserId)
        ], "test"));

        DefaultHttpContext httpContext = new() { User = user };
        OpenIddictServerTransaction transaction = new() { Request = request };
        httpContext.Features.Set(new OpenIddictServerAspNetCoreFeature { Transaction = transaction });
        // A consent decision only counts when it is POSTed.
        httpContext.Request.Method = consentGranted ? "POST" : "GET";
        httpContext.Request.Path = "/connect/authorize";
        httpContext.Request.QueryString = new QueryString("?client_id=" + clientId);

        // Supply authentication services for SID creation and cookie reissuance.
        IAuthenticationService authenticationService = Substitute.For<IAuthenticationService>();
        authenticationService
            .AuthenticateAsync(Arg.Any<HttpContext>(), IdentityConstants.ApplicationScheme)
            .Returns(AuthenticateResult.Success(
                new AuthenticationTicket(user, new AuthenticationProperties(), IdentityConstants.ApplicationScheme)));
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(authenticationService)
            .BuildServiceProvider();

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        IUrlHelper urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.IsLocalUrl(Arg.Any<string>()).Returns(true);
        _controller.Url = urlHelper;

        WallowUser wallowUser = WallowUser.Create(
            "Test", "User", "test@example.com", TimeProvider.System);

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
        _applicationManager.GetConsentTypeAsync(application, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(
                clientId == FirstPartyClientId ? ConsentTypes.Implicit : ConsentTypes.Explicit));
        _applicationManager.GetIdAsync(application, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(ApplicationId));

        _authorizationManager.FindBySubjectAsync(_testUserId, Arg.Any<CancellationToken>())
            .Returns(Empty());

        _clientTenantResolver.ResolveAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(new ClientTenantInfo(_testOrganizationId, "Test Org"));

        _enrollment.EnrollAsync(
                Guid.Parse(_testUserId), _testOrganizationId, Arg.Any<CancellationToken>())
            .Returns(new Enrolled());

        // Resolve roles for the selected organization.
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
