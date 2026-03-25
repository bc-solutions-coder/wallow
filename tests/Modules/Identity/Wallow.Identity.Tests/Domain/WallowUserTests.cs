using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Domain;
using Microsoft.Extensions.Time.Testing;

namespace Wallow.Identity.Tests.Domain;

public class WallowUserTests
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Create_WithValidData_ReturnsUserWithCorrectProperties()
    {
        Guid tenantId = Guid.NewGuid();
        string firstName = "John";
        string lastName = "Doe";
        string email = "john.doe@example.com";

        WallowUser user = WallowUser.Create(tenantId, firstName, lastName, email, _timeProvider);

        user.Id.Should().NotBe(Guid.Empty);
        user.TenantId.Should().Be(tenantId);
        user.FirstName.Should().Be(firstName);
        user.LastName.Should().Be(lastName);
        user.Email.Should().Be(email);
        user.UserName.Should().Be(email);
        user.IsActive.Should().BeTrue();
        user.CreatedAt.Should().Be(_timeProvider.GetUtcNow());
        user.DeactivatedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithBlankFirstName_ThrowsBusinessRuleException(string? firstName)
    {
        Func<WallowUser> act = () => WallowUser.Create(Guid.NewGuid(), firstName!, "Doe", "test@example.com", _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*first name*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithBlankLastName_ThrowsBusinessRuleException(string? lastName)
    {
        Func<WallowUser> act = () => WallowUser.Create(Guid.NewGuid(), "John", lastName!, "test@example.com", _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*last name*");
    }

    [Fact]
    public void Create_IsActiveDefaultsToTrue()
    {
        WallowUser user = WallowUser.Create(Guid.NewGuid(), "Jane", "Smith", "jane@example.com", _timeProvider);

        user.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithBlankEmail_ThrowsBusinessRuleException(string? email)
    {
        Func<WallowUser> act = () => WallowUser.Create(Guid.NewGuid(), "John", "Doe", email!, _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*Email*");
    }

    [Fact]
    public void Create_HasPasswordDefaultsToTrue()
    {
        WallowUser user = WallowUser.Create(Guid.NewGuid(), "Jane", "Smith", "jane@example.com", _timeProvider);

        user.HasPassword.Should().BeTrue();
    }

    [Fact]
    public void Create_MfaDefaultsToDisabled()
    {
        WallowUser user = WallowUser.Create(Guid.NewGuid(), "Jane", "Smith", "jane@example.com", _timeProvider);

        user.MfaEnabled.Should().BeFalse();
        user.MfaMethod.Should().BeNull();
        user.TotpSecretEncrypted.Should().BeNull();
        user.BackupCodesHash.Should().BeNull();
        user.MfaGraceDeadline.Should().BeNull();
    }

    [Fact]
    public void EnableMfa_WithTotp_EnablesMfaAndSetsSecret()
    {
        WallowUser user = CreateUser();

        user.EnableMfa("totp", "encrypted-secret-123");

        user.MfaEnabled.Should().BeTrue();
        user.MfaMethod.Should().Be("totp");
        user.TotpSecretEncrypted.Should().Be("encrypted-secret-123");
    }

    [Fact]
    public void EnableMfa_WithUnsupportedMethod_ThrowsArgumentException()
    {
        WallowUser user = CreateUser();

        Action act = () => user.EnableMfa("sms", "secret");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*totp*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EnableMfa_WithBlankSecret_ThrowsArgumentException(string? secret)
    {
        WallowUser user = CreateUser();

        Action act = () => user.EnableMfa("totp", secret!);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*secret*");
    }

    [Fact]
    public void DisableMfa_ClearsAllMfaFields()
    {
        WallowUser user = CreateUser();
        user.EnableMfa("totp", "secret");
        user.SetBackupCodes("backup-hash");

        user.DisableMfa();

        user.MfaEnabled.Should().BeFalse();
        user.MfaMethod.Should().BeNull();
        user.TotpSecretEncrypted.Should().BeNull();
        user.BackupCodesHash.Should().BeNull();
    }

    [Fact]
    public void SetBackupCodes_WithValidHash_SetsBackupCodesHash()
    {
        WallowUser user = CreateUser();

        user.SetBackupCodes("hashed-codes-123");

        user.BackupCodesHash.Should().Be("hashed-codes-123");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void SetBackupCodes_WithBlankHash_ThrowsArgumentException(string? hash)
    {
        WallowUser user = CreateUser();

        Action act = () => user.SetBackupCodes(hash!);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*hash*");
    }

    [Fact]
    public void SetPasswordless_SetsHasPasswordToFalse()
    {
        WallowUser user = CreateUser();

        user.SetPasswordless();

        user.HasPassword.Should().BeFalse();
    }

    [Fact]
    public void SetMfaGraceDeadline_WithFutureDate_SetsDeadline()
    {
        WallowUser user = CreateUser();
        DateTimeOffset futureDeadline = DateTimeOffset.UtcNow.AddDays(7);

        user.SetMfaGraceDeadline(futureDeadline);

        user.MfaGraceDeadline.Should().Be(futureDeadline);
    }

    [Fact]
    public void SetMfaGraceDeadline_WithPastDate_ThrowsBusinessRuleException()
    {
        WallowUser user = CreateUser();
        DateTimeOffset pastDeadline = DateTimeOffset.UtcNow.AddDays(-1);

        Action act = () => user.SetMfaGraceDeadline(pastDeadline);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*future*");
    }

    [Fact]
    public void UpdateName_WithValidNames_UpdatesBothNames()
    {
        WallowUser user = CreateUser();

        user.UpdateName("Jane", "Smith");

        user.FirstName.Should().Be("Jane");
        user.LastName.Should().Be("Smith");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UpdateName_WithBlankFirstName_ThrowsBusinessRuleException(string? firstName)
    {
        WallowUser user = CreateUser();

        Action act = () => user.UpdateName(firstName!, "Smith");

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*first name*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UpdateName_WithBlankLastName_ThrowsBusinessRuleException(string? lastName)
    {
        WallowUser user = CreateUser();

        Action act = () => user.UpdateName("Jane", lastName!);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*last name*");
    }

    [Fact]
    public void Create_CalledTwice_GeneratesUniqueIds()
    {
        WallowUser user1 = WallowUser.Create(Guid.NewGuid(), "John", "Doe", "john@example.com", _timeProvider);
        WallowUser user2 = WallowUser.Create(Guid.NewGuid(), "Jane", "Smith", "jane@example.com", _timeProvider);

        user1.Id.Should().NotBe(user2.Id);
    }

    [Fact]
    public void EnableMfa_CalledTwice_OverwritesPreviousSettings()
    {
        WallowUser user = CreateUser();
        user.EnableMfa("totp", "first-secret");

        user.EnableMfa("totp", "second-secret");

        user.MfaEnabled.Should().BeTrue();
        user.MfaMethod.Should().Be("totp");
        user.TotpSecretEncrypted.Should().Be("second-secret");
    }

    [Fact]
    public void DisableMfa_WhenAlreadyDisabled_RemainsDisabled()
    {
        WallowUser user = CreateUser();

        user.DisableMfa();

        user.MfaEnabled.Should().BeFalse();
        user.MfaMethod.Should().BeNull();
        user.TotpSecretEncrypted.Should().BeNull();
        user.BackupCodesHash.Should().BeNull();
    }

    [Fact]
    public void SetBackupCodes_CalledTwice_OverwritesPreviousHash()
    {
        WallowUser user = CreateUser();
        user.SetBackupCodes("first-hash");

        user.SetBackupCodes("second-hash");

        user.BackupCodesHash.Should().Be("second-hash");
    }

    [Fact]
    public void SetPasswordless_CalledMultipleTimes_RemainsPasswordless()
    {
        WallowUser user = CreateUser();

        user.SetPasswordless();
        user.SetPasswordless();

        user.HasPassword.Should().BeFalse();
    }

    [Fact]
    public void SetMfaGraceDeadline_WithExactlyNow_ThrowsBusinessRuleException()
    {
        WallowUser user = CreateUser();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Action act = () => user.SetMfaGraceDeadline(now);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*future*");
    }

    [Fact]
    public void SetMfaGraceDeadline_CalledTwice_OverwritesPreviousDeadline()
    {
        WallowUser user = CreateUser();
        DateTimeOffset firstDeadline = DateTimeOffset.UtcNow.AddDays(7);
        DateTimeOffset secondDeadline = DateTimeOffset.UtcNow.AddDays(14);
        user.SetMfaGraceDeadline(firstDeadline);

        user.SetMfaGraceDeadline(secondDeadline);

        user.MfaGraceDeadline.Should().Be(secondDeadline);
    }

    [Fact]
    public void UpdateName_DoesNotAffectOtherProperties()
    {
        WallowUser user = CreateUser();
        user.EnableMfa("totp", "secret");
        Guid originalId = user.Id;
        string originalEmail = user.Email!;

        user.UpdateName("NewFirst", "NewLast");

        user.Id.Should().Be(originalId);
        user.Email.Should().Be(originalEmail);
        user.MfaEnabled.Should().BeTrue();
    }

    private WallowUser CreateUser() =>
        WallowUser.Create(Guid.NewGuid(), "John", "Doe", "john@example.com", _timeProvider);
}
