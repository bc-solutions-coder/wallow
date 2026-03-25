namespace Wallow.Web.Configuration;

public sealed class BrandingOptions
{
    public string AppName { get; set; } = "Wallow";
    public string AppIcon { get; set; } = "piggy-icon.svg";
    public string Tagline { get; set; } = "Wallow in it";
    public string RepositoryUrl { get; set; } = "";
    public LandingPageOptions LandingPage { get; set; } = new();
}

public sealed class LandingPageOptions
{
    public bool Enabled { get; set; } = true;
}
