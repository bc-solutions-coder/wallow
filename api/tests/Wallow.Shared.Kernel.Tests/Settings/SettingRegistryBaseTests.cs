using Wallow.Shared.Kernel.Settings;

namespace Wallow.Shared.Kernel.Tests.Settings;

public class SettingRegistryBaseTests
{
    private sealed class TestRegistry : SettingRegistryBase
    {
        public override string ModuleName => "test";

        public static readonly SettingDefinition<bool> FeatureEnabled = new(
            Key: "feature.enabled",
            DefaultValue: false,
            Description: "Whether feature is enabled");

        public static readonly SettingDefinition<int> MaxRetries = new(
            Key: "max.retries",
            DefaultValue: 3,
            Description: "Max retry count");

        public static readonly SettingDefinition<string> WelcomeMessage = new(
            Key: "welcome.message",
            DefaultValue: "Hello!",
            Description: "Welcome message shown to users");
    }

    private readonly TestRegistry _registry = new();

    [Fact]
    public void ModuleName_ReturnsExpectedName()
    {
        _registry.ModuleName.Should().Be("test");
    }

    [Fact]
    public void Defaults_ContainsAllDefinedSettings()
    {
        _registry.Defaults.Should().HaveCount(3);
        _registry.Defaults.Should().ContainKey("feature.enabled");
        _registry.Defaults.Should().ContainKey("max.retries");
        _registry.Defaults.Should().ContainKey("welcome.message");
    }

    [Fact]
    public void Defaults_HasCorrectDefaultValues()
    {
        _registry.Defaults["feature.enabled"].Should().Be(false);
        _registry.Defaults["max.retries"].Should().Be(3);
        _registry.Defaults["welcome.message"].Should().Be("Hello!");
    }

    [Fact]
    public void Metadata_ContainsAllDefinedSettings()
    {
        _registry.Metadata.Should().HaveCount(3);
        _registry.Metadata.Should().ContainKey("feature.enabled");
        _registry.Metadata.Should().ContainKey("max.retries");
    }

    [Fact]
    public void Metadata_HasCorrectDescriptions()
    {
        _registry.Metadata["feature.enabled"].Description.Should().Be("Whether feature is enabled");
        _registry.Metadata["max.retries"].Description.Should().Be("Max retry count");
    }

    [Fact]
    public void Metadata_HasCorrectValueTypes()
    {
        _registry.Metadata["feature.enabled"].ValueType.Should().Be<bool>();
        _registry.Metadata["max.retries"].ValueType.Should().Be<int>();
        _registry.Metadata["welcome.message"].ValueType.Should().Be<string>();
    }

    [Fact]
    public void Metadata_HasCorrectDefaultValues()
    {
        _registry.Metadata["feature.enabled"].DefaultValue.Should().Be(false);
        _registry.Metadata["max.retries"].DefaultValue.Should().Be(3);
    }

    [Fact]
    public void IsCodeDefinedKey_WithDefinedKey_ReturnsTrue()
    {
        _registry.IsCodeDefinedKey("feature.enabled").Should().BeTrue();
        _registry.IsCodeDefinedKey("max.retries").Should().BeTrue();
        _registry.IsCodeDefinedKey("welcome.message").Should().BeTrue();
    }

    [Fact]
    public void IsCodeDefinedKey_WithUndefinedKey_ReturnsFalse()
    {
        _registry.IsCodeDefinedKey("unknown.key").Should().BeFalse();
        _registry.IsCodeDefinedKey("custom.anything").Should().BeFalse();
    }

    [Fact]
    public void Defaults_IsCaseSensitive()
    {
        _registry.IsCodeDefinedKey("Feature.Enabled").Should().BeFalse();
        _registry.IsCodeDefinedKey("FEATURE.ENABLED").Should().BeFalse();
    }

    [Fact]
    public void Metadata_DisplayName_IsFieldName()
    {
        _registry.Metadata["feature.enabled"].DisplayName.Should().Be("FeatureEnabled");
        _registry.Metadata["max.retries"].DisplayName.Should().Be("MaxRetries");
    }

    [Fact]
    public void Defaults_IsLazyAndReturnsSameInstance()
    {
        IReadOnlyDictionary<string, object> first = _registry.Defaults;
        IReadOnlyDictionary<string, object> second = _registry.Defaults;

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void Metadata_IsLazyAndReturnsSameInstance()
    {
        IReadOnlyDictionary<string, SettingMetadata> first = _registry.Metadata;
        IReadOnlyDictionary<string, SettingMetadata> second = _registry.Metadata;

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void RegistryWithNoSettings_HasEmptyDefaultsAndMetadata()
    {
        EmptyRegistry registry = new();

        registry.Defaults.Should().BeEmpty();
        registry.Metadata.Should().BeEmpty();
    }

    private sealed class EmptyRegistry : SettingRegistryBase
    {
        public override string ModuleName => "empty";
    }
}
