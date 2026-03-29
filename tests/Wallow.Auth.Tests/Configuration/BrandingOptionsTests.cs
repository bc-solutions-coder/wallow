using System.Text.Json;
using Wallow.Auth.Configuration;

namespace Wallow.Auth.Tests.Configuration;

public sealed class BrandingOptionsTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Defaults_ShouldHaveWallowBranding()
    {
        BrandingOptions options = new();

        options.AppName.Should().Be("Wallow");
        options.AppIcon.Should().Be("piggy-icon.svg");
        options.Tagline.Should().Be("Wallow in it");
        options.Theme.DefaultMode.Should().Be("dark");
    }

    [Fact]
    public void Deserialize_ValidJson_ShouldMapAllProperties()
    {
        string json = """
        {
          "appName": "TestApp",
          "appIcon": "test-icon.svg",
          "tagline": "Test tagline",
          "theme": {
            "defaultMode": "light",
            "light": {
              "primary": "oklch(0.5 0.1 200)",
              "background": "oklch(0.95 0.01 60)"
            },
            "dark": {
              "primary": "oklch(0.6 0.1 200)"
            }
          }
        }
        """;

        BrandingOptions options = JsonSerializer.Deserialize<BrandingOptions>(json, _jsonOptions)!;

        options.AppName.Should().Be("TestApp");
        options.AppIcon.Should().Be("test-icon.svg");
        options.Tagline.Should().Be("Test tagline");
        options.Theme.DefaultMode.Should().Be("light");
        options.Theme.Light.Primary.Should().Be("oklch(0.5 0.1 200)");
        options.Theme.Light.Background.Should().Be("oklch(0.95 0.01 60)");
        options.Theme.Dark.Primary.Should().Be("oklch(0.6 0.1 200)");
    }

    [Fact]
    public void Deserialize_PartialJson_ShouldUseDefaults()
    {
        string json = """{ "appName": "PartialApp" }""";

        BrandingOptions options = JsonSerializer.Deserialize<BrandingOptions>(json, _jsonOptions)!;

        options.AppName.Should().Be("PartialApp");
        options.AppIcon.Should().Be("piggy-icon.svg");
        options.Theme.DefaultMode.Should().Be("dark");
    }

    [Fact]
    public void Deserialize_EmptyJson_ShouldUseAllDefaults()
    {
        string json = "{}";

        BrandingOptions options = JsonSerializer.Deserialize<BrandingOptions>(json, _jsonOptions)!;

        options.AppName.Should().Be("Wallow");
        options.AppIcon.Should().Be("piggy-icon.svg");
        options.Tagline.Should().Be("Wallow in it");
    }

    [Fact]
    public void ThemeColorSet_NullByDefault()
    {
        ThemeColorSet colors = new();

        colors.Primary.Should().BeNull();
        colors.Background.Should().BeNull();
        colors.Popover.Should().BeNull();
    }

    [Theory]
    [InlineData("light", "light")]
    [InlineData("dark", "dark")]
    [InlineData("invalid", "dark")]
    [InlineData("", "dark")]
    [InlineData("Light", "dark")]
    public void DefaultMode_Validation_ShouldNormalize(string input, string expected)
    {
        BrandingOptions options = new();
        options.Theme.DefaultMode = input;

        if (options.Theme.DefaultMode is not ("light" or "dark"))
        {
            options.Theme.DefaultMode = "dark";
        }

        options.Theme.DefaultMode.Should().Be(expected);
    }
}
