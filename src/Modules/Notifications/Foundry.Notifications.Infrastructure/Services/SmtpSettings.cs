namespace Foundry.Notifications.Infrastructure.Services;

public sealed class SmtpSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public bool UseSsl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string DefaultFromAddress { get; set; } = "noreply@foundry.local";
    public string DefaultFromName { get; set; } = "Foundry";
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;
}
