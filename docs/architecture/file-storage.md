# File Storage

This guide covers file management in Wallow through the Storage module.

## Overview

The **Storage** module provides a file storage abstraction supporting multiple backends. Files are stored with tenant isolation, bucket-based organization, presigned URL support, and optional virus scanning.

### Architecture

```
Client Applications
        │
        ▼
  Storage Module
  • StoredFile metadata
  • StorageBucket policies
  • Upload / Download / Delete
  • Presigned URLs
  • File scanning (optional)
        │
        ▼
   IStorageProvider
  ┌──────────┬──────────────┐
  ▼          ▼              ▼
Local     AWS S3         GarageHQ
Filesystem Cloudflare R2  (Self-hosted)
```

## IStorageProvider Interface

The core abstraction is defined in `src/Shared/Wallow.Shared.Contracts/Storage/IStorageProvider.cs`:

- `UploadAsync` — upload content, returns an ETag
- `DownloadAsync` — download content as a stream
- `DeleteAsync` — remove a file
- `ExistsAsync` — check if a file exists
- `GetPresignedUrlAsync` — generate a time-limited URL for direct access (upload or download)

### Implementations

- **`LocalStorageProvider`** (`src/Modules/Storage/Wallow.Storage.Infrastructure/Providers/LocalStorageProvider.cs`) — stores files on the local filesystem. Used in development.
- **`S3StorageProvider`** (`src/Modules/Storage/Wallow.Storage.Infrastructure/Providers/S3StorageProvider.cs`) — uses any S3-compatible backend. Used in production.

## Configuration

Storage options are defined in `src/Modules/Storage/Wallow.Storage.Infrastructure/Configuration/StorageOptions.cs`.

### Provider Selection

```json
{
  "Storage": {
    "Provider": "Local",
    "Local": {
      "BasePath": "/var/wallow/storage",
      "BaseUrl": "http://localhost:5000"
    },
    "S3": {
      "Endpoint": "http://localhost:3900",
      "AccessKey": "...",
      "SecretKey": "...",
      "BucketName": "wallow-files",
      "UsePathStyle": true,
      "Region": "us-east-1"
    }
  }
}
```

Set `Provider` to `"Local"` or `"S3"`. The S3 options also support `RegionBuckets` — a dictionary of region-specific bucket overrides that falls back to `BucketName`.

### Supported S3-Compatible Backends

| Backend | `UsePathStyle` | Notes |
|---------|----------------|-------|
| AWS S3 | `false` | Standard AWS configuration |
| GarageHQ | `true` | Self-hosted, lightweight (used in local dev) |
| MinIO | `true` | Self-hosted, feature-rich |
| Cloudflare R2 | `false` | No egress fees |
| DigitalOcean Spaces | `false` | Regional endpoints |

### File Scanning (ClamAV)

Virus scanning is optional and disabled by default. When enabled, uploaded files are scanned via ClamAV before being stored. When disabled, a `NoOpFileScanner` is used instead.

```json
{
  "Storage": {
    "ClamAv": {
      "Enabled": true,
      "Host": "localhost",
      "Port": 3310
    }
  }
}
```

To run ClamAV locally, start it with the `clamav` Docker Compose profile:

```bash
cd docker && docker compose --profile clamav up -d
```

When ClamAV is enabled, a health check is registered that verifies TCP connectivity to the ClamAV daemon. The scanning logic lives in `src/Modules/Storage/Wallow.Storage.Infrastructure/Scanning/ClamAvFileScanner.cs`, with `NoOpFileScanner` as the fallback at `src/Modules/Storage/Wallow.Storage.Infrastructure/Scanning/NoOpFileScanner.cs`.

### Size Limits

Kestrel's global request body limit is set to 1 MB in `Program.cs`. The storage upload endpoint overrides this to 100 MB via `[RequestSizeLimit(100 * 1024 * 1024)]`.

## API Endpoints

All storage endpoints are versioned and require authorization. The base path is `/api/v1/storage`.

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/storage/buckets` | Create a storage bucket |
| GET | `/api/v1/storage/buckets/{name}` | Get bucket by name |
| DELETE | `/api/v1/storage/buckets/{name}` | Delete a bucket |
| POST | `/api/v1/storage/upload` | Upload a file (multipart) |
| GET | `/api/v1/storage/files/{id}` | Get file metadata |
| GET | `/api/v1/storage/files/{id}/download` | Download (redirects to presigned URL) |
| GET | `/api/v1/storage/files/{id}/presigned-url` | Get presigned download URL |
| DELETE | `/api/v1/storage/files/{id}` | Delete a file |
| GET | `/api/v1/storage/files?bucket=x&path=y` | List files in bucket (paginated) |
| POST | `/api/v1/storage/presigned-upload` | Get presigned upload URL |

The controller is at `src/Modules/Storage/Wallow.Storage.Api/Controllers/StorageController.cs`.

## Upload Patterns

### Server-Side Upload

Files uploaded through the API are processed by `UploadFileHandler`. The handler:

1. Validates bucket existence, content type, and file size
2. Sanitizes the filename
3. Scans the file via `IFileScanner` (ClamAV or no-op)
4. Uploads to the storage backend
5. Persists `StoredFile` metadata to PostgreSQL

### Direct Client Upload (Presigned URLs)

For large files, clients request a presigned upload URL via `POST /api/v1/storage/presigned-upload`, then upload directly to the storage backend, bypassing the API for the file transfer.

### Download

The download endpoint (`GET /api/v1/storage/files/{id}/download`) redirects the client to a presigned URL, offloading bandwidth from the API.

## Multi-Tenancy

### Tenant Isolation

All files are stored with tenant prefixes:

```
tenant-{tenantId}/{bucket}/{path}/{fileId}{extension}
```

The `StoredFile` entity (`src/Modules/Storage/Wallow.Storage.Domain/Entities/StoredFile.cs`) implements `ITenantScoped`. EF Core applies a `TenantSaveChangesInterceptor` to enforce tenant isolation on all queries.

### Bucket Validation

Storage buckets (`src/Modules/Storage/Wallow.Storage.Domain/Entities/StorageBucket.cs`) enforce validation rules including allowed content types (with wildcard support like `image/*`) and maximum file size.

## Troubleshooting

**File upload fails with "Bucket not found"**: Ensure the bucket exists. Create it first via `POST /api/v1/storage/buckets`.

**S3 upload fails with connection error**: Verify `Storage:S3:Endpoint` is correct. For self-hosted backends (GarageHQ, MinIO), ensure `UsePathStyle: true`.

**Presigned URLs expire immediately**: Check server and S3 time synchronization.

**File rejected by scanner**: If ClamAV is enabled and rejects a file, check ClamAV logs. Ensure virus definitions are up to date.

**Debug logging**: Set `"Wallow.Storage": "Debug"` in `Logging:LogLevel` for detailed storage operation logs.
