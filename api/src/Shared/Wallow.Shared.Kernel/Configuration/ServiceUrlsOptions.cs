namespace Wallow.Shared.Kernel.Configuration;

public sealed class ServiceUrlsOptions
{
    public const string SectionName = "ServiceUrls";

    public string ApiUrl { get; set; } = "http://localhost:5001";

    public string AuthUrl { get; set; } = "http://localhost:3002";

    public string WebUrl { get; set; } = "http://localhost:5003";
}
