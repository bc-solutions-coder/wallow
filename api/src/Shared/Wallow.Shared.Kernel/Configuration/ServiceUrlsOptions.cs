namespace Wallow.Shared.Kernel.Configuration;

public sealed class ServiceUrlsOptions
{
    public const string SectionName = "ServiceUrls";

    public string ApiUrl { get; set; } = "http://localhost:5001";

    public string AuthUrl { get; set; } = "http://localhost:5002";

    public string WebUrl { get; set; } = "http://localhost:5003";
}
