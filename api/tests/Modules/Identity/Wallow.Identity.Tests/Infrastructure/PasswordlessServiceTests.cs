using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Errors;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Errors;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class PasswordlessServiceTests
{
    private readonly IDatabase _redis;
    private readonly IMessageBus _messageBus;
    private readonly UserManager<WallowUser> _userManager;
    private readonly PasswordlessService _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public PasswordlessServiceTests()
    {
        IConnectionMultiplexer mux = Substitute.For<IConnectionMultiplexer>();
        _redis = Substitute.For<IDatabase>();
        mux.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_redis);
        _messageBus = Substitute.For<IMessageBus>();
        _userManager = Substitute.For<UserManager<WallowUser>>(
            Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null);
        TenantContext tc = new(); tc.SetTenant(new TenantId(_tenantId));
        IDataProtectionProvider dp = DataProtectionProvider.Create("test");
        PasswordlessOptions opts = new() { RateLimitMaxRequests = 3, RateLimitWindow = TimeSpan.FromMinutes(15), MagicLinkTtl = TimeSpan.FromMinutes(10), OtpTtl = TimeSpan.FromMinutes(5) };
        _sut = new PasswordlessService(mux, _messageBus, _userManager, dp, Options.Create(opts), NullLogger<PasswordlessService>.Instance);
    }

    [Fact]
    public async Task SendMagicLink_RateLimited_Fails()
    {
        _redis.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(4L);
        Result r = await _sut.SendMagicLinkAsync("r@t.com", CancellationToken.None);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(SharedErrors.RateLimitExceeded.Code);
        r.Error.RetryAfter.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task SendMagicLink_UserNotFound_ReturnsSuccess()
    {
        _redis.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(1L);
        _userManager.FindByEmailAsync("no@t.com").Returns((WallowUser?)null);
        Result r = await _sut.SendMagicLinkAsync("no@t.com", CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SendMagicLink_UserExists_Sends()
    {
        _redis.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(1L);
        WallowUser user = WallowUser.Create("A", "B", "f@t.com", TimeProvider.System);
        _userManager.FindByEmailAsync("f@t.com").Returns(user);
        Result r = await _sut.SendMagicLinkAsync("f@t.com", CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
        await _messageBus.Received(1).PublishAsync(Arg.Any<object>());
    }

    [Fact]
    public async Task ValidateMagicLink_BadFormat_Fails()
    {
        Result<string> r = await _sut.ValidateMagicLinkAsync("nodots", CancellationToken.None);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(IdentityErrors.AuthTokenInvalid.Code);
    }

    [Fact]
    public async Task ValidateMagicLink_BadSignature_Fails()
    {
        Result<string> r = await _sut.ValidateMagicLinkAsync("raw.badsig", CancellationToken.None);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(IdentityErrors.AuthTokenInvalid.Code);
    }

    [Fact]
    public async Task SendOtp_RateLimited_Fails()
    {
        _redis.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(4L);
        Result r = await _sut.SendOtpAsync("o@t.com", CancellationToken.None);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(SharedErrors.RateLimitExceeded.Code);
        r.Error.RetryAfter.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task SendOtp_UserNotFound_ReturnsSuccess()
    {
        _redis.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(1L);
        _userManager.FindByEmailAsync("no@t.com").Returns((WallowUser?)null);
        Result r = await _sut.SendOtpAsync("no@t.com", CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SendOtp_UserExists_Sends()
    {
        _redis.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(1L);
        WallowUser user = WallowUser.Create("A", "B", "o@t.com", TimeProvider.System);
        _userManager.FindByEmailAsync("o@t.com").Returns(user);
        Result r = await _sut.SendOtpAsync("o@t.com", CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
        await _messageBus.Received(1).PublishAsync(Arg.Any<object>());
    }

    [Fact]
    public async Task ValidateOtp_Expired_Fails()
    {
        _redis.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(RedisValue.Null);
        Result<string> r = await _sut.ValidateOtpAsync("u@t.com", "123456", CancellationToken.None);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(IdentityErrors.AuthOtpInvalid.Code);
    }

    [Fact]
    public async Task ValidateOtp_WrongCode_Fails()
    {
        _redis.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(new RedisValue("999999"));
        Result<string> r = await _sut.ValidateOtpAsync("u@t.com", "123456", CancellationToken.None);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(IdentityErrors.AuthOtpInvalid.Code);
    }

    [Fact]
    public async Task ValidateOtp_ValidCode_Succeeds()
    {
        _redis.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(new RedisValue("123456"));
        Result<string> r = await _sut.ValidateOtpAsync("u@t.com", "123456", CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
        await _redis.Received(1).KeyDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>());
    }

    // Regression for Wallow-gfph: magic-link send and verify run in DIFFERENT DI scopes
    // (send = POST request, verify = later GET when the user clicks the emailed link), so
    // they resolve DIFFERENT scoped PasswordlessService instances. The HMAC signing key must
    // be stable across instances or verification can never succeed. This reproduces the
    // production 401 by minting a token on one instance and validating it on another that
    // shares the same IDataProtectionProvider singleton, exactly as production DI does.
    [Fact]
    public async Task MagicLink_SendThenVerify_AcrossSeparateScopes_Succeeds()
    {
        IDataProtectionProvider sharedProvider = DataProtectionProvider.Create("Wallow.Identity.Passwordless");

        IDatabase redis = Substitute.For<IDatabase>();
        redis.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(1L);
        redis.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(new RedisValue("scoped@t.com"));

        IMessageBus sendBus = Substitute.For<IMessageBus>();
        MagicLinkRequestedEvent? published = null;
        await sendBus.PublishAsync(Arg.Do<MagicLinkRequestedEvent>(e => published = e));

        WallowUser user = WallowUser.Create("A", "B", "scoped@t.com", TimeProvider.System);

        PasswordlessService sendScope = CreateService(sharedProvider, redis, sendBus, user);
        Result sendResult = await sendScope.SendMagicLinkAsync("scoped@t.com", CancellationToken.None);
        sendResult.IsSuccess.Should().BeTrue();
        published.Should().NotBeNull();

        PasswordlessService verifyScope = CreateService(sharedProvider, redis, Substitute.For<IMessageBus>(), user);
        Result<string> verifyResult = await verifyScope.ValidateMagicLinkAsync(published!.Token, CancellationToken.None);

        verifyResult.IsSuccess.Should().BeTrue();
        verifyResult.Value.Should().Be("scoped@t.com");
    }

    private PasswordlessService CreateService(
        IDataProtectionProvider dataProtectionProvider,
        IDatabase redis,
        IMessageBus messageBus,
        WallowUser user)
    {
        IConnectionMultiplexer mux = Substitute.For<IConnectionMultiplexer>();
        mux.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(redis);
        UserManager<WallowUser> userManager = Substitute.For<UserManager<WallowUser>>(
            Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null);
        userManager.FindByEmailAsync(user.Email!).Returns(user);
        TenantContext tc = new();
        tc.SetTenant(new TenantId(_tenantId));
        PasswordlessOptions opts = new() { RateLimitMaxRequests = 3, RateLimitWindow = TimeSpan.FromMinutes(15), MagicLinkTtl = TimeSpan.FromMinutes(10), OtpTtl = TimeSpan.FromMinutes(5) };
        return new PasswordlessService(mux, messageBus, userManager, dataProtectionProvider, Options.Create(opts), NullLogger<PasswordlessService>.Instance);
    }
}
