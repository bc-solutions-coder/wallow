using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;

using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.Identity.Tests.Infrastructure;

public class MfaServiceTests
{
    private readonly MfaService _sut;

    public MfaServiceTests()
    {
        IDataProtectionProvider dataProtectionProvider = DataProtectionProvider.Create("test");
        UserManager<WallowUser> userManager = Substitute.For<UserManager<WallowUser>>(
            Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null);
        _sut = new MfaService(dataProtectionProvider, userManager, NullLogger<MfaService>.Instance);
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
}
