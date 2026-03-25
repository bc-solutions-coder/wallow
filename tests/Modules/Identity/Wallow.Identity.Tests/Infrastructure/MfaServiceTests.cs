using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.Identity.Tests.Infrastructure;

public class MfaServiceTests
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly UserManager<WallowUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MfaService> _logger;
    private readonly MfaService _sut;

    public MfaServiceTests()
    {
        _dataProtectionProvider = DataProtectionProvider.Create("test");
        _cache = Substitute.For<IDistributedCache>();
        _connectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
        _connectionMultiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(Substitute.For<IDatabase>());
        _userManager = Substitute.For<UserManager<WallowUser>>(
            Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null);
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mfa:MaxFailedAttempts"] = "5"
            })
            .Build();
        _logger = NullLogger<MfaService>.Instance;
        _sut = new MfaService(_dataProtectionProvider, _cache, _connectionMultiplexer, _userManager, _configuration, _logger);
    }

    [Fact]
    public async Task GenerateEnrollmentSecretAsync_WithValidUserId_ReturnsProtectedSecretAndQrUri()
    {
        string userId = "user-123";

        (string Secret, string QrUri) result = await _sut.GenerateEnrollmentSecretAsync(userId, CancellationToken.None);

        result.Secret.Should().NotBeNullOrEmpty();
        result.QrUri.Should().StartWith("otpauth://totp/Wallow:user-123?secret=");
        result.QrUri.Should().Contain("issuer=Wallow");
        result.QrUri.Should().Contain("digits=6");
        result.QrUri.Should().Contain("period=30");
    }

    [Fact]
    public async Task GenerateEnrollmentSecretAsync_CalledTwice_ReturnsDifferentSecrets()
    {
        string userId = "user-123";

        (string Secret, string QrUri) first = await _sut.GenerateEnrollmentSecretAsync(userId, CancellationToken.None);
        (string Secret, string QrUri) second = await _sut.GenerateEnrollmentSecretAsync(userId, CancellationToken.None);

        first.Secret.Should().NotBe(second.Secret);
    }

    [Fact]
    public async Task ValidateTotpAsync_WithInvalidCode_ReturnsFalse()
    {
        string userId = "user-123";
        (string Secret, string QrUri) enrollment = await _sut.GenerateEnrollmentSecretAsync(userId, CancellationToken.None);

        bool result = await _sut.ValidateTotpAsync(enrollment.Secret, "000000", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateTotpAsync_WithWrongLengthCode_ReturnsFalse()
    {
        string userId = "user-123";
        (string Secret, string QrUri) enrollment = await _sut.GenerateEnrollmentSecretAsync(userId, CancellationToken.None);

        bool result = await _sut.ValidateTotpAsync(enrollment.Secret, "12345", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IssueChallengeAsync_WithValidUserId_ReturnsChallengeToken()
    {
        string userId = "user-456";

        string token = await _sut.IssueChallengeAsync(userId, CancellationToken.None);

        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task IssueChallengeAsync_StoresChallengeInCache()
    {
        string userId = "user-456";

        await _sut.IssueChallengeAsync(userId, CancellationToken.None);

        await _cache.Received(1).SetAsync(
            Arg.Is<string>(k => k == $"mfa:challenge:{userId}"),
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IssueChallengeAsync_ResetsFailureCount()
    {
        string userId = "user-456";

        await _sut.IssueChallengeAsync(userId, CancellationToken.None);

        await _cache.Received(1).RemoveAsync(
            Arg.Is<string>(k => k == $"mfa:failures:{userId}"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IssueChallengeAsync_CalledTwice_ReturnsDifferentTokens()
    {
        string userId = "user-456";

        string first = await _sut.IssueChallengeAsync(userId, CancellationToken.None);
        string second = await _sut.IssueChallengeAsync(userId, CancellationToken.None);

        first.Should().NotBe(second);
    }

    [Fact]
    public async Task ValidateChallengeAsync_WithExpiredChallenge_ReturnsFailure()
    {
        string userId = "user-789";
        _cache.GetAsync(Arg.Is<string>(k => k == $"mfa:failures:{userId}"), Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);
        _cache.GetAsync(Arg.Is<string>(k => k == $"mfa:challenge:{userId}"), Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);

        Wallow.Shared.Kernel.Results.Result result = await _sut.ValidateChallengeAsync(userId, "some-code", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Mfa.ExpiredChallenge");
    }

    [Fact]
    public async Task ValidateChallengeAsync_WithInvalidCode_ReturnsFailureAndIncrementsCount()
    {
        string userId = "user-789";
        byte[] storedToken = System.Text.Encoding.UTF8.GetBytes("valid-token");

        _cache.GetAsync(Arg.Is<string>(k => k == $"mfa:failures:{userId}"), Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);
        _cache.GetAsync(Arg.Is<string>(k => k == $"mfa:challenge:{userId}"), Arg.Any<CancellationToken>())
            .Returns(storedToken);

        Wallow.Shared.Kernel.Results.Result result = await _sut.ValidateChallengeAsync(userId, "wrong-code", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Mfa.InvalidCode");

        await _cache.Received(1).SetAsync(
            Arg.Is<string>(k => k == $"mfa:failures:{userId}"),
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateChallengeAsync_WithValidCode_ReturnsSuccess()
    {
        string userId = "user-789";
        string challengeCode = "correct-token";
        byte[] storedToken = System.Text.Encoding.UTF8.GetBytes(challengeCode);

        _cache.GetAsync(Arg.Is<string>(k => k == $"mfa:failures:{userId}"), Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);
        _cache.GetAsync(Arg.Is<string>(k => k == $"mfa:challenge:{userId}"), Arg.Any<CancellationToken>())
            .Returns(storedToken);

        Wallow.Shared.Kernel.Results.Result result = await _sut.ValidateChallengeAsync(userId, challengeCode, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateChallengeAsync_WithValidCode_CleansUpCache()
    {
        string userId = "user-789";
        string challengeCode = "correct-token";
        byte[] storedToken = System.Text.Encoding.UTF8.GetBytes(challengeCode);

        _cache.GetAsync(Arg.Is<string>(k => k == $"mfa:failures:{userId}"), Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);
        _cache.GetAsync(Arg.Is<string>(k => k == $"mfa:challenge:{userId}"), Arg.Any<CancellationToken>())
            .Returns(storedToken);

        await _sut.ValidateChallengeAsync(userId, challengeCode, CancellationToken.None);

        await _cache.Received().RemoveAsync(
            Arg.Is<string>(k => k == $"mfa:challenge:{userId}"),
            Arg.Any<CancellationToken>());
        await _cache.Received().RemoveAsync(
            Arg.Is<string>(k => k == $"mfa:failures:{userId}"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateChallengeAsync_WhenLockedOut_ReturnsFailure()
    {
        string userId = "user-789";
        byte[] failureCount = System.Text.Encoding.UTF8.GetBytes("5");

        _cache.GetAsync(Arg.Is<string>(k => k == $"mfa:failures:{userId}"), Arg.Any<CancellationToken>())
            .Returns(failureCount);

        Wallow.Shared.Kernel.Results.Result result = await _sut.ValidateChallengeAsync(userId, "any-code", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Mfa.Locked");
    }

    [Fact]
    public async Task ValidateChallengeAsync_WhenBelowLockoutThreshold_DoesNotLock()
    {
        string userId = "user-789";
        byte[] failureCount = System.Text.Encoding.UTF8.GetBytes("4");
        string challengeCode = "correct-token";
        byte[] storedToken = System.Text.Encoding.UTF8.GetBytes(challengeCode);

        _cache.GetAsync(Arg.Is<string>(k => k == $"mfa:failures:{userId}"), Arg.Any<CancellationToken>())
            .Returns(failureCount);
        _cache.GetAsync(Arg.Is<string>(k => k == $"mfa:challenge:{userId}"), Arg.Any<CancellationToken>())
            .Returns(storedToken);

        Wallow.Shared.Kernel.Results.Result result = await _sut.ValidateChallengeAsync(userId, challengeCode, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateBackupCodesAsync_Returns10Codes()
    {
        List<string> codes = await _sut.GenerateBackupCodesAsync(CancellationToken.None);

        codes.Should().HaveCount(10);
    }

    [Fact]
    public async Task GenerateBackupCodesAsync_CodesHaveCorrectFormat()
    {
        List<string> codes = await _sut.GenerateBackupCodesAsync(CancellationToken.None);

        foreach (string code in codes)
        {
            code.Should().MatchRegex(@"^[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}$");
        }
    }

    [Fact]
    public async Task GenerateBackupCodesAsync_CodesAreUnique()
    {
        List<string> codes = await _sut.GenerateBackupCodesAsync(CancellationToken.None);

        codes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GenerateBackupCodesAsync_CalledTwice_ReturnsDifferentCodes()
    {
        List<string> first = await _sut.GenerateBackupCodesAsync(CancellationToken.None);
        List<string> second = await _sut.GenerateBackupCodesAsync(CancellationToken.None);

        first.Should().NotBeEquivalentTo(second);
    }

    [Fact]
    public async Task ValidateChallengeAsync_WithCustomMaxFailedAttempts_RespectsConfiguration()
    {
        IConfiguration customConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mfa:MaxFailedAttempts"] = "2"
            })
            .Build();
        MfaService customSut = new(_dataProtectionProvider, _cache, _connectionMultiplexer, _userManager, customConfig, _logger);

        string userId = "user-custom";
        byte[] failureCount = System.Text.Encoding.UTF8.GetBytes("2");
        _cache.GetAsync(Arg.Is<string>(k => k == $"mfa:failures:{userId}"), Arg.Any<CancellationToken>())
            .Returns(failureCount);

        Wallow.Shared.Kernel.Results.Result result = await customSut.ValidateChallengeAsync(userId, "any-code", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Mfa.Locked");
    }
}
