namespace Wallow.Auth.Configuration;

public sealed class BrandingOptions
{
    public string AppName { get; set; } = "Wallow";
    public string AppIcon { get; set; } = "piggy-icon.svg";
    public string Tagline { get; set; } = "Wallow in it";
    public string RepositoryUrl { get; set; } = "";
    public LandingPageOptions LandingPage { get; set; } = new();
    public ThemeOptions Theme { get; set; } = new();
}

public sealed class LandingPageOptions
{
    public bool Enabled { get; set; } = true;
}

public sealed class ThemeOptions
{
    public string DefaultMode { get; set; } = "dark";
    public ThemeColorSet Light { get; set; } = new();
    public ThemeColorSet Dark { get; set; } = new();
}

public sealed class ThemeColorSet
{
    public string? Background { get; set; }
    public string? Foreground { get; set; }
    public string? Card { get; set; }
    public string? CardForeground { get; set; }
    public string? Popover { get; set; }
    public string? PopoverForeground { get; set; }
    public string? Primary { get; set; }
    public string? PrimaryForeground { get; set; }
    public string? Secondary { get; set; }
    public string? SecondaryForeground { get; set; }
    public string? Muted { get; set; }
    public string? MutedForeground { get; set; }
    public string? Accent { get; set; }
    public string? AccentForeground { get; set; }
    public string? Destructive { get; set; }
    public string? DestructiveForeground { get; set; }
    public string? Border { get; set; }
    public string? Input { get; set; }
    public string? Ring { get; set; }
    public string? Radius { get; set; }
}
