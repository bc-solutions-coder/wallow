using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Helpers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Kernel.Extensions;
using static OpenIddict.Abstractions.OpenIddictConstants;

#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()

namespace Wallow.Identity.Tests.Api.Controllers;

public sealed class AuthorizationControllerTests : IDisposable
{
    private static readonly string _testUserId = Guid.NewGuid().ToString();
    private static readonly Guid _testOrganizationId = Guid.NewGuid();
    private const string ThirdPartyClientId = "my-external-app";
    // Avoid the wallow- prefix so the test cannot pass by inferring first-party status from the ID.
    private const string FirstPartyClientId = "first-party-web";
    private const string ApplicationId = "app-id-123";

    private readonly UserManager<WallowUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictAuthorizationManager _authorizationManager;
    private readonly IScopeSubsetValidator _scopeSubsetValidator;
    private readonly IClientTenantResolver _clientTenantResolver;
    private readonly IUserEnrollmentService _enrollment;
    private readonly IMembershipRoleResolver _membershipRoleResolver;
    private readonly IOrganizationService _organizations;
    private readonly ISsoClientSessionService _ssoClientSessionService;
    private readonly IAuthenticationService _authenticationService;
    private readonly IConsentTokenService _consentTokens;
    private readonly ISessionService _sessionService = SessionServiceStub.Create();
    private readonly AuthorizationController _controller;

    /// <summary>The token the consent tests post back; what it redeems as is per test.</summary>
    private const string ConsentToken = "consent-token";

    public AuthorizationControllerTests()
    {
        IUserStore<WallowUser> userStore = Substitute.For<IUserStore<WallowUser>>();
        _userManager = Substitute.For<UserManager<WallowUser>>(
            userStore, null, null, null, null, null, null, null, null);

        _configuration = Substitute.For<IConfiguration>();
        _configuration["AuthUrl"].Returns("https://auth.example.com");

        _applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        _authorizationManager = Substitute.For<IOpenIddictAuthorizationManager>();
        _clientTenantResolver = Substitute.For<IClientTenantResolver>();
        _enrollment = Substitute.For<IUserEnrollmentService>();
        _membershipRoleResolver = Substitute.For<IMembershipRoleResolver>();
        _ssoClientSessionService = Substitute.For<ISsoClientSessionService>();
        _authenticationService = Substitute.For<IAuthenticationService>();

        // Tests override the empty membership list when selecting a default organization.
        _organizations = Substitute.For<IOrganizationService>();
        _organizations.GetMyOrganizationsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Consent redemption succeeds by default; refusal cases override it.
        _consentTokens = Substitute.For<IConsentTokenService>();
        _consentTokens.Issue(Arg.Any<string>(), Arg.Any<string>()).Returns(ConsentToken);
        _consentTokens
            .RedeemAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ConsentTokenOutcome.Redeemed);

        // Bypass client scope registration checks in these consent and session tests.
        _scopeSubsetValidator = Substitute.For<IScopeSubsetValidator>();
        _scopeSubsetValidator
            .ValidateAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(ScopeValidationResult.Success());

        _controller = new AuthorizationController(
            _userManager,
            _configuration,
            _applicationManager,
            _authorizationManager,
            _scopeSubsetValidator,
            _clientTenantResolver,
            _enrollment,
            _membershipRoleResolver,
            _organizations,
            _ssoClientSessionService,
            _consentTokens,
            Substitute.For<IClientAccessPolicy>(),
            _sessionService,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthorizationController>.Instance);
    }

    public void Dispose()
    {
        _controller.Dispose();
        _userManager.Dispose();
    }

    private void SetupAuthenticatedHttpContext(
        OpenIddictRequest request, string? queryString = null, string? existingSid = null, string method = "GET")
    {
        List<Claim> claims = [new Claim(ClaimTypes.NameIdentifier, _testUserId)];
        if (existingSid is not null)
        {
            claims.Add(new Claim(ClaimsPrincipalExtensions.SessionIdClaimType, existingSid));
        }

        ClaimsPrincipal user = new(new ClaimsIdentity(claims, "test"));

        DefaultHttpContext httpContext = new() { User = user };


        OpenIddictServerTransaction transaction = new() { Request = request };
        httpContext.Features.Set(new OpenIddictServerAspNetCoreFeature { Transaction = transaction });

        httpContext.Request.Method = method;
        httpContext.Request.Path = "/connect/authorize";
        httpContext.Request.QueryString = new QueryString(queryString ?? "?client_id=" + (request.ClientId ?? ThirdPartyClientId));

        // Supply authentication services for SID creation and cookie reissuance.
        _authenticationService
            .AuthenticateAsync(Arg.Any<HttpContext>(), IdentityConstants.ApplicationScheme)
            .Returns(AuthenticateResult.Success(
                new AuthenticationTicket(user, new AuthenticationProperties(), IdentityConstants.ApplicationScheme)));
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_authenticationService)
            .BuildServiceProvider();

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };


        IUrlHelper urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.IsLocalUrl(Arg.Any<string>()).Returns(true);
        _controller.Url = urlHelper;
    }

    /// <summary>
    /// Builds the request fixture with an unauthenticated principal.
    /// </summary>
    private void SetupAnonymousHttpContext(OpenIddictRequest request, string method)
    {
        SetupAuthenticatedHttpContext(request, method: method);
        _controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
    }

    private void SetupUser()
    {
        WallowUser wallowUser = WallowUser.Create(
            "Test", "User", "test@example.com", TimeProvider.System);

        _userManager.GetUserId(Arg.Any<ClaimsPrincipal>()).Returns(_testUserId);
        _userManager.FindByIdAsync(_testUserId).Returns(wallowUser);
        _userManager.GetUserNameAsync(wallowUser).Returns("testuser");
        _userManager.GetEmailAsync(wallowUser).Returns("test@example.com");
        _userManager.GetClaimsAsync(wallowUser).Returns(new List<Claim>());
    }

    private void SetupApplication(
        string clientId,
        string applicationId = ApplicationId,
        string consentType = ConsentTypes.Explicit)
    {
        object application = new();
        _applicationManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(application));
        _applicationManager.GetClientIdAsync(application, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(clientId));
        _applicationManager.GetConsentTypeAsync(application, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(consentType));
        _applicationManager.GetIdAsync(application, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(applicationId));
    }

    private void SetupNoExistingAuthorizations()
    {
        _authorizationManager.FindBySubjectAsync(_testUserId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<object>());
    }

    private void SetupExistingValidAuthorization(string applicationId, ImmutableArray<string> scopes)
    {
        object authorization = new();
        _authorizationManager.FindBySubjectAsync(_testUserId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(authorization));
        _authorizationManager.GetApplicationIdAsync(authorization, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(applicationId));
        _authorizationManager.GetStatusAsync(authorization, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(OpenIddictConstants.Statuses.Valid));
        // Consent lookup filters by type: only a permanent authorization counts as stored consent.
        _authorizationManager.GetTypeAsync(authorization, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(OpenIddictConstants.AuthorizationTypes.Permanent));
        _authorizationManager.GetScopesAsync(authorization, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(scopes));
    }

    /// <summary>
    /// Stubs a client with no organization binding.
    /// </summary>
    private void SetupUnboundClientTenantResolver(string clientId)
    {
        _clientTenantResolver.ResolveAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(new ClientTenantInfo(Guid.Empty, null));
    }

    private void SetupClientTenantResolver(string clientId)
    {
        _clientTenantResolver.ResolveAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(new ClientTenantInfo(_testOrganizationId, "Test Org"));

        _enrollment.EnrollAsync(
                Guid.Parse(_testUserId), _testOrganizationId, Arg.Any<CancellationToken>())
            .Returns(new Enrolled());

        _membershipRoleResolver.GetRoleNamesAsync(
                Guid.Parse(_testUserId), _testOrganizationId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<string>>(["user"]);
    }

    #region Consent Decisions

    /// <summary>A third-party authorize request answering the consent screen.</summary>
    private static OpenIddictRequest ConsentDecision(string decision, string? token = ConsentToken)
    {
        OpenIddictRequest request = new()
        {
            ClientId = ThirdPartyClientId,
            Scope = "openid profile",
            [AuthorizationController.ConsentDecisionParameter] = decision
        };

        if (token is not null)
        {
            request[AuthorizationController.ConsentTokenParameter] = token;
        }

        return request;
    }

    [Fact]
    public async Task Authorize_WithConsentDenied_ThirdPartyClient_ReturnsForbidWithConsentRequired()
    {

        OpenIddictRequest request = ConsentDecision(AuthorizationController.ConsentDenied);

        SetupAuthenticatedHttpContext(request, method: "POST");
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupNoExistingAuthorizations();
        SetupClientTenantResolver(ThirdPartyClientId);


        IActionResult result = await _controller.Authorize();


        ForbidResult forbidResult = result.Should().BeOfType<ForbidResult>().Subject;
        forbidResult.AuthenticationSchemes.Should().Contain(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        forbidResult.Properties!.Items[OpenIddictServerAspNetCoreConstants.Properties.Error]
            .Should().Be(Errors.ConsentRequired);
    }

    [Fact]
    public async Task Authorize_WithConsentGranted_NoExistingAuthorization_CreatesAuthorizationAndReturnsSignIn()
    {

        OpenIddictRequest request = ConsentDecision(AuthorizationController.ConsentGranted);

        SetupAuthenticatedHttpContext(request, method: "POST");
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupNoExistingAuthorizations();
        SetupClientTenantResolver(ThirdPartyClientId);


        IActionResult result = await _controller.Authorize();

        // Permanent consent and per-sign-in token authorization are separate records.
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>();
        await _authorizationManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictAuthorizationDescriptor>(d => d.Type == AuthorizationTypes.Permanent),
            Arg.Any<CancellationToken>());
        await _authorizationManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictAuthorizationDescriptor>(d => d.Type == AuthorizationTypes.AdHoc),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_WithConsentGranted_ExistingValidAuthorization_DoesNotCreateDuplicateAuthorization()
    {

        OpenIddictRequest request = ConsentDecision(AuthorizationController.ConsentGranted);

        SetupAuthenticatedHttpContext(request, method: "POST");
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupExistingValidAuthorization(ApplicationId, ["openid", "profile"]);
        SetupClientTenantResolver(ThirdPartyClientId);


        IActionResult result = await _controller.Authorize();

        // Reuse stored consent while creating a new per-sign-in authorization.
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>();
        await _authorizationManager.DidNotReceive().CreateAsync(
            Arg.Is<OpenIddictAuthorizationDescriptor>(d => d.Type == AuthorizationTypes.Permanent),
            Arg.Any<CancellationToken>());
        await _authorizationManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictAuthorizationDescriptor>(d => d.Type == AuthorizationTypes.AdHoc),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("granted")]
    [InlineData("denied")]
    public async Task Authorize_WithAConsentDecisionOnTheGet_IgnoresItAndShowsTheConsentScreen(string decision)
    {
        // A GET decision must be ignored even when the token stub would accept it.
        OpenIddictRequest request = ConsentDecision(decision);

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupNoExistingAuthorizations();
        SetupClientTenantResolver(ThirdPartyClientId);


        IActionResult result = await _controller.Authorize();


        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().StartWith("https://auth.example.com/consent?");
        await _authorizationManager.DidNotReceive().CreateAsync(
            Arg.Any<OpenIddictAuthorizationDescriptor>(), Arg.Any<CancellationToken>());
        await _consentTokens.DidNotReceive().RedeemAsync(
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ConsentTokenOutcome.Missing)]
    [InlineData(ConsentTokenOutcome.Invalid)]
    [InlineData(ConsentTokenOutcome.Mismatched)]
    [InlineData(ConsentTokenOutcome.Replayed)]
    public async Task Authorize_WithAConsentDecisionWhoseTokenIsRefused_ShowsTheConsentScreenAgain(
        ConsentTokenOutcome outcome)
    {

        OpenIddictRequest request = ConsentDecision(AuthorizationController.ConsentGranted);
        _consentTokens
            .RedeemAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(outcome);

        SetupAuthenticatedHttpContext(request, method: "POST");
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupNoExistingAuthorizations();
        SetupClientTenantResolver(ThirdPartyClientId);


        IActionResult result = await _controller.Authorize();

        // Return a consent token without creating an authorization.
        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(new Uri(redirect.Url).Query);
        query[AuthorizationController.ConsentTokenParameter].ToString().Should().Be(ConsentToken);
        await _authorizationManager.DidNotReceive().CreateAsync(
            Arg.Any<OpenIddictAuthorizationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_WithAConsentDecision_RedeemsTheTokenForTheSignedInUserAndTheRequest()
    {
        // Redemption must bind the same user and request fingerprint used at issuance.
        OpenIddictRequest shown = new() { ClientId = ThirdPartyClientId, Scope = "openid profile" };
        SetupAuthenticatedHttpContext(shown);
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupNoExistingAuthorizations();
        SetupClientTenantResolver(ThirdPartyClientId);
        string? mintedFor = null;
        _consentTokens.Issue(_testUserId, Arg.Do<string>(fingerprint => mintedFor = fingerprint));
        await _controller.Authorize();
        mintedFor.Should().NotBeNull("showing the consent screen mints a token for the request");

        OpenIddictRequest answered = ConsentDecision(AuthorizationController.ConsentGranted);
        SetupAuthenticatedHttpContext(answered, method: "POST");


        await _controller.Authorize();


        await _consentTokens.Received(1).RedeemAsync(ConsentToken, _testUserId, mintedFor, Arg.Any<CancellationToken>());
    }

    #endregion

    #region First-Party Client Skips Consent

    [Fact]
    public async Task Authorize_FirstPartyClient_WithConsentParameter_SkipsConsentLogicAndReturnsSignIn()
    {

        OpenIddictRequest request = new()
        {
            ClientId = FirstPartyClientId,
            Scope = "openid profile",
            [AuthorizationController.ConsentDecisionParameter] = AuthorizationController.ConsentGranted,
            [AuthorizationController.ConsentTokenParameter] = ConsentToken
        };

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupClientTenantResolver(FirstPartyClientId);


        IActionResult result = await _controller.Authorize();

        // Implicit consent bypasses permanent-consent persistence and lookup.
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>();


        await _authorizationManager.DidNotReceive().CreateAsync(
            Arg.Is<OpenIddictAuthorizationDescriptor>(d => d.Type == AuthorizationTypes.Permanent),
            Arg.Any<CancellationToken>());


        _authorizationManager.DidNotReceive().FindBySubjectAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_FirstPartyClient_BoundToNoOrganization_SignsInWithAnOrgLessToken()
    {
        // With no organization selected, issue no organization claims or roles.
        OpenIddictRequest request = new() { ClientId = FirstPartyClientId, Scope = "openid profile" };

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupUnboundClientTenantResolver(FirstPartyClientId);

        IActionResult result = await _controller.Authorize();

        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        signIn.Principal.FindFirst("org_id").Should().BeNull("an org-less first-party login carries no organization");
        signIn.Principal.FindFirst("org_name").Should().BeNull();
        signIn.Principal.FindAll(Claims.Role).Should().BeEmpty("roles are granted by an organization");
        await _enrollment.DidNotReceive().EnrollAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_FirstPartyClient_UserWithASingleMembership_SignsInWithThatOrganization()
    {
        // A single available organization supplies the default context and its roles.
        Guid organizationId = Guid.NewGuid();
        OpenIddictRequest request = new() { ClientId = FirstPartyClientId, Scope = "openid profile" };

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupUnboundClientTenantResolver(FirstPartyClientId);
        _organizations.GetMyOrganizationsAsync(Guid.Parse(_testUserId), Arg.Any<CancellationToken>())
            .Returns([new MyOrganizationDto(organizationId, "Only Org", "only-org", IsOwner: true)]);
        _enrollment.EnrollAsync(Guid.Parse(_testUserId), organizationId, Arg.Any<CancellationToken>())
            .Returns(new Enrolled());
        _membershipRoleResolver.GetRoleNamesAsync(Guid.Parse(_testUserId), organizationId, Arg.Any<CancellationToken>())
            .Returns(["admin"]);

        IActionResult result = await _controller.Authorize();

        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        signIn.Principal.FindFirst("org_id")!.Value.Should().Be(organizationId.ToString());
        signIn.Principal.FindFirst("org_name")!.Value.Should().Be("Only Org");
        signIn.Principal.FindAll(Claims.Role).Select(c => c.Value).Should().Equal("admin");
    }

    [Fact]
    public async Task Authorize_FirstPartyClient_UserWithSeveralMemberships_SignsInWithAnOrgLessToken()
    {
        // Multiple memberships need a hint to select an organization.
        OpenIddictRequest request = new() { ClientId = FirstPartyClientId, Scope = "openid profile" };

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupUnboundClientTenantResolver(FirstPartyClientId);
        _organizations.GetMyOrganizationsAsync(Guid.Parse(_testUserId), Arg.Any<CancellationToken>())
            .Returns(
            [
                new MyOrganizationDto(Guid.NewGuid(), "One", "one", IsOwner: true),
                new MyOrganizationDto(Guid.NewGuid(), "Two", "two", IsOwner: false),
            ]);

        IActionResult result = await _controller.Authorize();

        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        signIn.Principal.FindFirst("org_id").Should().BeNull();
        signIn.Principal.FindAll(Claims.Role).Should().BeEmpty();
    }

    [Fact]
    public async Task Authorize_ThirdPartyClient_BoundToNoOrganization_RedirectsToClientNotBoundError()
    {
        // A wallow- prefix cannot replace the explicit consent type or organization binding.
        const string lookalikeClientId = "wallow-lookalike";
        OpenIddictRequest request = new() { ClientId = lookalikeClientId, Scope = "openid" };

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(lookalikeClientId, consentType: ConsentTypes.Explicit);
        _clientTenantResolver.ResolveAsync(lookalikeClientId, Arg.Any<CancellationToken>())
            .Returns(new ClientTenantInfo(Guid.Empty, null));

        IActionResult result = await _controller.Authorize();

        result.Should().BeOfType<RedirectResult>().Which.Url
            .Should().Be("https://auth.example.com/error?reason=client_not_bound_to_organization");
    }

    #endregion

    #region Organization Hint

    [Fact]
    public async Task Authorize_FirstPartyClient_WithAnOrganizationHint_RunsThatOrganizationsPolicy()
    {
        // The hint selects which organization enrollment and role services are queried.
        Guid hinted = Guid.NewGuid();
        OpenIddictRequest request = FirstPartyRequestWithHint(hinted.ToString());

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupUnboundClientTenantResolver(FirstPartyClientId);
        _organizations.GetMyOrganizationsAsync(Guid.Parse(_testUserId), Arg.Any<CancellationToken>())
            .Returns(
            [
                new MyOrganizationDto(hinted, "Hinted Org", "hinted", IsOwner: false),
                new MyOrganizationDto(Guid.NewGuid(), "Other", "other", IsOwner: true),
            ]);
        _organizations.GetOrganizationByIdAsync(hinted, Arg.Any<CancellationToken>())
            .Returns(new OrganizationDto(hinted, "Hinted Org", null, 2));
        _enrollment.EnrollAsync(Guid.Parse(_testUserId), hinted, Arg.Any<CancellationToken>())
            .Returns(new Enrolled());
        _membershipRoleResolver.GetRoleNamesAsync(Guid.Parse(_testUserId), hinted, Arg.Any<CancellationToken>())
            .Returns(["user"]);

        IActionResult result = await _controller.Authorize();

        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        signIn.Principal.FindFirst("org_id")!.Value.Should().Be(hinted.ToString());
        signIn.Principal.FindFirst("org_name")!.Value.Should().Be("Hinted Org");
        signIn.Principal.FindAll(Claims.Role).Select(c => c.Value).Should().Equal("user");
        await _enrollment.Received(1).EnrollAsync(
            Guid.Parse(_testUserId), hinted, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_FirstPartyClient_WithAHintTheUserIsNoMemberOf_RedirectsToTheErrorPage()
    {
        // First-party enrollment refusals use the auth host error page.
        Guid hinted = Guid.NewGuid();
        OpenIddictRequest request = FirstPartyRequestWithHint(hinted.ToString());

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupUnboundClientTenantResolver(FirstPartyClientId);
        _organizations.GetOrganizationByIdAsync(hinted, Arg.Any<CancellationToken>())
            .Returns((OrganizationDto?)null);
        _enrollment.EnrollAsync(Guid.Parse(_testUserId), hinted, Arg.Any<CancellationToken>())
            .Returns(new Rejected("not_a_member"));

        IActionResult result = await _controller.Authorize();

        result.Should().BeOfType<RedirectResult>().Which.Url
            .Should().Be("https://auth.example.com/error?reason=not_a_member");
    }

    [Fact]
    public async Task Authorize_ThirdPartyClient_WithAHintOtherThanItsBoundOrganization_IsInvalidRequest()
    {
        // A hint cannot override a registered organization binding.
        OpenIddictRequest request = new()
        {
            ClientId = ThirdPartyClientId,
            Scope = "openid profile",
            [AuthorizationController.OrganizationParameter] = Guid.NewGuid().ToString(),
        };

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupClientTenantResolver(ThirdPartyClientId);

        IActionResult result = await _controller.Authorize();

        ForbidResult forbid = result.Should().BeOfType<ForbidResult>().Subject;
        forbid.AuthenticationSchemes.Should().Contain(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        forbid.Properties!.Items[OpenIddictServerAspNetCoreConstants.Properties.Error]
            .Should().Be(Errors.InvalidRequest);
        await _enrollment.DidNotReceive().EnrollAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_ThirdPartyClient_WithAHintNamingItsOwnOrganization_SignsIn()
    {
        // A matching hint preserves the registered organization context.
        OpenIddictRequest request = new()
        {
            ClientId = ThirdPartyClientId,
            Scope = "openid profile",
            [AuthorizationController.OrganizationParameter] = _testOrganizationId.ToString(),
        };

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(ThirdPartyClientId, consentType: ConsentTypes.Implicit);
        SetupClientTenantResolver(ThirdPartyClientId);

        IActionResult result = await _controller.Authorize();

        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        signIn.Principal.FindFirst("org_id")!.Value.Should().Be(_testOrganizationId.ToString());
    }

    [Fact]
    public async Task Authorize_WithAHintThatIsNotAnOrganizationId_IsInvalidRequest()
    {
        OpenIddictRequest request = FirstPartyRequestWithHint("not-an-organization");

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupUnboundClientTenantResolver(FirstPartyClientId);

        IActionResult result = await _controller.Authorize();

        ForbidResult forbid = result.Should().BeOfType<ForbidResult>().Subject;
        forbid.Properties!.Items[OpenIddictServerAspNetCoreConstants.Properties.Error]
            .Should().Be(Errors.InvalidRequest);
    }

    [Fact]
    public async Task Authorize_FirstPartyClient_WithAnOrganization_LinksTheTokensToAnAuthorizationNamingIt()
    {
        // Store the selected organization on the authorization so revocation can find its tokens.
        OpenIddictRequest request = FirstPartyRequestWithHint(_testOrganizationId.ToString());

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupUnboundClientTenantResolver(FirstPartyClientId);
        _organizations.GetOrganizationByIdAsync(_testOrganizationId, Arg.Any<CancellationToken>())
            .Returns(new OrganizationDto(_testOrganizationId, "Acme", null, 1));
        _enrollment.EnrollAsync(Guid.Parse(_testUserId), _testOrganizationId, Arg.Any<CancellationToken>())
            .Returns(new Enrolled());
        object authorization = new();
        OpenIddictAuthorizationDescriptor? created = null;
        _authorizationManager.CreateAsync(
                Arg.Do<OpenIddictAuthorizationDescriptor>(d => created = d), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(authorization));
        _authorizationManager.GetIdAsync(authorization, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("authorization-1"));

        IActionResult result = await _controller.Authorize();

        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        signIn.Principal.GetAuthorizationId().Should().Be("authorization-1");
        created.Should().NotBeNull();
        created!.Type.Should().Be(AuthorizationTypes.AdHoc);
        created.Subject.Should().Be(_testUserId);
        created.ApplicationId.Should().Be(ApplicationId);
        created.Properties[AuthorizationProperties.OrganizationId].GetString()
            .Should().Be(_testOrganizationId.ToString());
        created.Properties[AuthorizationProperties.SessionId].GetString()
            .Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Authorize_FirstPartyClient_WithoutAnOrganization_RecordsASessionScopedAuthorization()
    {
        // Organization-less authorizations still need a SID for session revocation.
        OpenIddictRequest request = new() { ClientId = FirstPartyClientId, Scope = "openid profile" };

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupUnboundClientTenantResolver(FirstPartyClientId);
        _organizations.GetMyOrganizationsAsync(Guid.Parse(_testUserId), Arg.Any<CancellationToken>())
            .Returns([]);
        object authorization = new();
        OpenIddictAuthorizationDescriptor? created = null;
        _authorizationManager.CreateAsync(
                Arg.Do<OpenIddictAuthorizationDescriptor>(d => created = d), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(authorization));
        _authorizationManager.GetIdAsync(authorization, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("authorization-1"));

        IActionResult result = await _controller.Authorize();

        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        signIn.Principal.GetAuthorizationId().Should().Be("authorization-1");
        created.Should().NotBeNull();
        created!.Type.Should().Be(AuthorizationTypes.AdHoc);
        created.Properties[AuthorizationProperties.SessionId].GetString()
            .Should().NotBeNullOrEmpty();
        created.Properties.Should().NotContainKey(AuthorizationProperties.OrganizationId);
    }

    private static OpenIddictRequest FirstPartyRequestWithHint(string organization) => new()
    {
        ClientId = FirstPartyClientId,
        Scope = "openid profile",
        [AuthorizationController.OrganizationParameter] = organization,
    };

    #endregion

    #region Enrollment Refusals

    [Theory]
    [InlineData("membership_suspended")]
    [InlineData("membership_denied")]
    [InlineData("not_a_member")]
    public async Task Authorize_ThirdPartyClient_WhenTheOrganizationRefuses_SendsAccessDeniedToTheRelyingParty(
        string reason)
    {
        // Return organization refusal reasons through the OpenIddict error result.
        OpenIddictRequest request = new() { ClientId = ThirdPartyClientId, Scope = "openid profile" };

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupClientTenantResolver(ThirdPartyClientId);
        _enrollment.EnrollAsync(Guid.Parse(_testUserId), _testOrganizationId, Arg.Any<CancellationToken>())
            .Returns(new Rejected(reason));

        IActionResult result = await _controller.Authorize();

        ForbidResult forbid = result.Should().BeOfType<ForbidResult>().Subject;
        forbid.AuthenticationSchemes.Should().Contain(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        forbid.Properties!.Items[OpenIddictServerAspNetCoreConstants.Properties.Error]
            .Should().Be(Errors.AccessDenied);
        forbid.Properties.Items[OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription]
            .Should().Be(reason);
    }

    [Fact]
    public async Task Authorize_ThirdPartyClient_WhenTheRequestIsPending_SendsAccessDeniedMembershipPending()
    {
        // Translate the pending outcome into membership_pending for the relying party.
        OpenIddictRequest request = new() { ClientId = ThirdPartyClientId, Scope = "openid profile" };

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupClientTenantResolver(ThirdPartyClientId);
        _enrollment.EnrollAsync(Guid.Parse(_testUserId), _testOrganizationId, Arg.Any<CancellationToken>())
            .Returns(new PendingApproval());

        IActionResult result = await _controller.Authorize();

        ForbidResult forbid = result.Should().BeOfType<ForbidResult>().Subject;
        forbid.Properties!.Items[OpenIddictServerAspNetCoreConstants.Properties.Error]
            .Should().Be(Errors.AccessDenied);
        forbid.Properties.Items[OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription]
            .Should().Be("membership_pending");
    }

    [Fact]
    public async Task Authorize_ThirdPartyClient_WhenTheEmailIsUnverified_StaysOnTheAuthHost()
    {
        // Email verification errors stay on the auth host.
        OpenIddictRequest request = new() { ClientId = ThirdPartyClientId, Scope = "openid profile" };

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupClientTenantResolver(ThirdPartyClientId);
        _enrollment.EnrollAsync(Guid.Parse(_testUserId), _testOrganizationId, Arg.Any<CancellationToken>())
            .Returns(new Rejected("email_unverified"));

        IActionResult result = await _controller.Authorize();

        result.Should().BeOfType<RedirectResult>().Which.Url
            .Should().Be("https://auth.example.com/error?reason=email_unverified");
    }

    [Fact]
    public async Task Authorize_FirstPartyClient_WhenTheRequestIsPending_RedirectsToTheAccessRequestScreen()
    {
        Guid hinted = Guid.NewGuid();
        OpenIddictRequest request = FirstPartyRequestWithHint(hinted.ToString());

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupUnboundClientTenantResolver(FirstPartyClientId);
        _organizations.GetOrganizationByIdAsync(hinted, Arg.Any<CancellationToken>())
            .Returns(new OrganizationDto(hinted, "Hinted Org", null, 2));
        _enrollment.EnrollAsync(Guid.Parse(_testUserId), hinted, Arg.Any<CancellationToken>())
            .Returns(new PendingApproval());

        IActionResult result = await _controller.Authorize();

        result.Should().BeOfType<RedirectResult>().Which.Url
            .Should().Be("https://auth.example.com/access-request");
    }

    #endregion

    #region Redirect To Consent

    [Fact]
    public async Task Authorize_ThirdPartyClient_NoExistingAuthorization_RedirectsToConsentCarryingRequestedScopes()
    {
        // The consent screen needs the requested scopes to display the decision.
        OpenIddictRequest request = new()
        {
            ClientId = ThirdPartyClientId,
            Scope = "openid profile"
        };

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupNoExistingAuthorizations();
        SetupClientTenantResolver(ThirdPartyClientId);


        IActionResult result = await _controller.Authorize();


        RedirectResult redirectResult = result.Should().BeOfType<RedirectResult>().Subject;
        redirectResult.Url.Should().StartWith("https://auth.example.com/consent?");

        Dictionary<string, StringValues> query =
            QueryHelpers.ParseQuery(new Uri(redirectResult.Url).Query);

        // Preserve the space-delimited scope format.
        query.Should().ContainKey("scope");
        query["scope"].ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Should().BeEquivalentTo("openid", "profile");


        query[AuthorizationController.ConsentTokenParameter].ToString().Should().Be(ConsentToken);
        _consentTokens.Received(1).Issue(_testUserId, Arg.Any<string>());
    }

    [Fact]
    public async Task Authorize_ThirdPartyClient_NoExistingAuthorization_KeepsReturnUrlAndClientIdOnTheConsentRedirect()
    {
        // Rebuild the return URL from protocol parameters, excluding the consent decision.
        OpenIddictRequest request = new()
        {
            ClientId = ThirdPartyClientId,
            Scope = "openid profile",
            [AuthorizationController.ConsentDecisionParameter] = AuthorizationController.ConsentGranted
        };

        SetupAuthenticatedHttpContext(
            request,
            "?client_id=" + ThirdPartyClientId + "&scope=openid%20profile&consent_decision=granted");
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupNoExistingAuthorizations();
        SetupClientTenantResolver(ThirdPartyClientId);


        IActionResult result = await _controller.Authorize();


        RedirectResult redirectResult = result.Should().BeOfType<RedirectResult>().Subject;

        Dictionary<string, StringValues> query =
            QueryHelpers.ParseQuery(new Uri(redirectResult.Url).Query);

        query["client_id"].ToString().Should().Be(ThirdPartyClientId);
        query["returnUrl"].ToString().Should().Be(
            "/connect/authorize?client_id=" + ThirdPartyClientId + "&scope=openid%20profile");
    }

    [Fact]
    public async Task Authorize_AnonymousConsentPost_SendsTheWholeRequestBackThroughLogin()
    {
        // An anonymous POST must preserve request parameters for login without replaying its decision.
        OpenIddictRequest request = ConsentDecision(AuthorizationController.ConsentGranted);
        SetupAnonymousHttpContext(request, "POST");


        IActionResult result = await _controller.Authorize();


        RedirectResult redirectResult = result.Should().BeOfType<RedirectResult>().Subject;
        redirectResult.Url.Should().StartWith("https://auth.example.com/login?");

        Dictionary<string, StringValues> query =
            QueryHelpers.ParseQuery(new Uri(redirectResult.Url).Query);
        query["client_id"].ToString().Should().Be(ThirdPartyClientId);
        query["returnUrl"].ToString().Should().Be(
            "/connect/authorize?client_id=" + ThirdPartyClientId + "&scope=openid%20profile");
    }

    #endregion

    #region Front-Channel Logout Session Id

    [Fact]
    public async Task Authorize_CookieWithoutSid_MintsSidAndReissuesCookie()
    {
        // A cookie without a SID needs a session and an updated cookie.
        OpenIddictRequest request = new() { ClientId = FirstPartyClientId, Scope = "openid" };

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupClientTenantResolver(FirstPartyClientId);


        IActionResult result = await _controller.Authorize();


        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        string? sid = signIn.Principal.GetSessionId();
        sid.Should().NotBeNullOrEmpty();

        await _authenticationService.Received(1).SignInAsync(
            Arg.Any<HttpContext>(),
            IdentityConstants.ApplicationScheme,
            Arg.Is<ClaimsPrincipal>(p => p.GetSessionId() == sid),
            Arg.Any<AuthenticationProperties>());
    }

    [Fact]
    public async Task Authorize_CookieWithLiveSid_ReusesItWithoutReissuingCookie()
    {
        // Reuse the SID while its session remains active.
        OpenIddictRequest request = new() { ClientId = FirstPartyClientId, Scope = "openid" };

        ActiveSession session = ArrangeLiveSession();
        SetupAuthenticatedHttpContext(request, existingSid: session.Sid);
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupClientTenantResolver(FirstPartyClientId);


        IActionResult result = await _controller.Authorize();


        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        signIn.Principal.GetSessionId().Should().Be(session.Sid);

        await _authenticationService.DidNotReceive().SignInAsync(
            Arg.Any<HttpContext>(),
            Arg.Any<string?>(),
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<AuthenticationProperties>());
    }

    [Fact]
    public async Task Authorize_CookieWithRevokedSid_MintsAFreshSessionAndReissuesCookie()
    {
        // An empty active-session result models a cookie whose SID is no longer live.
        OpenIddictRequest request = new() { ClientId = FirstPartyClientId, Scope = "openid" };
        string deadSid = Guid.NewGuid().ToString("N");

        SetupAuthenticatedHttpContext(request, existingSid: deadSid);
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupClientTenantResolver(FirstPartyClientId);


        IActionResult result = await _controller.Authorize();


        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        string? sid = signIn.Principal.GetSessionId();
        sid.Should().NotBeNullOrEmpty();
        sid.Should().NotBe(deadSid);

        await _authenticationService.Received(1).SignInAsync(
            Arg.Any<HttpContext>(),
            IdentityConstants.ApplicationScheme,
            Arg.Is<ClaimsPrincipal>(p => p.GetSessionId() == sid),
            Arg.Any<AuthenticationProperties>());
    }

    [Fact]
    public async Task Authorize_RecordsClientParticipationInTheSsoSession()
    {
        // Record participation so logout can find this relying party.
        OpenIddictRequest request = new() { ClientId = FirstPartyClientId, Scope = "openid" };

        ActiveSession session = ArrangeLiveSession();
        SetupAuthenticatedHttpContext(request, existingSid: session.Sid);
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupClientTenantResolver(FirstPartyClientId);


        await _controller.Authorize();


        await _ssoClientSessionService.Received(1).RecordAsync(
            session.Sid,
            FirstPartyClientId,
            Guid.Parse(_testUserId),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Returns an active session from the session-service stub.
    /// </summary>
    private ActiveSession ArrangeLiveSession()
    {
        ActiveSession session = ActiveSession.Create(
            Guid.Parse(_testUserId), Guid.Empty, TimeSpan.FromHours(24), TimeProvider.System);
        _sessionService
            .GetActiveSessionsAsync(Guid.Parse(_testUserId), Arg.Any<CancellationToken>())
            .Returns([session]);
        return session;
    }

    [Fact]
    public async Task Authorize_SidClaimIsDestinedForTheIdentityTokenOnly()
    {
        // This contract exposes the logout SID only in the identity token.
        OpenIddictRequest request = new() { ClientId = FirstPartyClientId, Scope = "openid" };

        SetupAuthenticatedHttpContext(request, existingSid: "sid-already-minted");
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupClientTenantResolver(FirstPartyClientId);


        IActionResult result = await _controller.Authorize();


        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        Claim sidClaim = signIn.Principal!.Claims
            .Should().ContainSingle(c => c.Type == ClaimsPrincipalExtensions.SessionIdClaimType).Subject;
        sidClaim.GetDestinations().Should().BeEquivalentTo(
            [OpenIddictConstants.Destinations.IdentityToken]);
    }

    #endregion

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
    {
        foreach (T item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}
