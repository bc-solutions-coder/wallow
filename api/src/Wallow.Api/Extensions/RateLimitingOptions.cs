namespace Wallow.Api.Extensions;

/// <summary>
/// Fixed-window rate-limit settings, bound from the <c>RateLimiting</c> configuration
/// section. Defaults come from <see cref="RateLimitDefaults"/>; the Testing environment
/// overrides them with generous values in <c>appsettings.Testing.json</c> so functional
/// suites exercise the limiter without tripping it.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public AuthRateLimit Auth { get; set; } = new();

    public UploadRateLimit Upload { get; set; } = new();

    public RegistrationRateLimit Registration { get; set; } = new();

    public GlobalRateLimit Global { get; set; } = new();
}

public sealed class AuthRateLimit
{
    public int PermitLimit { get; set; } = RateLimitDefaults.AuthPermitLimit;

    public int WindowMinutes { get; set; } = RateLimitDefaults.AuthWindowMinutes;
}

public sealed class UploadRateLimit
{
    public int PermitLimit { get; set; } = RateLimitDefaults.UploadPermitLimit;

    public int WindowHours { get; set; } = RateLimitDefaults.UploadWindowHours;
}

public sealed class RegistrationRateLimit
{
    public int PermitLimit { get; set; } = RateLimitDefaults.RegistrationPermitLimit;

    public int WindowHours { get; set; } = RateLimitDefaults.RegistrationWindowHours;
}

public sealed class GlobalRateLimit
{
    public int PermitLimit { get; set; } = RateLimitDefaults.GlobalPermitLimit;

    public int WindowHours { get; set; } = RateLimitDefaults.GlobalWindowHours;
}
