using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.Identity.Tests.Infrastructure;

public class MfaServiceGapTests
{
    private readonly IDatabase _redisDb;
    private readonly UserManager<WallowUser> _userManager;
    private readonly MfaService _sut;

    public MfaServiceGapTests()
    {
        IDataProtectionProvider dp = DataProtectionProvider.Create("test");
        IDistributedCache cache = Substitute.For<IDistributedCache>();
        IConnectionMultiplexer mux = Substitute.For<IConnectionMultiplexer>();
        _redisDb = Substitute.For<IDatabase>();
        mux.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_redisDb);
        _userManager = Substitute.For<UserManager<WallowUser>>(
            Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null);
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Mfa:MaxFailedAttempts"] = "5" }).Build();
        _sut = new MfaService(dp, cache, mux, _userManager, cfg, NullLogger<MfaService>.Instance);
    }

    [Fact]
    public async Task IssueMfaChallengeToken_ReturnsToken()
    {
        string token = await _sut.IssueMfaChallengeTokenAsync("u1", CancellationToken.None);
        token.Should().NotBeNullOrEmpty();
        await _redisDb.Received(1).StringSetAsync(
            Arg.Is<RedisKey>(k => k.ToString().StartsWith("mfa:challenge:u1:")),
            Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task IssueMfaChallengeToken_UniquePerId()
    {
        string t1 = await _sut.IssueMfaChallengeTokenAsync("u1", CancellationToken.None);
        string t2 = await _sut.IssueMfaChallengeTokenAsync("u1", CancellationToken.None);
        t1.Should().NotBe(t2);
    }

    [Fact]
    public async Task ValidateChallenge3_NoChallengeInRedis_False()
    {
        _redisDb.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(false);
        bool r = await _sut.ValidateChallengeAsync("u1", "ct", "123456", CancellationToken.None);
        r.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateChallenge3_UserNotFound_False()
    {
        _redisDb.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(true);
        _userManager.FindByIdAsync("u1").Returns((WallowUser?)null);
        bool r = await _sut.ValidateChallengeAsync("u1", "ct", "123456", CancellationToken.None);
        r.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateChallenge3_NoTotpSecret_False()
    {
        _redisDb.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(true);
        WallowUser user = WallowUser.Create(Guid.NewGuid(), "T", "U", "u@t.com", TimeProvider.System);
        _userManager.FindByIdAsync("u1").Returns(user);
        bool r = await _sut.ValidateChallengeAsync("u1", "ct", "123456", CancellationToken.None);
        r.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateBackupCode_UserNotFound_False()
    {
        _userManager.FindByIdAsync("no").Returns((WallowUser?)null);
        bool r = await _sut.ValidateBackupCodeAsync("no", "code", CancellationToken.None);
        r.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateBackupCode_NoBackupCodes_False()
    {
        WallowUser user = WallowUser.Create(Guid.NewGuid(), "T", "U", "u@t.com", TimeProvider.System);
        _userManager.FindByIdAsync("u1").Returns(user);
        bool r = await _sut.ValidateBackupCodeAsync("u1", "code", CancellationToken.None);
        r.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateBackupCode_WrongCode_False()
    {
        WallowUser user = WallowUser.Create(Guid.NewGuid(), "T", "U", "u@t.com", TimeProvider.System);
        user.SetBackupCodes(JsonSerializer.Serialize(new List<string> { "aaa", "bbb" }));
        _userManager.FindByIdAsync("u1").Returns(user);
        bool r = await _sut.ValidateBackupCodeAsync("u1", "wrong", CancellationToken.None);
        r.Should().BeFalse();
    }
}
