using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Identity.Tests.Api.Controllers;

public class AccountControllerEmailChangeTests
{
    private readonly UserManager<WallowUser> _userManager;
    private readonly IMessageBus _messageBus;
    private readonly IDatabase _redisDb;
    private readonly AccountController _controller;
    private readonly TimeProvider _timeProvider;

    private const string TestEmail = "user@example.com";
    private const string NewEmail = "newemail@example.com";
    private readonly Guid _userId = Guid.NewGuid();

    public AccountControllerEmailChangeTests()
    {
        _userManager = Substitute.For<UserManager<WallowUser>>(
            Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null);

        SignInManager<WallowUser> signInManager = Substitute.For<SignInManager<WallowUser>>(
            _userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<WallowUser>>(),
            null, null, null, null);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthUrl"] = "https://auth.example.com"
            })
            .Build();

        _messageBus = Substitute.For<IMessageBus>();
        _timeProvider = Substitute.For<TimeProvider>();
        _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 3, 29, 12, 0, 0, TimeSpan.Zero));

        IConnectionMultiplexer redisMultiplexer = Substitute.For<IConnectionMultiplexer>();
        _redisDb = Substitute.For<IDatabase>();
        redisMultiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_redisDb);

        _controller = new AccountController(
            signInManager,
            configuration,
            Substitute.For<IRedirectUriValidator>(),
            new EphemeralDataProtectionProvider(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            _messageBus,
            Substitute.For<IClientTenantResolver>(),
            Substitute.For<IOrganizationService>(),
            Substitute.For<IPasswordlessService>(),
            Substitute.For<IMfaExemptionChecker>(),
            Substitute.For<IMfaService>(),
            Substitute.For<IMfaPartialAuthService>(),
            Substitute.For<IOrganizationMfaPolicyService>(),
            Substitute.For<IMfaLockoutService>(),
            redisMultiplexer,
            Substitute.For<ILogger<AccountController>>(),
            _timeProvider);

        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
            ], "test"))
        };
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private WallowUser CreateTestUser(string? email = null, Guid? id = null)
    {
        Guid userId = id ?? _userId;
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", email ?? TestEmail, TimeProvider.System);
        // Set Id via reflection since WallowUser inherits from IdentityUser<Guid>
        typeof(IdentityUser<Guid>).GetProperty(nameof(IdentityUser<Guid>.Id))!.SetValue(user, userId);
        return user;
    }

    #region ChangeEmail - Success

    [Fact]
    public async Task ChangeEmail_OnSuccess_ReturnsOkAndPublishesEvent()
    {
        WallowUser user = CreateTestUser();
        _userManager.FindByIdAsync(_userId.ToString()).Returns(user);
        _redisDb.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(1L);
        _userManager.GenerateChangeEmailTokenAsync(user, NewEmail).Returns("test-token");

        IActionResult result = await _controller.ChangeEmail(new ChangeEmailRequest(NewEmail));

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"succeeded\":true");

        await _messageBus.Received(1).PublishAsync(Arg.Is<UserEmailChangeRequestedEvent>(e =>
            e.UserId == _userId &&
            e.NewEmail == NewEmail));
    }

    #endregion

    #region ChangeEmail - Same email

    [Fact]
    public async Task ChangeEmail_WhenSameEmail_ReturnsBadRequest()
    {
        WallowUser user = CreateTestUser();
        _userManager.FindByIdAsync(_userId.ToString()).Returns(user);

        IActionResult result = await _controller.ChangeEmail(new ChangeEmailRequest(TestEmail));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region ChangeEmail - Rate limited

    [Fact]
    public async Task ChangeEmail_WhenRateLimited_Returns429()
    {
        WallowUser user = CreateTestUser();
        _userManager.FindByIdAsync(_userId.ToString()).Returns(user);
        // Simulate 4th attempt (exceeds max 3/hour)
        _redisDb.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(4L);

        IActionResult result = await _controller.ChangeEmail(new ChangeEmailRequest(NewEmail));

        ObjectResult statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(429);
    }

    #endregion

    #region ConfirmEmailChange - Success

    [Fact]
    public async Task ConfirmEmailChange_WithValidToken_ReturnsOkAndPublishesEvent()
    {
        WallowUser user = CreateTestUser();
        // Set PendingEmailExpiry to future
        typeof(WallowUser).GetProperty(nameof(WallowUser.PendingEmailExpiry))!
            .SetValue(user, new DateTimeOffset(2026, 3, 29, 13, 0, 0, TimeSpan.Zero));
        typeof(WallowUser).GetProperty(nameof(WallowUser.PendingEmail))!
            .SetValue(user, NewEmail);
        _userManager.FindByIdAsync(_userId.ToString()).Returns(user);
        _userManager.ChangeEmailAsync(user, NewEmail, "valid-token").Returns(IdentityResult.Success);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        IActionResult result = await _controller.ConfirmEmailChange("valid-token", _userId.ToString(), NewEmail);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"succeeded\":true");

        await _messageBus.Received(1).PublishAsync(Arg.Is<UserEmailChangedEvent>(e =>
            e.UserId == _userId &&
            e.OldEmail == TestEmail &&
            e.NewEmail == NewEmail));
    }

    #endregion

    #region ConfirmEmailChange - Expired token

    [Fact]
    public async Task ConfirmEmailChange_WhenExpired_ReturnsBadRequestTokenExpired()
    {
        WallowUser user = CreateTestUser();
        // Set PendingEmailExpiry to past
        typeof(WallowUser).GetProperty(nameof(WallowUser.PendingEmailExpiry))!
            .SetValue(user, new DateTimeOffset(2026, 3, 29, 11, 0, 0, TimeSpan.Zero));
        _userManager.FindByIdAsync(_userId.ToString()).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        IActionResult result = await _controller.ConfirmEmailChange("expired-token", _userId.ToString(), NewEmail);

        BadRequestObjectResult bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        json.Should().Contain("token_expired");
    }

    #endregion

    #region ConfirmEmailChange - Invalid token

    [Fact]
    public async Task ConfirmEmailChange_WithInvalidToken_ReturnsBadRequestInvalidToken()
    {
        WallowUser user = CreateTestUser();
        typeof(WallowUser).GetProperty(nameof(WallowUser.PendingEmailExpiry))!
            .SetValue(user, new DateTimeOffset(2026, 3, 29, 13, 0, 0, TimeSpan.Zero));
        _userManager.FindByIdAsync(_userId.ToString()).Returns(user);
        _userManager.ChangeEmailAsync(user, NewEmail, "bad-token")
            .Returns(IdentityResult.Failed(new IdentityError { Code = "InvalidToken", Description = "Invalid token" }));

        IActionResult result = await _controller.ConfirmEmailChange("bad-token", _userId.ToString(), NewEmail);

        BadRequestObjectResult bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        json.Should().Contain("invalid_token");
    }

    #endregion

    #region ConfirmEmailChange - User not found

    [Fact]
    public async Task ConfirmEmailChange_WhenUserNotFound_ReturnsBadRequest()
    {
        _userManager.FindByIdAsync(_userId.ToString()).Returns((WallowUser?)null);

        IActionResult result = await _controller.ConfirmEmailChange("token", _userId.ToString(), NewEmail);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion
}
