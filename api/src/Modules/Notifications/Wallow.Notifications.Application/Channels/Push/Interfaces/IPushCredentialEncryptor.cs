namespace Wallow.Notifications.Application.Channels.Push.Interfaces;

public interface IPushCredentialEncryptor
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
