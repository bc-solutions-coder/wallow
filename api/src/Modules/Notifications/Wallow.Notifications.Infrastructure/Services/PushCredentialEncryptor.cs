using Microsoft.AspNetCore.DataProtection;
using Wallow.Notifications.Application.Channels.Push.Interfaces;

namespace Wallow.Notifications.Infrastructure.Services;

public sealed class PushCredentialEncryptor(IDataProtectionProvider dataProtectionProvider) : IPushCredentialEncryptor
{
    private const string Purpose = "TenantPushCredentials";

    private IDataProtector Protector => dataProtectionProvider.CreateProtector(Purpose);

    public string Encrypt(string plaintext)
    {
        return Protector.Protect(plaintext);
    }

    public string Decrypt(string ciphertext)
    {
        return Protector.Unprotect(ciphertext);
    }
}
