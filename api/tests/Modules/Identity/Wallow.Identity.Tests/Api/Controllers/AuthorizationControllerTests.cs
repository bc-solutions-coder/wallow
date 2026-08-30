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
    private const string FirstPartyClientId = "wallow-web";
    private const string ApplicationId = "app-id-123";

    private readonly UserManager<WallowUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictAuthorizationManager _authorizationManager;
    private readonly IScopeSubsetValidator _scopeSubsetValidator;
    private readonly IClientTenantResolver _clientTenantResolver;
    private readonly IUserEnrollmentService _enrollment;
    private readonly IMembershipRoleResolver _membershipRoleResolver;
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

    private void SetupApplication(string clientId, string applicationId = ApplicationId)
    {
        object application = new();
        _applicationManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(application));
        _applicationManager.GetClientIdAsync(application, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(clientId));
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
        await _controller.Authorize();
        string mintedFor = (string)_consentTokens.ReceivedCalls().Single().GetArguments()[1]!;

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
        SetupApplication(FirstPartyClientId);
        SetupClientTenantResolver(FirstPartyClientId);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert - first-party clients (wallow-* prefix) skip consent entirely
        // and go directly to token issuance. No authorization should be created.
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>();

        // First-party clients should never trigger authorization creation
        await _authorizationManager.DidNotReceive().CreateAsync(
            Arg.Any<OpenIddictAuthorizationDescriptor>(), Arg.Any<CancellationToken>());

        // Consent-related authorization lookups should not happen for first-party clients
        _authorizationManager.DidNotReceive().FindBySubjectAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
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
            ["consent_granted"] = "true"
        };

        SetupAuthenticatedHttpContext(request, "?client_id=" + ThirdPartyClientId + "&scope=openid%20profile&consent_granted=true");
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
        SetupApplication(FirstPartyClientId);
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
        SetupApplication(FirstPartyClientId);
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
        SetupApplication(FirstPartyClientId);
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
        SetupApplication(FirstPartyClientId);
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
