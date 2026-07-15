using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Storage.Infrastructure.Configuration;

namespace Wallow.Storage.Infrastructure.Providers;

/// <summary>
/// S3-compatible storage provider. Works with AWS S3, Garage, MinIO, and Cloudflare R2.
/// </summary>
public sealed class S3StorageProvider(IAmazonS3 s3Client, IOptions<StorageOptions> options, ITenantContext tenantContext) : IStorageProvider
{
    private readonly IAmazonS3 _s3Client = s3Client;
    private readonly S3StorageOptions _options = options.Value.S3;
    private readonly ITenantContext _tenantContext = tenantContext;
    private string ResolveBucket() => _options.GetBucketForRegion(_tenantContext.Region);

    public async Task<string> UploadAsync(Stream content, string key, string contentType, CancellationToken ct = default)
    {
        PutObjectRequest request = new()
        {
            BucketName = ResolveBucket(),
            Key = key,
            InputStream = content,
            ContentType = contentType
        };

        PutObjectResponse response = await _s3Client.PutObjectAsync(request, ct);
        return response.ETag;
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        GetObjectRequest request = new()
        {
            BucketName = ResolveBucket(),
            Key = key
        };

        GetObjectResponse response = await _s3Client.GetObjectAsync(request, ct);
        return response.ResponseStream;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        DeleteObjectRequest request = new()
        {
            BucketName = ResolveBucket(),
            Key = key
        };

        await _s3Client.DeleteObjectAsync(request, ct);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            GetObjectMetadataRequest request = new()
            {
                BucketName = ResolveBucket(),
                Key = key
            };

            await _s3Client.GetObjectMetadataAsync(request, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, bool forUpload = false, CancellationToken ct = default)
    {
        GetPreSignedUrlRequest request = new()
        {
            BucketName = ResolveBucket(),
            Key = key,
            Expires = DateTime.UtcNow.Add(expiry),
            Verb = forUpload ? HttpVerb.PUT : HttpVerb.GET
        };

        return _s3Client.GetPreSignedURLAsync(request);
    }

}
