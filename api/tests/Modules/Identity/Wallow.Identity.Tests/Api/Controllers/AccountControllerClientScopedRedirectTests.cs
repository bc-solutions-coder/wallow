using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Identity.Tests.Api.Controllers;

/// <summary>
/// IRedirectUriValidator.IsAllowedAsync scopes redirect validation to a client_id, but every
/// AccountController call site still calls it without one, so each falls back to the union of
/// EVERY registered client's origins. These tests pin the contract: each endpoint that validates
/// a redirect must pass the requesting client's id, so an origin registered by another client
/// cannot be admitted.
/// </summary>
public class AccountControllerClientScopedRedirectTests
{
    private const string AuthUrl = "http://localhost:5002";
    private const string ClientId = "client-a";

    /// <summary>An origin registered by client-a only — allowed for client-a, denied for anyone else.</summary>
    private const string ClientAUrl = "https://a.example.com/callback";

    private const string TestEmail = "client-scoped@test.com";
    private const string TestPassword = "Password123!";

    private readonly AccountController _controller;
    private readonly SignInManager<WallowUser> _signInManager;
    private readonly UserManager<WallowUser> _userManager;
    private readonly IRedirectUriValidator _redirectUriValidator;
    private readonly IAuthenticationSchemeProvider _authSchemeProvider;
    private readonly IClientTenantResolver _clientTenantResolver;
    private readonly IMessageBus _messageBus;
    private readonly IDatabase _redisDb;

    public AccountControllerClientScopedRedirectTests()
    {
        _userManager = Substitute.For<UserManager<WallowUser>>(
            Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null);

        _signInManager = Substitute.For<SignInManager<WallowUser>>(
            _userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<WallowUser>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<ILogger<SignInManager<WallowUser>>>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<WallowUser>>());

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthUrl"] = AuthUrl
            })
            .Build();

        _redirectUriValidator = Substitute.For<IRedirectUriValidator>();
        _authSchemeProvider = Substitute.For<IAuthenticationSchemeProvider>();
        _clientTenantResolver = Substitute.For<IClientTenantResolver>();
        _messageBus = Substitute.For<IMessageBus>();

        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        _redisDb = Substitute.For<IDatabase>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_redisDb);

        _controller = new AccountController(
            _signInManager,
            configuration,
            _redirectUriValidator,
            new EphemeralDataProtectionProvider(),
            _authSchemeProvider,
            _messageBus,
            _clientTenantResolver,
            Substitute.For<IPasswordlessService>(),
            Substitute.For<IMfaExemptionChecker>(),
            Substitute.For<IMfaService>(),
            Substitute.For<IMfaPartialAuthService>(),
            Substitute.For<IOrganizationMfaPolicyService>(),
            Substitute.For<IMfaLockoutService>(),
            redis,
            Substitute.For<ILogger<AccountController>>(),
            TimeProvider.System);

        DefaultHttpContext httpContext = new();
        IAuthenticationService authService = Substitute.For<IAuthenticationService>();
        authService.SignOutAsync(Arg.Any<HttpContext>(), Arg.Any<string>(), Arg.Any<AuthenticationProperties>())
            .Returns(Task.CompletedTask);
        httpContext.RequestServices = new TestServiceProvider(authService);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        IUrlHelper urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.Action(Arg.Any<UrlActionContext>())
            .Returns("http://localhost:5001/v1/identity/auth/external-login-callback");
        _controller.Url = urlHelper;

        // Ticket has not been exchanged before — the replay guard lets it through.
        _redisDb.StringSetAsync(
                Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);
    }

    /// <summary>
    /// Models the per-client allow list: <paramref name="uri"/> validates only when the validator
    /// is told which client is asking, and only for <paramref name="clientId"/>. A call that omits
    /// the client id (today's behaviour) is refused, so any endpoint that fails to thread the
    /// request's client id through cannot pass.
    /// </summary>
    private void AllowOnlyForClient(string uri, string clientId)
    {
        _redirectUriValidator
            .IsAllowedAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _redirectUriValidator
            .IsAllowedAsync(uri, clientId, Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private void SetupRegisteredProvider(string provider = "Google")
    {
        _authSchemeProvider.GetSchemeAsync(provider)
            .Returns(new AuthenticationScheme(provider, provider, typeof(IAuthenticationHandler)));
    }

    private void SetupExternalLoginInfo(string provider = "Google")
    {
        ClaimsIdentity identity = new(new[]
        {
            new Claim(ClaimTypes.Email, TestEmail),
            new Claim("email_verified", "true")
        });
        ExternalLoginInfo info = new(new ClaimsPrincipal(identity), provider, "provider-key-123", provider);

        _signInManager.GetExternalLoginInfoAsync(Arg.Any<string>()).Returns(info);
        _signInManager.ExternalLoginSignInAsync(provider, "provider-key-123", false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _userManager.FindByEmailAsync(TestEmail).Returns((WallowUser?)null);
    }

    /// <summary>Mints a real sign-in ticket the way the password login path does.</summary>
    private async Task<string> CreateTicketViaLogin()
    {
        WallowUser user = WallowUser.Create("Test", "User", TestEmail, TimeProvider.System);
        _userManager.FindByEmailAsync(TestEmail).Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, TestPassword, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        IActionResult loginResult = await _controller.Login(
            new AccountLoginRequest(TestEmail, TestPassword, false), CancellationToken.None);

        OkObjectResult ok = loginResult.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("signInTicket").GetString()!;
    }

    #region ExternalLogin

    [Fact]
    public async Task ExternalLogin_ValidatesReturnUrlAgainstRequestClientId()
    {
        SetupRegisteredProvider();
        AllowOnlyForClient(ClientAUrl, ClientId);

        IActionResult result = await _controller.ExternalLogin("Google", ClientAUrl, ClientId);

        result.Should().BeOfType<ChallengeResult>();
    }

    [Fact]
    public async Task ExternalLogin_PassesRequestClientIdToRedirectValidator()
    {
        SetupRegisteredProvider();
        AllowOnlyForClient(ClientAUrl, ClientId);

        await _controller.ExternalLogin("Google", ClientAUrl, ClientId);

        await _redirectUriValidator.Received(1)
            .IsAllowedAsync(ClientAUrl, ClientId, Arg.Any<CancellationToken>());
    }

    #endregion

    #region ExternalLoginCallback

    [Fact]
    public async Task ExternalLoginCallback_ValidatesReturnUrlAgainstRequestClientId()
    {
        AllowOnlyForClient(ClientAUrl, ClientId);
        SetupExternalLoginInfo();

        IActionResult result = await _controller.ExternalLoginCallback(ClientAUrl, ClientId);

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be(ClientAUrl);
    }

    [Fact]
    public async Task ExternalLoginCallback_PassesRequestClientIdToRedirectValidator()
    {
        AllowOnlyForClient(ClientAUrl, ClientId);
        SetupExternalLoginInfo();

        await _controller.ExternalLoginCallback(ClientAUrl, ClientId);

        await _redirectUriValidator.Received(1)
            .IsAllowedAsync(ClientAUrl, ClientId, Arg.Any<CancellationToken>());
    }

    #endregion

    #region CompleteExternalRegistration

    [Fact]
    public async Task CompleteExternalRegistration_ValidatesReturnUrlAgainstRequestClientId()
    {
        AllowOnlyForClient(ClientAUrl, ClientId);

        IActionResult result = await _controller.CompleteExternalRegistration(
            acceptedTerms: false, returnUrl: ClientAUrl, clientId: ClientId);

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain(Uri.EscapeDataString(ClientAUrl));
    }

    [Fact]
    public async Task CompleteExternalRegistration_PassesRequestClientIdToRedirectValidator()
    {
        AllowOnlyForClient(ClientAUrl, ClientId);

        await _controller.CompleteExternalRegistration(
            acceptedTerms: false, returnUrl: ClientAUrl, clientId: ClientId);

        await _redirectUriValidator.Received(1)
            .IsAllowedAsync(ClientAUrl, ClientId, Arg.Any<CancellationToken>());
    }

    #endregion

    #region ExchangeTicket

    [Fact]
    public async Task ExchangeTicket_ValidatesReturnUrlAgainstRequestClientId()
    {
        AllowOnlyForClient(ClientAUrl, ClientId);
        string ticket = await CreateTicketViaLogin();

        IActionResult result = await _controller.ExchangeTicket(ticket, ClientAUrl, ClientId);

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be(ClientAUrl);
    }

    [Fact]
    public async Task ExchangeTicket_PassesRequestClientIdToRedirectValidator()
    {
        AllowOnlyForClient(ClientAUrl, ClientId);
        string ticket = await CreateTicketViaLogin();

        await _controller.ExchangeTicket(ticket, ClientAUrl, ClientId);

        await _redirectUriValidator.Received(1)
            .IsAllowedAsync(ClientAUrl, ClientId, Arg.Any<CancellationToken>());
    }

    #endregion

    #region ValidateRedirectUri

    [Fact]
    public async Task ValidateRedirectUri_ValidatesAgainstRequestClientId()
    {
        AllowOnlyForClient(ClientAUrl, ClientId);

        IActionResult result = await _controller.ValidateRedirectUri(ClientAUrl, ClientId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("true");
    }

    [Fact]
    public async Task ValidateRedirectUri_PassesRequestClientIdToRedirectValidator()
    {
        AllowOnlyForClient(ClientAUrl, ClientId);

        await _controller.ValidateRedirectUri(ClientAUrl, ClientId, CancellationToken.None);

        await _redirectUriValidator.Received(1)
            .IsAllowedAsync(ClientAUrl, ClientId, Arg.Any<CancellationToken>());
    }

    #endregion

    #region SignOut

    [Fact]
    public async Task SignOut_ValidatesPostLogoutRedirectUriAgainstRequestClientId()
    {
        AllowOnlyForClient(ClientAUrl, ClientId);

        IActionResult result = await _controller.SignOut(ClientAUrl, ClientId);

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("post_logout_redirect_uri=");
    }

    [Fact]
    public async Task SignOut_PassesRequestClientIdToRedirectValidator()
    {
        AllowOnlyForClient(ClientAUrl, ClientId);

        await _controller.SignOut(ClientAUrl, ClientId);

        await _redirectUriValidator.Received(1)
            .IsAllowedAsync(ClientAUrl, ClientId, Arg.Any<CancellationToken>());
    }

    #endregion

    #region Register

    [Fact]
    public async Task Register_ValidatesReturnUrlAgainstRequestClientId()
    {
        AllowOnlyForClient(ClientAUrl, ClientId);
        SetupSuccessfulRegistration();

        await _controller.Register(new AccountRegisterRequest(
            TestEmail, TestPassword, TestPassword, ClientId, null, ClientAUrl));

        await _messageBus.Received(1).PublishAsync(Arg.Is<EmailVerificationRequestedEvent>(e =>
            e.VerifyUrl.Contains($"returnUrl={Uri.EscapeDataString(ClientAUrl)}", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Register_PassesRequestClientIdToRedirectValidator()
    {
        AllowOnlyForClient(ClientAUrl, ClientId);
        SetupSuccessfulRegistration();

        await _controller.Register(new AccountRegisterRequest(
            TestEmail, TestPassword, TestPassword, ClientId, null, ClientAUrl));

        await _redirectUriValidator.Received(1)
            .IsAllowedAsync(ClientAUrl, ClientId, Arg.Any<CancellationToken>());
    }

    private void SetupSuccessfulRegistration()
    {
        _clientTenantResolver.ResolveAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns(new ClientTenantInfo(Guid.Empty, "Client A Org"));
        _userManager.CreateAsync(Arg.Any<WallowUser>(), TestPassword).Returns(IdentityResult.Success);
        _userManager.GenerateEmailConfirmationTokenAsync(Arg.Any<WallowUser>()).Returns("token123");
    }

    #endregion

    private sealed class TestServiceProvider(IAuthenticationService authenticationService) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IAuthenticationService))
            {
                return authenticationService;
            }
            return null;
        }
    }
}
