using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.Identity.Tests.Infrastructure;

public class MfaServiceGapTests
{
    private readonly UserManager<WallowUser> _userManager;
    private readonly MfaService _sut;

    public MfaServiceGapTests()
    {
        IDataProtectionProvider dp = DataProtectionProvider.Create("test");
        _userManager = Substitute.For<UserManager<WallowUser>>(
            Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null);
        _sut = new MfaService(dp, _userManager, NullLogger<MfaService>.Instance);
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
