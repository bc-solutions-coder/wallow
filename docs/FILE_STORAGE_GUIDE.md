# File Storage Guide

This guide covers file management in Foundry, including raw storage operations and domain-level asset management.

## Overview

Foundry provides file management through the **Storage** module, which offers a raw file storage abstraction supporting multiple backends.

### When to Use Storage

- Uploading invoices, receipts, or other documents
- Storing user-generated files
- Building custom file handling for module-specific needs
- Implementing backup or archive functionality

### Architecture Overview

```
┌──────────────────────────────────────────────────────────────┐
│                     Client Applications                       │
└──────────────────────────────────────────────────────────────┘
                              │
                              ▼
                ┌─────────────────────────────┐
                │      Storage Module          │
                │  ─────────────────────────   │
                │  • StoredFile metadata       │
                │  • StorageBucket policies    │
                │  • Upload/Download/Delete    │
                │  • Presigned URLs            │
                └──────────────┬───────────────┘
                               │
                               ▼
                ┌────────────────────────────────┐
                │      IStorageProvider          │
                │  ────────────────────────────  │
                │  LocalStorageProvider (dev)    │
                │  S3StorageProvider (prod)      │
                └────────────────────────────────┘
                               │
          ┌────────────────────┼────────────────┐
          ▼                    ▼                ▼
┌──────────────────┐  ┌────────────────────┐  ┌───────────────┐
│  Local Filesystem │  │    AWS S3          │  │ MinIO/Garage  │
│  (Development)    │  │    Cloudflare R2   │  │ (Self-hosted) │
└──────────────────┘  └────────────────────┘  └───────────────┘
```

---

## Storage Module (Raw Files)

The Storage module provides raw file storage abstraction supporting multiple backends. Files are stored with tenant isolation, bucket-based organization, and presigned URL support.

### IStorageProvider Interface

The core abstraction for storage backends:

```csharp
// src/Shared/Foundry.Shared.Contracts/Storage/IStorageProvider.cs
public interface IStorageProvider
{
    /// <summary>
    /// Upload content to the storage backend.
    /// </summary>
    /// <returns>The ETag or version identifier of the uploaded content.</returns>
    Task<string> UploadAsync(
        Stream content,
        string key,
        string contentType,
        CancellationToken ct = default);

    /// <summary>
    /// Download content from the storage backend.
    /// </summary>
    Task<Stream> DownloadAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Delete a file from the storage backend.
    /// </summary>
    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Check if a file exists in the storage backend.
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Generate a presigned URL for direct access to the file.
    /// </summary>
    /// <param name="key">The storage key.</param>
    /// <param name="expiry">How long the URL should be valid.</param>
    /// <param name="forUpload">If true, generate a URL for uploading; otherwise for downloading.</param>
    Task<string> GetPresignedUrlAsync(
        string key,
        TimeSpan expiry,
        bool forUpload = false,
        CancellationToken ct = default);
}
```

### Local File System Provider

For development environments, files are stored on the local filesystem:

```csharp
// src/Modules/Storage/Foundry.Storage.Infrastructure/Providers/LocalStorageProvider.cs
public sealed class LocalStorageProvider : IStorageProvider
{
    public async Task<string> UploadAsync(
        Stream content,
        string key,
        string contentType,
        CancellationToken ct = default)
    {
        var filePath = GetFilePath(key);
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = new FileStream(
            filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, ct);

        return Convert.ToBase64String(BitConverter.GetBytes(DateTime.UtcNow.Ticks));
    }

    public Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        var filePath = GetFilePath(key);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {key}", key);

        var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream>(stream);
    }

    private string GetFilePath(string key)
    {
        var normalizedKey = key.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_options.BasePath, normalizedKey);
    }
}
```

### S3 Storage Provider

For production environments, use any S3-compatible storage:

```csharp
// src/Modules/Storage/Foundry.Storage.Infrastructure/Providers/S3StorageProvider.cs
public sealed class S3StorageProvider : IStorageProvider, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly S3StorageOptions _options;

    public S3StorageProvider(IOptions<StorageOptions> options)
    {
        _options = options.Value.S3;

        var config = new AmazonS3Config
        {
            ServiceURL = _options.Endpoint,
            ForcePathStyle = _options.UsePathStyle,
            RegionEndpoint = RegionEndpoint.GetBySystemName(_options.Region)
        };

        _s3Client = new AmazonS3Client(
            _options.AccessKey,
            _options.SecretKey,
            config);
    }

    public async Task<string> UploadAsync(
        Stream content,
        string key,
        string contentType,
        CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType
        };

        var response = await _s3Client.PutObjectAsync(request, ct);
        return response.ETag;
    }

    public Task<string> GetPresignedUrlAsync(
        string key,
        TimeSpan expiry,
        bool forUpload = false,
        CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            Expires = DateTime.UtcNow.Add(expiry),
            Verb = forUpload ? HttpVerb.PUT : HttpVerb.GET
        };

        var url = _s3Client.GetPreSignedURL(request);
        return Task.FromResult(url);
    }
}
```

### Upload/Download Patterns

#### Server-Side Upload

For files uploaded through the API:

```csharp
// Upload handler builds storage key with tenant isolation
private static string BuildStorageKey(
    Guid tenantId,
    string bucketName,
    string? path,
    Guid fileId,
    string extension)
{
    var parts = new List<string>
    {
        $"tenant-{tenantId}",
        bucketName
    };

    if (!string.IsNullOrWhiteSpace(path))
        parts.Add(path.Trim('/'));

    parts.Add($"{fileId}{extension}");

    return string.Join("/", parts);
}

// Example key: tenant-abc123/invoices/2026/02/file-xyz.pdf
```

#### Direct Client Upload (Presigned URLs)

For large files, clients can upload directly to storage:

```csharp
// 1. Client requests a presigned upload URL
POST /api/storage/presigned-upload
{
    "bucketName": "documents",
    "fileName": "report.pdf",
    "contentType": "application/pdf",
    "sizeBytes": 5242880,
    "expiryMinutes": 15
}

// 2. API validates and returns presigned URL
{
    "uploadUrl": "https://s3.example.com/bucket/key?X-Amz-...",
    "storageKey": "tenant-123/documents/abc-def.pdf",
    "expiresAt": "2026-02-15T12:15:00Z"
}

// 3. Client uploads directly to S3
PUT {uploadUrl}
Content-Type: application/pdf
[file bytes]
```

### Streaming Large Files

For efficient handling of large files:

```csharp
// Download with streaming (no memory buffering)
public async Task<IActionResult> Download(Guid id, CancellationToken ct)
{
    var result = await _bus.InvokeAsync<Result<PresignedUrlResult>>(
        new GetPresignedUrlQuery(_tenantContext.TenantId.Value, id), ct);

    if (result.IsFailure)
        return result.ToActionResult();

    // Redirect to presigned URL - client downloads directly from storage
    return Redirect(result.Value!.Url);
}

// For server-side processing, stream directly
await using var stream = await _storageProvider.DownloadAsync(storageKey, ct);
// Process stream without loading entire file into memory
```


---

## Configuration

### Storage Provider Selection

```json
// appsettings.json
{
  "Storage": {
    "Provider": "Local",  // Options: "Local", "S3"
    "Local": {
      "BasePath": "/var/foundry/storage",
      "BaseUrl": "http://localhost:5000"
    },
    "S3": {
      "Endpoint": "http://localhost:9000",
      "AccessKey": "minioadmin",
      "SecretKey": "minioadmin",
      "BucketName": "foundry-files",
      "UsePathStyle": true,
      "Region": "us-east-1"
    }
  }
}
```

### S3 Bucket Configuration

Configuration options for S3-compatible storage:

```csharp
// src/Modules/Storage/Foundry.Storage.Infrastructure/Configuration/StorageOptions.cs
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public StorageProvider Provider { get; set; } = StorageProvider.Local;
    public LocalStorageOptions Local { get; set; } = new();
    public S3StorageOptions S3 { get; set; } = new();
}

public sealed class S3StorageOptions
{
    public string Endpoint { get; set; } = string.Empty;      // S3 endpoint URL
    public string AccessKey { get; set; } = string.Empty;     // AWS access key
    public string SecretKey { get; set; } = string.Empty;     // AWS secret key
    public string BucketName { get; set; } = string.Empty;    // Target bucket
    public bool UsePathStyle { get; set; } = true;            // Required for MinIO/Garage
    public string Region { get; set; } = "us-east-1";         // AWS region
}
```

### Supported S3-Compatible Backends

| Backend | Configuration | Notes |
|---------|---------------|-------|
| AWS S3 | `UsePathStyle: false` | Standard AWS configuration |
| MinIO | `UsePathStyle: true` | Self-hosted, feature-rich |
| Garage | `UsePathStyle: true` | Self-hosted, lightweight |
| Cloudflare R2 | `UsePathStyle: false` | No egress fees |
| DigitalOcean Spaces | `UsePathStyle: false` | Regional endpoints |

#### Production Example (AWS S3)

```json
{
  "Storage": {
    "Provider": "S3",
    "S3": {
      "Endpoint": "https://s3.us-west-2.amazonaws.com",
      "AccessKey": "${AWS_ACCESS_KEY_ID}",
      "SecretKey": "${AWS_SECRET_ACCESS_KEY}",
      "BucketName": "your-app-files",
      "UsePathStyle": false,
      "Region": "us-west-2"
    }
  }
}
```

#### Self-Hosted Example (MinIO)

```json
{
  "Storage": {
    "Provider": "S3",
    "S3": {
      "Endpoint": "http://minio:9000",
      "AccessKey": "minioadmin",
      "SecretKey": "minioadmin",
      "BucketName": "foundry",
      "UsePathStyle": true,
      "Region": "us-east-1"
    }
  }
}
```

### Local Storage Path

```json
{
  "Storage": {
    "Provider": "Local",
    "Local": {
      "BasePath": "/var/foundry/storage",
      "BaseUrl": "http://localhost:5000"
    }
  }
}
```

---

## Uploading Files

### API Endpoints for Uploads

#### Storage Module - Direct File Upload

```http
POST /api/storage/upload
Content-Type: multipart/form-data

file: [binary]
bucket: "documents"
path: "invoices/2026"
isPublic: false
```

Response:
```json
{
  "fileId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "fileName": "invoice.pdf",
  "sizeBytes": 245632,
  "contentType": "application/pdf",
  "uploadedAt": "2026-02-15T10:30:00Z"
}
```

### Multipart Form Handling

The controllers use ASP.NET Core's `IFormFile` for multipart handling:

```csharp
// src/Modules/Storage/Foundry.Storage.Api/Controllers/StorageController.cs
[HttpPost("upload")]
[RequestSizeLimit(100 * 1024 * 1024)] // 100MB limit
[Consumes("multipart/form-data")]
public async Task<IActionResult> Upload(
    IFormFile file,
    [FromForm] string bucket,
    [FromForm] string? path = null,
    [FromForm] bool isPublic = false,
    CancellationToken cancellationToken = default)
{
    if (file.Length == 0)
        return BadRequest("File is empty");

    await using var stream = file.OpenReadStream();

    var command = new UploadFileCommand(
        _tenantContext.TenantId.Value,
        GetCurrentUserId(),
        bucket,
        file.FileName,
        file.ContentType,
        stream,
        file.Length,
        path,
        isPublic);

    var result = await _bus.InvokeAsync<Result<UploadResult>>(command, cancellationToken);
    return result.Map(ToUploadResponse)
        .ToCreatedResult($"/api/storage/files/{result.Value?.FileId}");
}
```

### File Validation

Storage buckets enforce validation rules:

```csharp
// src/Modules/Storage/Foundry.Storage.Domain/Entities/StorageBucket.cs
public bool IsContentTypeAllowed(string contentType)
{
    if (string.IsNullOrEmpty(AllowedContentTypes))
        return true;

    var allowedTypes = JsonSerializer.Deserialize<List<string>>(AllowedContentTypes);

    foreach (var pattern in allowedTypes)
    {
        // Support wildcards like "image/*"
        if (pattern.EndsWith("/*"))
        {
            var prefix = pattern[..^2];
            if (contentType.StartsWith(prefix + "/"))
                return true;
        }
        else if (pattern == contentType)
        {
            return true;
        }
    }
    return false;
}

public bool IsFileSizeAllowed(long sizeBytes)
{
    if (MaxFileSizeBytes == 0)
        return true;  // 0 = unlimited
    return sizeBytes <= MaxFileSizeBytes;
}
```

### Size Limits

Configure upload size limits in Program.cs:

```csharp
// Global limit (1MB default in Kestrel configuration)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1_048_576; // 1MB
});

// Per-endpoint via attribute to allow larger uploads on specific routes
[RequestSizeLimit(100 * 1024 * 1024)] // 100MB per-endpoint override
```

---

## Serving Files

### Direct Serving vs Presigned URLs

| Approach | Use Case | Pros | Cons |
|----------|----------|------|------|
| Direct serving | Small files, API-proxied | Simple, access control built-in | API bottleneck, memory usage |
| Presigned URLs | Large files, CDN-backed | Scalable, offloads API | More complex, time-limited |

### Presigned URL Generation

```csharp
// Get presigned download URL
[HttpGet("files/{id:guid}/presigned-url")]
public async Task<IActionResult> GetPresignedDownloadUrl(
    Guid id,
    [FromQuery] int? expiryMinutes = null,
    CancellationToken cancellationToken = default)
{
    var expiry = expiryMinutes.HasValue
        ? TimeSpan.FromMinutes(expiryMinutes.Value)
        : TimeSpan.FromMinutes(15);  // Default 15 minutes

    var result = await _bus.InvokeAsync<Result<PresignedUrlResult>>(
        new GetPresignedUrlQuery(_tenantContext.TenantId.Value, id, expiry),
        cancellationToken);

    return result.Map(r => new PresignedUrlResponse(r.Url, r.ExpiresAt))
        .ToActionResult();
}
```

### CDN Integration Points

For production deployments, integrate with a CDN:

```
Client Request
       │
       ▼
┌──────────────────┐
│    CloudFlare    │  Cache presigned URLs
│    or AWS        │  Serve from edge
│    CloudFront    │
└────────┬─────────┘
         │ (cache miss)
         ▼
┌──────────────────┐
│   Foundry API    │  Generate presigned URL
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│   S3 Storage     │  Serve actual file
└──────────────────┘
```

### Access Control

Files respect tenant isolation and access levels:

```csharp
// src/Modules/Storage/Foundry.Storage.Domain/Entities/StoredFile.cs
public sealed class StoredFile : Entity<StoredFileId>, ITenantScoped
{
    public TenantId TenantId { get; set; }     // Enforces tenant isolation
    public bool IsPublic { get; private set; }  // Public files skip auth check

    // Storage key includes tenant prefix for isolation
    // e.g., "tenant-abc123/bucket/path/file.pdf"
    public string StorageKey { get; private set; }
}
```

---

## Multi-Tenancy

### Tenant Isolation in Storage

All files are stored with tenant prefixes for strict isolation:

```
Storage Key Format:
tenant-{tenantId}/{bucket}/{path}/{fileId}{extension}

Examples:
tenant-abc123/invoices/2026/02/file-xyz.pdf
tenant-abc123/avatars/user-123.jpg
tenant-def456/products/sku-456/images/main.webp
```

### Path/Bucket Strategies

| Strategy | Description | Use Case |
|----------|-------------|----------|
| Tenant prefix in path | `tenant-{id}/bucket/file` | Single S3 bucket, path-based isolation |
| Tenant prefix in bucket | `tenant-{id}-bucket` | Separate buckets per tenant |
| Separate S3 buckets | Different credentials per tenant | Enterprise isolation requirements |

The default implementation uses tenant prefix in path:

```csharp
// src/Modules/Storage/Foundry.Storage.Application/Commands/UploadFile/UploadFileHandler.cs
private static string BuildStorageKey(
    Guid tenantId,
    string bucketName,
    string? path,
    Guid fileId,
    string extension)
{
    var parts = new List<string>
    {
        $"tenant-{tenantId}",  // Tenant isolation
        bucketName
    };

    if (!string.IsNullOrWhiteSpace(path))
        parts.Add(path.Trim('/'));

    parts.Add($"{fileId}{extension}");

    return string.Join("/", parts);
}
```

### Query Filtering

All queries automatically filter by tenant:

```csharp
// Entities implement ITenantScoped
public sealed class StoredFile : Entity<StoredFileId>, ITenantScoped
{
    public TenantId TenantId { get; set; }
    // ...
}

// EF Core automatically applies tenant filter via interceptor
services.AddDbContext<StorageDbContext>((sp, options) =>
{
    options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
});
```

---

## API Reference

### Storage Module Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/storage/buckets` | Create a storage bucket |
| GET | `/api/storage/buckets/{name}` | Get bucket by name |
| DELETE | `/api/storage/buckets/{name}` | Delete a bucket |
| POST | `/api/storage/upload` | Upload a file (multipart) |
| GET | `/api/storage/files/{id}` | Get file metadata |
| GET | `/api/storage/files/{id}/download` | Download (redirects to presigned URL) |
| GET | `/api/storage/files/{id}/presigned-url` | Get presigned download URL |
| DELETE | `/api/storage/files/{id}` | Delete a file |
| GET | `/api/storage/files?bucket=x&path=y` | List files in bucket |
| POST | `/api/storage/presigned-upload` | Get presigned upload URL |

---

## Usage Examples

### Upload a Document

```csharp
// Using Wolverine message bus
var command = new UploadFileCommand(
    tenantId: _tenantContext.TenantId.Value,
    userId: currentUserId,
    bucketName: "documents",
    fileName: "contract.pdf",
    contentType: "application/pdf",
    content: fileStream,
    sizeBytes: fileStream.Length,
    path: "contracts/2026",
    isPublic: false);

var result = await _bus.InvokeAsync<Result<UploadResult>>(command);

if (result.IsSuccess)
{
    var fileId = result.Value.FileId;
    var storageKey = result.Value.StorageKey;
}
```

---

## Troubleshooting

### Common Issues

**File upload fails with "Bucket not found"**
- Ensure the bucket exists. Create it first via `POST /api/storage/buckets`
- Check bucket name is spelled correctly (case-sensitive)

**S3 upload fails with connection error**
- Verify `Storage:S3:Endpoint` is correct
- For self-hosted (MinIO/Garage), ensure `UsePathStyle: true`
- Check AWS credentials are valid

**Presigned URLs expire immediately**
- Check server and S3 time synchronization
- Increase expiry time in configuration

### Debugging

```csharp
// Enable detailed logging
"Logging": {
  "LogLevel": {
    "Foundry.Storage": "Debug"
  }
}
```

### Performance Tips

1. **Use presigned URLs for large files** - Offloads bandwidth from API
2. **Configure CDN** - Cache presigned URLs at edge locations
3. **Set appropriate bucket policies** - Restrict content types and sizes
