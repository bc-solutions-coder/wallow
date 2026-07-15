using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

public class AccountControllerTicketReplayTests
{
    private readonly AccountController _controller;
    private readonly UserManager<WallowUser> _userManager;
    private readonly SignInManager<WallowUser> _signInManager;
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _redisDb;

    private const string TestEmail = "replay@test.com";
    private const string TestPassword = "Password123!";

    public AccountControllerTicketReplayTests()
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
                ["AuthUrl"] = "http://localhost:5002"
            })
            .Build();

        _redis = Substitute.For<IConnectionMultiplexer>();
        _redisDb = Substitute.For<IDatabase>();
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_redisDb);

        _controller = new AccountController(
            _signInManager,
            configuration,
            Substitute.For<IRedirectUriValidator>(),
            new EphemeralDataProtectionProvider(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IMessageBus>(),
            Substitute.For<IClientTenantResolver>(),
            Substitute.For<IOrganizationService>(),
            Substitute.For<IPasswordlessService>(),
            Substitute.For<IMfaExemptionChecker>(),
            Substitute.For<IMfaService>(),
            Substitute.For<IMfaPartialAuthService>(),
            Substitute.For<IOrganizationMfaPolicyService>(),
            Substitute.For<IMfaLockoutService>(),
            _redis,
            Substitute.For<ILogger<AccountController>>(),
            TimeProvider.System);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

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

    #region First ticket exchange succeeds

    [Fact]
    public async Task ExchangeTicket_FirstUse_Succeeds()
    {
        string ticket = await CreateTicketViaLogin();

        // Redis StringSetAsync returns true = key was set (not already present) = first use
        _redisDb.StringSetAsync(
                Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);

        IActionResult result = await _controller.ExchangeTicket(ticket, null);

        // Should redirect (success), not return 401
        result.Should().BeOfType<RedirectResult>();
    }

    #endregion

    #region Second ticket exchange returns 401 ticket_already_used

    [Fact]
    public async Task ExchangeTicket_SecondUse_Returns401WithTicketAlreadyUsed()
    {
        string ticket = await CreateTicketViaLogin();

        // Redis StringSetAsync returns false = key already existed = replay attempt
        _redisDb.StringSetAsync(
                Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(false);

        IActionResult result = await _controller.ExchangeTicket(ticket, null);

        UnauthorizedObjectResult unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(unauthorized.Value);
        json.Should().Contain("ticket_already_used");
    }

    #endregion

    #region Concurrent replay — second request loses the race

    [Fact]
    public async Task ExchangeTicket_ConcurrentRace_SecondRequestFails()
    {
        string ticket = await CreateTicketViaLogin();

        // Simulate race: first call returns true, second call returns false
        _redisDb.StringSetAsync(
                Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true, false);

        // First exchange succeeds
        IActionResult result1 = await _controller.ExchangeTicket(ticket, null);
        result1.Should().BeOfType<RedirectResult>();

        // Second exchange with same ticket fails
        IActionResult result2 = await _controller.ExchangeTicket(ticket, null);
        UnauthorizedObjectResult unauthorized = result2.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(unauthorized.Value);
        json.Should().Contain("ticket_already_used");
    }

    #endregion

    #region Invalid/expired ticket fails without touching Redis

    [Fact]
    public async Task ExchangeTicket_InvalidTicket_FailsWithoutTouchingRedis()
    {
        IActionResult result = await _controller.ExchangeTicket("completely-invalid-ticket", null);

        result.Should().BeOfType<BadRequestObjectResult>();
        string json = System.Text.Json.JsonSerializer.Serialize(((BadRequestObjectResult)result).Value);
        json.Should().Contain("invalid_or_expired_ticket");

        // Redis should NOT have been called at all — invalid tickets are rejected before replay check
        await _redisDb.DidNotReceive().StringSetAsync(
            Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
            Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>());
    }

    #endregion
}
