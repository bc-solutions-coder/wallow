using System.Text;
using Wallow.Notifications.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;

namespace Wallow.Notifications.Tests.Infrastructure.Services;

public class PushCredentialEncryptorTests
{
    private readonly IDataProtectionProvider _dataProtectionProvider = Substitute.For<IDataProtectionProvider>();
    private readonly IDataProtector _dataProtector = Substitute.For<IDataProtector>();
    private readonly PushCredentialEncryptor _encryptor;

    public PushCredentialEncryptorTests()
    {
        _dataProtectionProvider
            .CreateProtector(Arg.Any<string>())
            .Returns(_dataProtector);

        _encryptor = new PushCredentialEncryptor(_dataProtectionProvider);
    }

    [Fact]
    public void Encrypt_WithPlaintext_ReturnsNonEmptyCiphertext()
    {
        byte[] fakeEncrypted = [1, 2, 3];
        _dataProtector.Protect(Arg.Any<byte[]>()).Returns(fakeEncrypted);

        string result = _encryptor.Encrypt("my-secret-credential");

        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Decrypt_WithCiphertext_ReturnsOriginalPlaintext()
    {
        string original = "my-secret-credential";
        byte[] originalBytes = Encoding.UTF8.GetBytes(original);
        _dataProtector.Unprotect(Arg.Any<byte[]>()).Returns(originalBytes);

        // The string Protect extension Base64-encodes the protected bytes,
        // and the string Unprotect extension Base64-decodes before calling Unprotect(byte[])
        string ciphertext = Convert.ToBase64String([1, 2, 3]);
        string result = _encryptor.Decrypt(ciphertext);

        result.Should().Be(original);
    }

    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginalValue()
    {
        string original = "firebase-api-key-12345";
        byte[] originalBytes = Encoding.UTF8.GetBytes(original);
        byte[] fakeEncrypted = [99, 100, 101];

        _dataProtector.Protect(Arg.Any<byte[]>()).Returns(fakeEncrypted);
        _dataProtector.Unprotect(Arg.Any<byte[]>()).Returns(originalBytes);

        string encrypted = _encryptor.Encrypt(original);
        string decrypted = _encryptor.Decrypt(encrypted);

        decrypted.Should().Be(original);
    }

    [Fact]
    public void Encrypt_CreatesProtectorWithCorrectPurpose()
    {
        _dataProtector.Protect(Arg.Any<byte[]>()).Returns([1, 2, 3]);

        _encryptor.Encrypt("test");

        _dataProtectionProvider.Received().CreateProtector("TenantPushCredentials");
    }
}
