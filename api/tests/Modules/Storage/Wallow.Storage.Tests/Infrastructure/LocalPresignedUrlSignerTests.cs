using Wallow.Storage.Application.Services;

namespace Wallow.Storage.Tests.Infrastructure;

public sealed class LocalPresignedUrlSignerTests
{
    private readonly LocalPresignedUrlSigner _signer = new();

    private static long FutureExpiry => DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds();

    [Fact]
    public void Validate_WithSignatureFromSign_ReturnsTrue()
    {
        long expires = FutureExpiry;
        string signature = _signer.Sign(
            LocalPresignedUrlSigner.DownloadMethod, "tenant-1/bucket/file.txt", expires);

        bool valid = _signer.Validate(
            LocalPresignedUrlSigner.DownloadMethod, "tenant-1/bucket/file.txt", expires, signature);

        valid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenExpiryHasPassed_ReturnsFalse()
    {
        // A signature over a past timestamp is authentic but no longer honored.
        long expires = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds();
        string signature = _signer.Sign(
            LocalPresignedUrlSigner.DownloadMethod, "tenant-1/bucket/file.txt", expires);

        bool valid = _signer.Validate(
            LocalPresignedUrlSigner.DownloadMethod, "tenant-1/bucket/file.txt", expires, signature);

        valid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithDifferentKey_ReturnsFalse()
    {
        long expires = FutureExpiry;
        string signature = _signer.Sign(
            LocalPresignedUrlSigner.DownloadMethod, "tenant-1/bucket/file.txt", expires);

        bool valid = _signer.Validate(
            LocalPresignedUrlSigner.DownloadMethod, "tenant-1/bucket/other.txt", expires, signature);

        valid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithDifferentExpiry_ReturnsFalse()
    {
        // Extending the deadline invalidates the signature: expiry is signed, not advisory.
        long expires = FutureExpiry;
        string signature = _signer.Sign(
            LocalPresignedUrlSigner.DownloadMethod, "tenant-1/bucket/file.txt", expires);

        bool valid = _signer.Validate(
            LocalPresignedUrlSigner.DownloadMethod, "tenant-1/bucket/file.txt", expires + 60, signature);

        valid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithDownloadSignatureForUploadMethod_ReturnsFalse()
    {
        // A download URL must not authorize a write.
        long expires = FutureExpiry;
        string signature = _signer.Sign(
            LocalPresignedUrlSigner.DownloadMethod, "tenant-1/bucket/file.txt", expires);

        bool valid = _signer.Validate(
            LocalPresignedUrlSigner.UploadMethod, "tenant-1/bucket/file.txt", expires, signature);

        valid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithGarbageSignature_ReturnsFalse()
    {
        bool valid = _signer.Validate(
            LocalPresignedUrlSigner.DownloadMethod, "tenant-1/bucket/file.txt", FutureExpiry, "not-a-signature");

        valid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithSignatureFromAnotherSignerInstance_ReturnsFalse()
    {
        // Each instance holds its own random key; only the process-wide singleton's
        // signatures are honored, so URLs die with the process that minted them.
        LocalPresignedUrlSigner other = new();
        long expires = FutureExpiry;
        string signature = other.Sign(
            LocalPresignedUrlSigner.DownloadMethod, "tenant-1/bucket/file.txt", expires);

        bool valid = _signer.Validate(
            LocalPresignedUrlSigner.DownloadMethod, "tenant-1/bucket/file.txt", expires, signature);

        valid.Should().BeFalse();
    }

    [Fact]
    public void Sign_ProducesUrlSafeSignature()
    {
        string signature = _signer.Sign(
            LocalPresignedUrlSigner.DownloadMethod, "tenant-1/bucket/file.txt", FutureExpiry);

        Uri.EscapeDataString(signature).Should().Be(signature);
    }
}
