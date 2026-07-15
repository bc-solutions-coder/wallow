namespace Wallow.Identity.Infrastructure.Options;

public sealed class PasswordlessOptions
{
    public const string SectionName = "Passwordless";

    public int RateLimitMaxRequests { get; set; } = 3;

    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan MagicLinkTtl { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan OtpTtl { get; set; } = TimeSpan.FromMinutes(5);
}
