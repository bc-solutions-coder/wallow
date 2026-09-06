namespace Wallow.Identity.Infrastructure.Options;

public sealed class EmailChangeOptions
{
    public const string SectionName = "Identity:EmailChange";

    public int RateLimitMaxRequests { get; set; } = 3;

    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromHours(1);
}
