using Wallow.Shared.Kernel.Settings;

namespace Wallow.Storage.Application.Settings;

public class StorageSettingKeys : SettingRegistryBase
{
    public override string ModuleName => "storage";

    public static readonly SettingDefinition<int> MaxUploadSizeMb = new(
        Key: "storage.max_upload_size_mb",
        DefaultValue: 50,
        Description: "Maximum file upload size in megabytes");

    public static readonly SettingDefinition<string> AllowedFileTypes = new(
        Key: "storage.allowed_file_types",
        DefaultValue: "jpg,png,pdf,doc,docx",
        Description: "Comma-separated list of allowed file extensions for uploads");

    public static readonly SettingDefinition<int> StorageQuotaMb = new(
        Key: "storage.storage_quota_mb",
        DefaultValue: 1024,
        Description: "Storage quota per tenant in megabytes");
}
