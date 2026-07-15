using Wallow.Shared.Kernel.Settings;

namespace Wallow.Shared.Kernel.Tests.Settings;

public class SettingKeyValidatorTests
{
    private readonly ISettingRegistry _registry;

    public SettingKeyValidatorTests()
    {
        _registry = Substitute.For<ISettingRegistry>();
        _registry.IsCodeDefinedKey("feature.enabled").Returns(true);
        _registry.IsCodeDefinedKey("unknown.key").Returns(false);
    }

    [Theory]
    [InlineData("custom.my-key")]
    [InlineData("custom.")]
    [InlineData("custom.anything")]
    public void IsCustomKey_WithCustomPrefix_ReturnsTrue(string key)
    {
        bool result = SettingKeyValidator.IsCustomKey(key);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("feature.enabled")]
    [InlineData("system.config")]
    [InlineData("")]
    public void IsCustomKey_WithoutCustomPrefix_ReturnsFalse(string key)
    {
        bool result = SettingKeyValidator.IsCustomKey(key);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("system.config")]
    [InlineData("system.")]
    [InlineData("system.any")]
    public void IsSystemKey_WithSystemPrefix_ReturnsTrue(string key)
    {
        bool result = SettingKeyValidator.IsSystemKey(key);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("feature.enabled")]
    [InlineData("custom.key")]
    [InlineData("")]
    public void IsSystemKey_WithoutSystemPrefix_ReturnsFalse(string key)
    {
        bool result = SettingKeyValidator.IsSystemKey(key);

        result.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithEmptyKey_ReturnsInvalidKey()
    {
        SettingKeyValidationResult result = SettingKeyValidator.Validate(string.Empty, _registry, false, 0);

        result.Should().Be(SettingKeyValidationResult.InvalidKey);
    }

    [Fact]
    public void Validate_WithWhitespaceKey_ReturnsInvalidKey()
    {
        SettingKeyValidationResult result = SettingKeyValidator.Validate("   ", _registry, false, 0);

        result.Should().Be(SettingKeyValidationResult.InvalidKey);
    }

    [Fact]
    public void Validate_WithSystemKey_WhenNotPlatformAdmin_ReturnsSystemKeyUnauthorized()
    {
        SettingKeyValidationResult result = SettingKeyValidator.Validate("system.config", _registry, false, 0);

        result.Should().Be(SettingKeyValidationResult.SystemKeyUnauthorized);
    }

    [Fact]
    public void Validate_WithSystemKey_WhenPlatformAdmin_ReturnsValid()
    {
        SettingKeyValidationResult result = SettingKeyValidator.Validate("system.config", _registry, true, 0);

        result.Should().Be(SettingKeyValidationResult.Valid);
    }

    [Fact]
    public void Validate_WithCustomKey_WhenUnderLimit_ReturnsValid()
    {
        SettingKeyValidationResult result = SettingKeyValidator.Validate("custom.key", _registry, false, 50);

        result.Should().Be(SettingKeyValidationResult.Valid);
    }

    [Fact]
    public void Validate_WithCustomKey_WhenAtLimit_ReturnsCustomKeyLimitExceeded()
    {
        SettingKeyValidationResult result = SettingKeyValidator.Validate(
            "custom.key", _registry, false, SettingKeyValidator.MaxCustomKeysPerTenant);

        result.Should().Be(SettingKeyValidationResult.CustomKeyLimitExceeded);
    }

    [Fact]
    public void Validate_WithCodeDefinedKey_ReturnsValid()
    {
        SettingKeyValidationResult result = SettingKeyValidator.Validate("feature.enabled", _registry, false, 0);

        result.Should().Be(SettingKeyValidationResult.Valid);
    }

    [Fact]
    public void Validate_WithUnknownKey_ReturnsInvalidKey()
    {
        SettingKeyValidationResult result = SettingKeyValidator.Validate("unknown.key", _registry, false, 0);

        result.Should().Be(SettingKeyValidationResult.InvalidKey);
    }

    [Fact]
    public void Validate_ClassificationOverload_WithCustomKey_ReturnsCustom()
    {
        SettingKeyValidationResult result = SettingKeyValidator.Validate("custom.some-key", _registry);

        result.Should().Be(SettingKeyValidationResult.Custom);
    }

    [Fact]
    public void Validate_ClassificationOverload_WithSystemKey_ReturnsSystem()
    {
        SettingKeyValidationResult result = SettingKeyValidator.Validate("system.any", _registry);

        result.Should().Be(SettingKeyValidationResult.System);
    }

    [Fact]
    public void Validate_ClassificationOverload_WithCodeDefinedKey_ReturnsCodeDefined()
    {
        SettingKeyValidationResult result = SettingKeyValidator.Validate("feature.enabled", _registry);

        result.Should().Be(SettingKeyValidationResult.CodeDefined);
    }

    [Fact]
    public void Validate_ClassificationOverload_WithUnknownKey_ReturnsUnknown()
    {
        SettingKeyValidationResult result = SettingKeyValidator.Validate("unknown.key", _registry);

        result.Should().Be(SettingKeyValidationResult.Unknown);
    }

    [Fact]
    public void MaxCustomKeysPerTenant_Is100()
    {
        SettingKeyValidator.MaxCustomKeysPerTenant.Should().Be(100);
    }

    [Fact]
    public void CustomPrefix_IsCorrect()
    {
        SettingKeyValidator.CustomPrefix.Should().Be("custom.");
    }

    [Fact]
    public void SystemPrefix_IsCorrect()
    {
        SettingKeyValidator.SystemPrefix.Should().Be("system.");
    }
}
