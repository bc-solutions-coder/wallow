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
        // Distinct instances have different keys; the registered singleton loses its key on restart.
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
