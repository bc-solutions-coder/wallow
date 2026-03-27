using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.Identity.Tests.Infrastructure;

public class MfaPartialAuthServiceTests
{
    private const string CookieName = "Identity.MfaPartial";

    private readonly DefaultHttpContext _httpContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly SignInManager<WallowUser> _signInManager;
    private readonly MfaPartialAuthService _sut;

    private static readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero));

    public MfaPartialAuthServiceTests()
    {
        _httpContext = new DefaultHttpContext();
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _httpContextAccessor.HttpContext.Returns(_httpContext);

        _dataProtectionProvider = DataProtectionProvider.Create("test");

        UserManager<WallowUser> userManager = Substitute.For<UserManager<WallowUser>>(
            Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null);
        _signInManager = Substitute.For<SignInManager<WallowUser>>(
            userManager,
            _httpContextAccessor,
            Substitute.For<IUserClaimsPrincipalFactory<WallowUser>>(),
            null, null, null, null);

        _sut = new MfaPartialAuthService(
            _httpContextAccessor,
            _dataProtectionProvider,
            _signInManager,
            NullLogger<MfaPartialAuthService>.Instance);
    }

    [Fact]
    public async Task IssuePartialCookieAsync_AppendsCookieNamedIdentityMfaPartial()
    {
        MfaPartialAuthPayload payload = new(
            UserId: "user-123",
            Email: "test@example.com",
            AuthMethod: "password",
            RememberMe: false,
            IssuedAt: DateTimeOffset.UtcNow);

        await _sut.IssuePartialCookieAsync(payload, CancellationToken.None);

        _httpContext.Response.Headers.SetCookie.ToString().Should().Contain(CookieName);
    }

    [Fact]
    public async Task ValidatePartialCookieAsync_WhenCookieAbsent_ReturnsNull()
    {
        MfaPartialAuthPayload? result = await _sut.ValidatePartialCookieAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidatePartialCookieAsync_WhenCookieExpired_ReturnsNull()
    {
        ITimeLimitedDataProtector expiredProtector = _dataProtectionProvider
            .CreateProtector("Wallow.Identity.MfaPartial")
            .ToTimeLimitedDataProtector();

        MfaPartialAuthPayload payload = new(
            UserId: "user-456",
            Email: "expired@example.com",
            AuthMethod: "password",
            RememberMe: false,
            IssuedAt: DateTimeOffset.UtcNow.AddMinutes(-30));

        string protectedValue = expiredProtector.Protect(
            JsonSerializer.Serialize(payload),
            TimeSpan.FromMilliseconds(1));

        // Wait briefly so the protection expires
        await Task.Delay(50);

        _httpContext.Request.Headers.Cookie = $"{CookieName}={Uri.EscapeDataString(protectedValue)}";

        MfaPartialAuthPayload? result = await _sut.ValidatePartialCookieAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidatePartialCookieAsync_WithValidCookie_ReturnsPopulatedPayload()
    {
        MfaPartialAuthPayload payload = new(
            UserId: "user-789",
            Email: "valid@example.com",
            AuthMethod: "password",
            RememberMe: true,
            IssuedAt: DateTimeOffset.UtcNow);

        await _sut.IssuePartialCookieAsync(payload, CancellationToken.None);

        string? setCookieHeader = _httpContext.Response.Headers.SetCookie.ToString();
        string cookieValue = ExtractCookieValue(setCookieHeader, CookieName);
        _httpContext.Request.Headers.Cookie = $"{CookieName}={cookieValue}";

        MfaPartialAuthPayload? result = await _sut.ValidatePartialCookieAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result!.UserId.Should().Be("user-789");
        result.Email.Should().Be("valid@example.com");
        result.AuthMethod.Should().Be("password");
        result.RememberMe.Should().BeTrue();
    }

    [Fact]
    public void DeletePartialCookie_DeletesTheCookie()
    {
        _sut.DeletePartialCookie();

        string? setCookieHeader = _httpContext.Response.Headers.SetCookie.ToString();
        setCookieHeader.Should().Contain(CookieName);
        // A deleted cookie should have an expiration in the past
        setCookieHeader.Should().Contain("expires=");
    }

    [Fact]
    public async Task UpgradeToFullAuthAsync_CallsSignInManagerSignInAsyncExactlyOnce()
    {
        WallowUser user = WallowUser.Create(Guid.NewGuid(), "Test", "User", "test@example.com", _timeProvider);

        _signInManager.UserManager.FindByIdAsync("user-upgrade").Returns(user);

        await _sut.UpgradeToFullAuthAsync("user-upgrade", isPersistent: true, CancellationToken.None);

        await _signInManager.Received(1).SignInAsync(
            Arg.Is<WallowUser>(u => u.Id == user.Id),
            true,
            Arg.Any<string?>());
    }

    [Fact]
    public async Task UpgradeToFullAuthAsync_WithNonPersistent_PassesCorrectIsPersistent()
    {
        WallowUser user = WallowUser.Create(Guid.NewGuid(), "Test", "User2", "test2@example.com", _timeProvider);

        _signInManager.UserManager.FindByIdAsync("user-upgrade-2").Returns(user);

        await _sut.UpgradeToFullAuthAsync("user-upgrade-2", isPersistent: false, CancellationToken.None);

        await _signInManager.Received(1).SignInAsync(
            Arg.Is<WallowUser>(u => u.Id == user.Id),
            false,
            Arg.Any<string?>());
    }

    [Fact]
    public async Task UpgradeToFullAuthAsync_DeletesPartialCookieAfterSignIn()
    {
        WallowUser user = WallowUser.Create(Guid.NewGuid(), "Test", "User3", "test3@example.com", _timeProvider);

        _signInManager.UserManager.FindByIdAsync("user-upgrade-3").Returns(user);

        await _sut.UpgradeToFullAuthAsync("user-upgrade-3", isPersistent: false, CancellationToken.None);

        string? setCookieHeader = _httpContext.Response.Headers.SetCookie.ToString();
        setCookieHeader.Should().Contain(CookieName);
        setCookieHeader.Should().Contain("expires=");
    }

    private static string ExtractCookieValue(string? setCookieHeader, string cookieName)
    {
        if (string.IsNullOrEmpty(setCookieHeader))
        {
            return string.Empty;
        }

        string prefix = $"{cookieName}=";
        int startIndex = setCookieHeader.IndexOf(prefix, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            return string.Empty;
        }

        startIndex += prefix.Length;
        int endIndex = setCookieHeader.IndexOf(';', startIndex);
        if (endIndex < 0)
        {
            endIndex = setCookieHeader.Length;
        }

        return setCookieHeader[startIndex..endIndex];
    }
}
