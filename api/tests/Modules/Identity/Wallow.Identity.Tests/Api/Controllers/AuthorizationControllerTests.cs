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
using static OpenIddict.Abstractions.OpenIddictConstants;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Helpers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Kernel.Extensions;

#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()

namespace Wallow.Identity.Tests.Api.Controllers;

public sealed class AuthorizationControllerTests : IDisposable
{
    private static readonly string _testUserId = Guid.NewGuid().ToString();
    private static readonly Guid _testOrganizationId = Guid.NewGuid();
    private const string ThirdPartyClientId = "my-external-app";
    // Deliberately NOT prefixed "wallow-": first-party status is the application's consent type,
    // written by the seed, and the authorize endpoint must never infer it from the id.
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

        // A user with no memberships is the default; a test about the single-membership default
        // for an org-less first-party login supplies one.
        _organizations = Substitute.For<IOrganizationService>();
        _organizations.GetMyOrganizationsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Minting is opaque here; redemption defaults to the happy path and a test that is about
        // a refused token overrides it.
        _consentTokens = Substitute.For<IConsentTokenService>();
        _consentTokens.Issue(Arg.Any<string>(), Arg.Any<string>()).Returns(ConsentToken);
        _consentTokens
            .RedeemAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ConsentTokenOutcome.Redeemed);

        // These tests are about consent, not scope gating: let every requested scope through.
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

        // Set up the OpenIddict server transaction on the feature collection
        OpenIddictServerTransaction transaction = new() { Request = request };
        httpContext.Features.Set(new OpenIddictServerAspNetCoreFeature { Transaction = transaction });

        httpContext.Request.Method = method;
        httpContext.Request.Path = "/connect/authorize";
        httpContext.Request.QueryString = new QueryString(queryString ?? "?client_id=" + (request.ClientId ?? ThirdPartyClientId));

        // Sid minting re-issues the identity cookie through IAuthenticationService, which the
        // HttpContext.AuthenticateAsync/SignInAsync extensions resolve from RequestServices.
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

        // Mock Url helper for IsLocalUrl
        IUrlHelper urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.IsLocalUrl(Arg.Any<string>()).Returns(true);
        _controller.Url = urlHelper;
    }

    /// <summary>
    /// The same request plumbing with nobody signed in: the identity cookie has lapsed (or was
    /// never there), so authorize has to bounce to login without losing the request.
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
        _authorizationManager.GetScopesAsync(authorization, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(scopes));
    }

    /// <summary>
    /// Binds the client to an organization the signed-in user is admitted to. Both halves are
    /// required: authorize refuses a client bound to no organization, and refuses a caller the
    /// enrollment service does not admit, so a consent test never reaches consent without them.
    /// </summary>
    /// <summary>An unbound (first-party) client: the resolver answers with no organization.</summary>
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
        // Arrange
        OpenIddictRequest request = ConsentDecision(AuthorizationController.ConsentDenied);

        SetupAuthenticatedHttpContext(request, method: "POST");
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupNoExistingAuthorizations();
        SetupClientTenantResolver(ThirdPartyClientId);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert - a denial is delivered to the relying party as consent_required
        ForbidResult forbidResult = result.Should().BeOfType<ForbidResult>().Subject;
        forbidResult.AuthenticationSchemes.Should().Contain(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        forbidResult.Properties!.Items[OpenIddictServerAspNetCoreConstants.Properties.Error]
            .Should().Be(Errors.ConsentRequired);
    }

    [Fact]
    public async Task Authorize_WithConsentGranted_NoExistingAuthorization_CreatesAuthorizationAndReturnsSignIn()
    {
        // Arrange
        OpenIddictRequest request = ConsentDecision(AuthorizationController.ConsentGranted);

        SetupAuthenticatedHttpContext(request, method: "POST");
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupNoExistingAuthorizations();
        SetupClientTenantResolver(ThirdPartyClientId);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert - the grant is recorded once and the code issued
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>();
        await _authorizationManager.Received(1).CreateAsync(
            Arg.Any<OpenIddictAuthorizationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_WithConsentGranted_ExistingValidAuthorization_DoesNotCreateDuplicateAuthorization()
    {
        // Arrange
        OpenIddictRequest request = ConsentDecision(AuthorizationController.ConsentGranted);

        SetupAuthenticatedHttpContext(request, method: "POST");
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupExistingValidAuthorization(ApplicationId, ["openid", "profile"]);
        SetupClientTenantResolver(ThirdPartyClientId);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert - a valid authorization already covers the scopes, so none is added
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>();
        await _authorizationManager.DidNotReceive().CreateAsync(
            Arg.Any<OpenIddictAuthorizationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("granted")]
    [InlineData("denied")]
    public async Task Authorize_WithAConsentDecisionOnTheGet_IgnoresItAndShowsTheConsentScreen(string decision)
    {
        // Arrange - a decision smuggled onto a link. The token is even "valid" here, so the GET
        // being refused is down to the method alone.
        OpenIddictRequest request = ConsentDecision(decision);

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupNoExistingAuthorizations();
        SetupClientTenantResolver(ThirdPartyClientId);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert - neither recorded nor delivered: the screen is shown
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
        // Arrange
        OpenIddictRequest request = ConsentDecision(AuthorizationController.ConsentGranted);
        _consentTokens
            .RedeemAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(outcome);

        SetupAuthenticatedHttpContext(request, method: "POST");
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupNoExistingAuthorizations();
        SetupClientTenantResolver(ThirdPartyClientId);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert - a fresh token, nothing granted
        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(new Uri(redirect.Url).Query);
        query[AuthorizationController.ConsentTokenParameter].ToString().Should().Be(ConsentToken);
        await _authorizationManager.DidNotReceive().CreateAsync(
            Arg.Any<OpenIddictAuthorizationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_WithAConsentDecision_RedeemsTheTokenForTheSignedInUserAndTheRequest()
    {
        // Arrange - the binding the token exists for: the redemption names the user who is
        // answering and digests the request being answered, and the digest excludes the decision
        // itself so it matches the one the token was minted against.
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

        // Act
        await _controller.Authorize();

        // Assert
        await _consentTokens.Received(1).RedeemAsync(ConsentToken, _testUserId, mintedFor, Arg.Any<CancellationToken>());
    }

    #endregion

    #region First-Party Client Skips Consent

    [Fact]
    public async Task Authorize_FirstPartyClient_WithConsentParameter_SkipsConsentLogicAndReturnsSignIn()
    {
        // Arrange
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

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert - a client registered with implicit consent skips consent entirely
        // and goes directly to token issuance. No consent is recorded.
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>();

        // First-party clients never record a permanent (consent) authorization
        await _authorizationManager.DidNotReceive().CreateAsync(
            Arg.Is<OpenIddictAuthorizationDescriptor>(d => d.Type == AuthorizationTypes.Permanent),
            Arg.Any<CancellationToken>());

        // Consent-related authorization lookups should not happen for first-party clients
        _authorizationManager.DidNotReceive().FindBySubjectAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_FirstPartyClient_BoundToNoOrganization_SignsInWithAnOrgLessToken()
    {
        // A first-party client is bound to no organization, so "no organization" is not an
        // error for it: for a user who belongs to no organization the login completes with no
        // org claims and no roles, and nothing is enrolled anywhere.
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
        // A first-party client names no organization, so the user's own memberships decide the
        // organization context: exactly one active membership is unambiguous and becomes the
        // token's org_id, with the roles that organization grants.
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
        // Several memberships and no hint is ambiguous; the token names no organization rather
        // than guessing one.
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
        // The id looks first-party; the consent type says otherwise, and only the consent type
        // counts. A third-party client with no organization is a registration defect.
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
        // The hint is what lets a first-party login name an organization: the transaction runs
        // the hinted organization's enrollment policy exactly as a bound client's would, and the
        // token is scoped to it — even when the user belongs to several and no default applies.
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
        // A first-party refusal stays on the auth host: the hinted organization's policy said no,
        // and the error page is where that is explained.
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
        // A bound client's organization is fixed by registration; a hint naming any other one is
        // a malformed request, answered to the relying party as such.
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
        // The one code path: a bound client is "hint fixed by registration", so restating that
        // organization is not a contradiction.
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
        // A first-party client is bound to no organization, so its tokens' organization has to be
        // recorded somewhere revocation can find it: the authorization the tokens chain to.
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
    }

    [Fact]
    public async Task Authorize_FirstPartyClient_WithoutAnOrganization_RecordsNoAuthorization()
    {
        // An org-less token has nothing to revoke per organization; OpenIddict's own ad-hoc
        // tracking is all it needs.
        OpenIddictRequest request = new() { ClientId = FirstPartyClientId, Scope = "openid profile" };

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupUnboundClientTenantResolver(FirstPartyClientId);
        _organizations.GetMyOrganizationsAsync(Guid.Parse(_testUserId), Arg.Any<CancellationToken>())
            .Returns([]);

        IActionResult result = await _controller.Authorize();

        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        signIn.Principal.GetAuthorizationId().Should().BeNull();
        await _authorizationManager.DidNotReceive().CreateAsync(
            Arg.Any<OpenIddictAuthorizationDescriptor>(), Arg.Any<CancellationToken>());
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
        // A third-party user the organization refuses is the relying party's to handle: the
        // refusal goes back to its redirect URI as access_denied, with the reason as the
        // description, rather than stranding the person on the auth host.
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
        // Pending is still recorded (the enrollment service did that); the relying party is told
        // the person is waiting rather than shown the auth host's request-submitted screen.
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
        // Verifying an email is the auth host's job, not the relying party's, so this one
        // precondition is not an organization's refusal and keeps its error page.
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
        // Arrange - a third-party client with no consent decision yet: the branch
        // that hands the user to the consent screen. The scopes the client asked
        // for are the entire substance of the decision the screen asks the user to
        // make, so the redirect has to carry them.
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

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert
        RedirectResult redirectResult = result.Should().BeOfType<RedirectResult>().Subject;
        redirectResult.Url.Should().StartWith("https://auth.example.com/consent?");

        Dictionary<string, StringValues> query =
            QueryHelpers.ParseQuery(new Uri(redirectResult.Url).Query);

        // Space-delimited, matching OAuth's own scope convention and the
        // space-split the consent-info endpoint already parses with.
        query.Should().ContainKey("scope");
        query["scope"].ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Should().BeEquivalentTo("openid", "profile");

        // The token the decision has to come back with, minted for this user.
        query[AuthorizationController.ConsentTokenParameter].ToString().Should().Be(ConsentToken);
        _consentTokens.Received(1).Issue(_testUserId, Arg.Any<string>());
    }

    [Fact]
    public async Task Authorize_ThirdPartyClient_NoExistingAuthorization_KeepsReturnUrlAndClientIdOnTheConsentRedirect()
    {
        // Arrange - the two parameters the consent screen relies on. The returnUrl is rebuilt
        // from the authorize request's own parameters, not read off the URL: a decision that
        // arrives by POST carries them in its body, and a link may carry a decision that must
        // not come back.
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

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert
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
        // Arrange - the identity cookie lapsed while the consent screen sat open, so the decision
        // arrives from nobody. A POST carries the authorize request in its body, not the URL,
        // so the login returnUrl has to be rebuilt from the request rather than the query string
        // - and the stale decision must not come back with it.
        OpenIddictRequest request = ConsentDecision(AuthorizationController.ConsentGranted);
        SetupAnonymousHttpContext(request, "POST");

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert
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
        // Arrange - a session signed in before front-channel logout existed carries no sid,
        // so authorize has to mint one and write it back onto the identity cookie.
        OpenIddictRequest request = new() { ClientId = FirstPartyClientId, Scope = "openid" };

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupClientTenantResolver(FirstPartyClientId);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert
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
    public async Task Authorize_CookieWithSid_ReusesItWithoutReissuingCookie()
    {
        // Arrange - the sid identifies the SSO session for its whole lifetime; a second
        // authorize (another RP joining the session) must reuse it, not rotate it.
        OpenIddictRequest request = new() { ClientId = FirstPartyClientId, Scope = "openid" };

        SetupAuthenticatedHttpContext(request, existingSid: "sid-already-minted");
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupClientTenantResolver(FirstPartyClientId);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert
        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        signIn.Principal.GetSessionId().Should().Be("sid-already-minted");

        await _authenticationService.DidNotReceive().SignInAsync(
            Arg.Any<HttpContext>(),
            Arg.Any<string?>(),
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<AuthenticationProperties>());
    }

    [Fact]
    public async Task Authorize_RecordsClientParticipationInTheSsoSession()
    {
        // Arrange - logout can only notify the RPs it knows joined the session, so every
        // successful authorize records (sid, client) participation.
        OpenIddictRequest request = new() { ClientId = FirstPartyClientId, Scope = "openid" };

        SetupAuthenticatedHttpContext(request, existingSid: "sid-already-minted");
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupClientTenantResolver(FirstPartyClientId);

        // Act
        await _controller.Authorize();

        // Assert
        await _ssoClientSessionService.Received(1).RecordAsync(
            "sid-already-minted",
            FirstPartyClientId,
            Guid.Parse(_testUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_SidClaimIsDestinedForTheIdentityTokenOnly()
    {
        // Arrange - sid exists for the RP to match logout notifications against; access-token
        // consumers have no use for it, so it must not leak there.
        OpenIddictRequest request = new() { ClientId = FirstPartyClientId, Scope = "openid" };

        SetupAuthenticatedHttpContext(request, existingSid: "sid-already-minted");
        SetupUser();
        SetupApplication(FirstPartyClientId, consentType: ConsentTypes.Implicit);
        SetupClientTenantResolver(FirstPartyClientId);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert
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
