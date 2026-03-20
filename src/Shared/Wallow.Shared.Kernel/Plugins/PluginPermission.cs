namespace Wallow.Shared.Kernel.Plugins;

/// <summary>
/// Known permission constants for plugin capability grants.
/// Permissions follow the pattern "module:action".
/// </summary>
public static class PluginPermission
{
    public const string BillingRead = "billing:read";
    public const string NotificationsSend = "notifications:send";
    public const string StorageRead = "storage:read";
    public const string StorageWrite = "storage:write";
    public const string IdentityRead = "identity:read";
}
