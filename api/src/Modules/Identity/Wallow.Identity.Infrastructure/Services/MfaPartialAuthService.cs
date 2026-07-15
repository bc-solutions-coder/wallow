using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class MfaPartialAuthService : IMfaPartialAuthService
{
    private const string CookieName = "Identity.MfaPartial";
    private static readonly TimeSpan _cookieLifetime = TimeSpan.FromMinutes(5);

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITimeLimitedDataProtector _protector;
    private readonly SignInManager<WallowUser> _signInManager;
    private readonly ILogger<MfaPartialAuthService> _logger;

    public MfaPartialAuthService(
        IHttpContextAccessor httpContextAccessor,
        IDataProtectionProvider dataProtectionProvider,
        SignInManager<WallowUser> signInManager,
        ILogger<MfaPartialAuthService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _protector = dataProtectionProvider
            .CreateProtector("Wallow.Identity.MfaPartial")
            .ToTimeLimitedDataProtector();
        _signInManager = signInManager;
        _logger = logger;
    }

    public Task IssuePartialCookieAsync(MfaPartialAuthPayload payload, CancellationToken ct)
    {
        string json = JsonSerializer.Serialize(payload);
        string protectedValue = _protector.Protect(json, _cookieLifetime);

        HttpContext httpContext = _httpContextAccessor.HttpContext!;
        httpContext.Response.Cookies.Append(CookieName, protectedValue, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = _cookieLifetime,
            IsEssential = true
        });

        LogPartialCookieIssued(payload.UserId, payload.Email);
        return Task.CompletedTask;
    }

    public Task<MfaPartialAuthPayload?> ValidatePartialCookieAsync(CancellationToken ct)
    {
        HttpContext httpContext = _httpContextAccessor.HttpContext!;
        string? cookieValue = httpContext.Request.Cookies[CookieName];

        if (string.IsNullOrEmpty(cookieValue))
        {
            return Task.FromResult<MfaPartialAuthPayload?>(null);
        }

        try
        {
            string json = _protector.Unprotect(cookieValue);
            MfaPartialAuthPayload? payload = JsonSerializer.Deserialize<MfaPartialAuthPayload>(json);
            return Task.FromResult(payload);
        }
        catch (CryptographicException)
        {
            LogPartialCookieValidationFailed("cryptographic error");
            return Task.FromResult<MfaPartialAuthPayload?>(null);
        }
        catch (FormatException)
        {
            LogPartialCookieValidationFailed("format error");
            return Task.FromResult<MfaPartialAuthPayload?>(null);
        }
    }

    public async Task UpgradeToFullAuthAsync(string userId, bool isPersistent, CancellationToken ct)
    {
        WallowUser? user = await _signInManager.UserManager.FindByIdAsync(userId);
        if (user is null)
        {
            LogPartialCookieValidationFailed($"user {userId} not found");
            return;
        }

        await _signInManager.SignInAsync(user, isPersistent);
        DeletePartialCookie();

        LogUpgradedToFullAuth(userId);
    }

    public void DeletePartialCookie()
    {
        HttpContext httpContext = _httpContextAccessor.HttpContext!;
        httpContext.Response.Cookies.Delete(CookieName);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "MFA partial cookie issued for user {UserId} ({Email})")]
    private partial void LogPartialCookieIssued(string userId, string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MFA partial cookie validation failed: {Reason}")]
    private partial void LogPartialCookieValidationFailed(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Upgraded to full auth for user {UserId}")]
    private partial void LogUpgradedToFullAuth(string userId);
}
