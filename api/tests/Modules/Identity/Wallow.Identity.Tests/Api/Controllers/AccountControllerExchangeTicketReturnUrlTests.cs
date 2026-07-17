using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wolverine;

namespace Wallow.Identity.Tests.Api.Controllers;

/// <summary>
/// Covers the returnUrl contract of the exchange-ticket endpoint (Wallow-vec7.3.18).
/// The external-login callback hands /mfa/challenge an ABSOLUTE returnUrl (normalized through
/// IRedirectUriValidator.IsAllowedAsync), and the MFA screen threads it straight back into
/// exchange-ticket. exchange-ticket must therefore honour an allow-listed absolute returnUrl
/// while still refusing a non-allow-listed one, and still honour local/relative returnUrls.
/// </summary>
public class AccountControllerExchangeTicketReturnUrlTests
{
    private const string AuthUrl = "http://localhost:5002";
    private const string TestEmail = "exchange-returnurl@test.com";
    private const string TestPassword = "Password123!";

    private readonly AccountController _controller;
    private readonly UserManager<WallowUser> _userManager;
    private readonly SignInManager<WallowUser> _signInManager;
    private readonly IRedirectUriValidator _redirectUriValidator;
    private readonly IDatabase _redisDb;

    public AccountControllerExchangeTicketReturnUrlTests()
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

        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        _redisDb = Substitute.For<IDatabase>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_redisDb);

        _controller = new AccountController(
            _signInManager,
            configuration,
            _redirectUriValidator,
            new EphemeralDataProtectionProvider(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IMessageBus>(),
            Substitute.For<IClientTenantResolver>(),
            Substitute.For<IOrganizationService>(),
            Substitute.For<IDomainAssignmentService>(),
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

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Real UrlHelper so IsLocalUrl keeps its production semantics (relative-only).
        _controller.Url = new UrlHelper(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()));

        // Ticket has not been exchanged before — replay guard lets it through.
        _redisDb.StringSetAsync(
                Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);
    }

    /// <summary>
    /// Mints a real sign-in ticket the same way the password login path does, so the
    /// exchange-ticket assertions exercise the genuine protected payload.
    /// </summary>
    private async Task<string> CreateTicketViaLogin()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", TestEmail, TimeProvider.System);
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

    #region AC1 — absolute returnUrl admitted by the allow-list is honoured

    [Fact]
    public async Task ExchangeTicket_WithAllowListedAbsoluteReturnUrl_RedirectsToReturnUrl()
    {
        const string absoluteReturnUrl = "https://app.example.com/dashboard";
        _redirectUriValidator.IsAllowedAsync(absoluteReturnUrl, Arg.Any<CancellationToken>())
            .Returns(true);

        string ticket = await CreateTicketViaLogin();

        IActionResult result = await _controller.ExchangeTicket(ticket, absoluteReturnUrl);

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be(absoluteReturnUrl);
    }

    [Fact]
    public async Task ExchangeTicket_WithAllowListedAbsoluteReturnUrl_StillSignsUserIn()
    {
        const string absoluteReturnUrl = "https://app.example.com/dashboard";
        _redirectUriValidator.IsAllowedAsync(absoluteReturnUrl, Arg.Any<CancellationToken>())
            .Returns(true);

        string ticket = await CreateTicketViaLogin();

        await _controller.ExchangeTicket(ticket, absoluteReturnUrl);

        await _signInManager.Received(1).SignInAsync(
            Arg.Is<WallowUser>(u => u.Email == TestEmail), Arg.Any<bool>(), Arg.Any<string?>());
    }

    #endregion

    #region AC2 — local/relative returnUrl still honoured (password path, no regression)

    [Theory]
    [InlineData("/dashboard")]
    [InlineData("/organizations/settings?tab=security")]
    public async Task ExchangeTicket_WithLocalReturnUrl_RedirectsToReturnUrl(string localReturnUrl)
    {
        string ticket = await CreateTicketViaLogin();

        IActionResult result = await _controller.ExchangeTicket(ticket, localReturnUrl);

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be(localReturnUrl);
    }

    #endregion

    #region AC3 — non-allow-listed absolute returnUrl falls back to authUrl (open-redirect refusal)

    [Theory]
    [InlineData("https://evil.example.com")]
    [InlineData("https://evil.example.com/steal?token=1")]
    [InlineData("//evil.example.com/protocol-relative")]
    public async Task ExchangeTicket_WithNonAllowListedAbsoluteReturnUrl_FallsBackToAuthUrl(string evilReturnUrl)
    {
        _redirectUriValidator.IsAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        string ticket = await CreateTicketViaLogin();

        IActionResult result = await _controller.ExchangeTicket(ticket, evilReturnUrl);

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be(AuthUrl);
    }

    #endregion

    #region AC4 — blank/missing returnUrl falls back to authUrl

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ExchangeTicket_WithBlankReturnUrl_FallsBackToAuthUrl(string? blankReturnUrl)
    {
        string ticket = await CreateTicketViaLogin();

        IActionResult result = await _controller.ExchangeTicket(ticket, blankReturnUrl);

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be(AuthUrl);
    }

    [Fact]
    public async Task ExchangeTicket_WithBlankReturnUrl_DoesNotConsultAllowList()
    {
        string ticket = await CreateTicketViaLogin();

        await _controller.ExchangeTicket(ticket, null);

        await _redirectUriValidator.DidNotReceive()
            .IsAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    #endregion
}
