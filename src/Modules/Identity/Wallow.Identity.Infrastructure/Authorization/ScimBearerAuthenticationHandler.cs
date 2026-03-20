using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Wallow.Identity.Infrastructure.Authorization;

public sealed partial class ScimBearerAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IdentityDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    private const string BearerPrefix = "Bearer ";

    public ScimBearerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IdentityDbContext dbContext,
        TenantContext tenantContext,
        TimeProvider timeProvider) : base(options, logger, encoder)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out StringValues authHeader))
        {
            return AuthenticateResult.NoResult();
        }

        string authValue = authHeader.ToString();
        if (!authValue.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        string token = authValue[BearerPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.Fail("Empty Bearer token");
        }

        string tokenPrefix = token.Length >= 8 ? token[..8] : token;

        Domain.Entities.ScimConfiguration? config = await _dbContext.ScimConfigurations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.IsEnabled && c.TokenPrefix == tokenPrefix,
                Context.RequestAborted);

        if (config == null || !config.IsTokenValid(_timeProvider))
        {
            LogInvalidScimTokenAttempt(Context.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("Invalid or expired SCIM token");
        }

        string hashedToken = HashToken(token);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(config.BearerToken);
        byte[] actualBytes = Encoding.UTF8.GetBytes(hashedToken);

        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            LogInvalidScimTokenAttempt(Context.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("Invalid or expired SCIM token");
        }

        _tenantContext.SetTenant(config.TenantId);

        LogScimTokenAuthenticated(_tenantContext.TenantId.Value);

        List<Claim> claims =
        [
            new("scim_client", "true"),
            new("auth_method", "scim_bearer"),
            new("tenant_id", _tenantContext.TenantId.Value.ToString())
        ];

        ClaimsIdentity identity = new(claims, "ScimBearer");
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, "ScimBearer");

        return AuthenticateResult.Success(ticket);
    }

    private static string HashToken(string token)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(token);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid SCIM token attempt from {RemoteIp}")]
    private partial void LogInvalidScimTokenAttempt(System.Net.IPAddress? remoteIp);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SCIM token authenticated for tenant {TenantId}")]
    private partial void LogScimTokenAuthenticated(Guid tenantId);
}
