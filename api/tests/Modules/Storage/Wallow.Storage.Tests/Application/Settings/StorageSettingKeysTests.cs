using Wallow.Storage.Application.Settings;

namespace Wallow.Storage.Tests.Application.Settings;

public class StorageSettingKeysTests
{
    private readonly StorageSettingKeys _registry = new();

    [Fact]
    public void ModuleName_IsStorage()
    {
        _registry.ModuleName.Should().Be("storage");
    }

    [Fact]
    public void MaxUploadSizeMb_HasCorrectKey()
    {
        StorageSettingKeys.MaxUploadSizeMb.Key.Should().Be("storage.max_upload_size_mb");
    }

    [Fact]
    public void MaxUploadSizeMb_HasDefaultValueOf50()
    {
        StorageSettingKeys.MaxUploadSizeMb.DefaultValue.Should().Be(50);
    }

    [Fact]
    public void AllowedFileTypes_HasCorrectKey()
    {
        StorageSettingKeys.AllowedFileTypes.Key.Should().Be("storage.allowed_file_types");
    }

    [Fact]
    public void AllowedFileTypes_HasExpectedDefaultValue()
    {
        StorageSettingKeys.AllowedFileTypes.DefaultValue.Should().Be("jpg,png,pdf,doc,docx");
    }

    [Fact]
    public void StorageQuotaMb_HasCorrectKey()
    {
        StorageSettingKeys.StorageQuotaMb.Key.Should().Be("storage.storage_quota_mb");
    }

    [Fact]
    public void StorageQuotaMb_HasDefaultValueOf1024()
    {
        StorageSettingKeys.StorageQuotaMb.DefaultValue.Should().Be(1024);
    }

    [Fact]
    public void Defaults_ContainsAllThreeKeys()
    {
        _registry.Defaults.Should().ContainKey("storage.max_upload_size_mb");
        _registry.Defaults.Should().ContainKey("storage.allowed_file_types");
        _registry.Defaults.Should().ContainKey("storage.storage_quota_mb");
    }

    [Fact]
    public void Metadata_ContainsAllThreeKeys()
    {
        _registry.Metadata.Should().ContainKey("storage.max_upload_size_mb");
        _registry.Metadata.Should().ContainKey("storage.allowed_file_types");
        _registry.Metadata.Should().ContainKey("storage.storage_quota_mb");
    }

    [Fact]
    public void IsCodeDefinedKey_WhenKnownKey_ReturnsTrue()
    {
        bool result = _registry.IsCodeDefinedKey("storage.max_upload_size_mb");

        result.Should().BeTrue();
    }

    [Fact]
    public void IsCodeDefinedKey_WhenUnknownKey_ReturnsFalse()
    {
        bool result = _registry.IsCodeDefinedKey("storage.unknown_key");

        result.Should().BeFalse();
    }
}
