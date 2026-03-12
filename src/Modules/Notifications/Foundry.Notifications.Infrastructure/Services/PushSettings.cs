namespace Foundry.Notifications.Infrastructure.Services;

public sealed class PushSettings
{
    public FcmDefaults Fcm { get; set; } = new();
    public ApnsDefaults Apns { get; set; } = new();
    public WebPushDefaults WebPush { get; set; } = new();
}

public sealed class FcmDefaults
{
    public string ServerKey { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
}

public sealed class ApnsDefaults
{
    public string TeamId { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string BundleId { get; set; } = string.Empty;
    public bool UseSandbox { get; set; } = true;
}

public sealed class WebPushDefaults
{
    public string Subject { get; set; } = string.Empty;
    public string VapidPublicKey { get; set; } = string.Empty;
    public string VapidPrivateKey { get; set; } = string.Empty;
}
