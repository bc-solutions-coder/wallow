# Storage Module

## Overview

The Storage module provides a unified file storage abstraction supporting multiple backends (local filesystem and S3-compatible services). It serves as the low-level infrastructure layer for file operations across the Wallow platform.

**Key Responsibilities:**
- Raw file I/O operations with pluggable storage backends
- Bucket-based organization with configurable policies
- Multi-tenant isolation via storage key prefixes
- Presigned URL generation for direct client uploads/downloads
- File metadata management and lifecycle policies


## Key Features

- **Multi-Backend Support**: Switch between local filesystem (development) and S3-compatible services (production) via configuration
- **Bucket Policies**: Per-bucket settings for access control, file size limits, allowed content types, and retention policies
- **Tenant Isolation**: Files are stored with tenant-prefixed keys ensuring complete data separation
- **Presigned URLs**: Generate time-limited URLs for direct client access to storage without proxying through the API
- **GDPR Compliance**: Built-in data export and erasure capabilities for regulatory compliance

## Architecture

The module follows Clean Architecture with four layers:

```
┌─────────────────────────────────────────────────────────┐
│  Api Layer                                              │
│  Controllers, Request/Response contracts                │
├─────────────────────────────────────────────────────────┤
│  Application Layer                                      │
│  Commands, Queries, Handlers, DTOs, Interfaces          │
├─────────────────────────────────────────────────────────┤
│  Infrastructure Layer                                   │
│  EF Core, Storage Providers, Repositories, Compliance   │
├─────────────────────────────────────────────────────────┤
│  Domain Layer                                           │
│  Entities, Value Objects, Enums, Strongly-typed IDs     │
└─────────────────────────────────────────────────────────┘
```

### Directory Structure

```
src/Modules/Storage/
├── Wallow.Storage.Domain/
│   ├── Entities/
│   │   ├── StorageBucket.cs
│   │   └── StoredFile.cs
│   ├── Identity/
│   │   ├── StorageBucketId.cs
│   │   └── StoredFileId.cs
│   ├── Enums/
│   │   ├── AccessLevel.cs
│   │   ├── RetentionAction.cs
│   │   └── StorageProvider.cs
│   └── ValueObjects/
│       └── RetentionPolicy.cs
│
├── Wallow.Storage.Application/
│   ├── Commands/
│   │   ├── CreateBucket/
│   │   ├── DeleteBucket/
│   │   ├── DeleteFile/
│   │   └── UploadFile/
│   ├── Queries/
│   │   ├── GetBucketByName/
│   │   ├── GetFileById/
│   │   ├── GetFilesByBucket/
│   │   ├── GetPresignedUrl/
│   │   └── GetUploadPresignedUrl/
│   ├── DTOs/
│   │   ├── BucketDto.cs
│   │   ├── PresignedUrlResult.cs
│   │   ├── StoredFileDto.cs
│   │   └── UploadResult.cs
│   ├── Interfaces/
│   │   ├── IStorageBucketRepository.cs
│   │   ├── IStorageProvider.cs
│   │   └── IStoredFileRepository.cs
│   └── Mappings/
│       └── StorageMappings.cs
│
├── Wallow.Storage.Infrastructure/
│   ├── Configuration/
│   │   └── StorageOptions.cs
│   ├── Compliance/
│   │   ├── StorageDataEraser.cs
│   │   └── StorageDataExporter.cs
│   ├── Persistence/
│   │   ├── Configurations/
│   │   ├── Repositories/
│   │   ├── StorageDbContext.cs
│   │   └── Migrations/
│   ├── Providers/
│   │   ├── LocalStorageProvider.cs
│   │   └── S3StorageProvider.cs
│   └── Extensions/
│       └── InfrastructureExtensions.cs
│
├── Wallow.Storage.Api/
│   ├── Controllers/
│   │   └── StorageController.cs
│   ├── Contracts/
│   │   ├── Requests/
│   │   └── Responses/
│   └── Extensions/
│       ├── ResultExtensions.cs
│       └── StorageModuleExtensions.cs
│
└── README.md
```

## Domain Model

### Entities

#### StorageBucket

Platform-wide logical grouping of files with shared settings. Buckets define storage policies and are not tenant-scoped.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `StorageBucketId` | Strongly-typed unique identifier |
| `Name` | `string` | Unique bucket name |
| `Description` | `string?` | Optional description |
| `Access` | `AccessLevel` | Private or Public access |
| `MaxFileSizeBytes` | `long` | Maximum file size (0 = unlimited) |
| `AllowedContentTypes` | `string?` | JSON array of allowed MIME types (supports wildcards like `image/*`) |
| `Retention` | `RetentionPolicy?` | Optional lifecycle policy |
| `Versioning` | `bool` | Whether to enable file versioning |
| `CreatedAt` | `DateTime` | Creation timestamp |

**Key Methods:**
- `IsContentTypeAllowed(contentType)`: Validates file type against bucket policy
- `IsFileSizeAllowed(sizeBytes)`: Validates file size against bucket limit

#### StoredFile

Metadata for a stored file. Actual bytes live in the storage backend. Tenant-scoped for isolation.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `StoredFileId` | Strongly-typed unique identifier |
| `TenantId` | `TenantId` | Owning tenant |
| `BucketId` | `StorageBucketId` | Parent bucket |
| `FileName` | `string` | Original filename |
| `ContentType` | `string` | MIME type |
| `SizeBytes` | `long` | File size |
| `StorageKey` | `string` | Backend storage path (e.g., `tenant-123/bucket/file.pdf`) |
| `Path` | `string?` | Optional subfolder within bucket |
| `IsPublic` | `bool` | Public accessibility flag |
| `UploadedBy` | `Guid` | User who uploaded the file |
| `UploadedAt` | `DateTime` | Upload timestamp |
| `Metadata` | `string?` | Custom JSON metadata |

### Value Objects

#### RetentionPolicy

Defines file lifecycle rules.

```csharp
public sealed record RetentionPolicy(int Days, RetentionAction Action);
```

### Enums

| Enum | Values | Description |
|------|--------|-------------|
| `AccessLevel` | `Private`, `Public` | File/bucket visibility |
| `StorageProvider` | `Local`, `S3` | Storage backend type |
| `RetentionAction` | `Delete`, `Archive` | Action when retention expires |

### Strongly-Typed IDs

- `StorageBucketId`: Wrapper around `Guid` for bucket identification
- `StoredFileId`: Wrapper around `Guid` for file identification

## CQRS Pattern

### Commands

| Command | Description | Returns |
|---------|-------------|---------|
| `CreateBucketCommand` | Creates a new storage bucket with policies | `Result<BucketDto>` |
| `DeleteBucketCommand` | Deletes a bucket (optionally forced) | `Result` |
| `UploadFileCommand` | Uploads a file to storage | `Result<UploadResult>` |
| `DeleteFileCommand` | Deletes a file from storage | `Result` |

#### CreateBucketCommand

```csharp
public sealed record CreateBucketCommand(
    string Name,
    string? Description = null,
    AccessLevel Access = AccessLevel.Private,
    long MaxFileSizeBytes = 0,
    IReadOnlyList<string>? AllowedContentTypes = null,
    int? RetentionDays = null,
    RetentionAction? RetentionAction = null,
    bool Versioning = false
);
```

#### UploadFileCommand

```csharp
public sealed record UploadFileCommand(
    Guid TenantId,
    Guid UserId,
    string BucketName,
    string FileName,
    string ContentType,
    Stream Content,
    long SizeBytes,
    string? Path = null,
    bool IsPublic = false,
    string? Metadata = null
);
```

### Queries

| Query | Description | Returns |
|-------|-------------|---------|
| `GetBucketByNameQuery` | Retrieves bucket by name | `Result<BucketDto>` |
| `GetFileByIdQuery` | Retrieves file metadata by ID | `Result<StoredFileDto>` |
| `GetFilesByBucketQuery` | Lists files in a bucket with optional path prefix | `Result<IReadOnlyList<StoredFileDto>>` |
| `GetPresignedUrlQuery` | Generates presigned download URL | `Result<PresignedUrlResult>` |
| `GetUploadPresignedUrlQuery` | Generates presigned upload URL | `Result<PresignedUploadResult>` |

## API Endpoints

All endpoints require authentication and are prefixed with `/api/storage`.

### Bucket Operations

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/buckets` | Create a new bucket |
| `GET` | `/buckets/{name}` | Get bucket by name |
| `DELETE` | `/buckets/{name}?force=false` | Delete a bucket |

### File Operations

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/upload` | Upload a file (multipart/form-data) |
| `GET` | `/files/{id}` | Get file metadata |
| `GET` | `/files/{id}/download` | Download file (redirects to presigned URL) |
| `DELETE` | `/files/{id}` | Delete a file |
| `GET` | `/files?bucket=x&path=y` | List files in bucket |

### Presigned URLs

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/presigned-upload` | Get presigned URL for direct upload |
| `GET` | `/files/{id}/presigned-url` | Get presigned URL for download |

### Request/Response Examples

**Create Bucket Request:**
```json
{
  "name": "user-uploads",
  "description": "User uploaded files",
  "access": "Private",
  "maxFileSizeBytes": 10485760,
  "allowedContentTypes": ["image/*", "application/pdf"],
  "retentionDays": 365,
  "retentionAction": "Archive",
  "versioning": false
}
```

**Upload File:**
```http
POST /api/storage/upload
Content-Type: multipart/form-data

file: (binary)
bucket: user-uploads
path: documents/2026
isPublic: false
```

**Presigned Upload Request:**
```json
{
  "bucketName": "user-uploads",
  "fileName": "report.pdf",
  "contentType": "application/pdf",
  "sizeBytes": 1048576,
  "path": "reports",
  "expiryMinutes": 15
}
```

## Storage Providers

### IStorageProvider Interface

```csharp
public interface IStorageProvider
{
    Task<string> UploadAsync(Stream content, string key, string contentType, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, bool forUpload = false, CancellationToken ct = default);
}
```

### LocalStorageProvider

For development environments. Stores files on the local filesystem.

- Files stored at configured `BasePath`
- Presigned URLs return API endpoints (no true presigning)
- Automatic directory creation

### S3StorageProvider

For production environments. Works with any S3-compatible service.

- AWS S3, Garage, MinIO, Cloudflare R2
- Path-style addressing support for self-hosted services
- True presigned URL generation

## Configuration

### appsettings.json

```json
{
  "Storage": {
    "Provider": "Local",
    "Local": {
      "BasePath": "/var/wallow/storage",
      "BaseUrl": "http://localhost:5000"
    },
    "S3": {
      "Endpoint": "http://garage:3900",
      "AccessKey": "YOUR_ACCESS_KEY",
      "SecretKey": "YOUR_SECRET_KEY",
      "BucketName": "wallow-files",
      "UsePathStyle": true,
      "Region": "us-east-1"
    }
  }
}
```

### Configuration Options

| Option | Type | Description |
|--------|------|-------------|
| `Provider` | `Local` or `S3` | Active storage backend |
| `Local.BasePath` | `string` | Local filesystem path |
| `Local.BaseUrl` | `string` | Base URL for presigned URLs |
| `S3.Endpoint` | `string` | S3 service endpoint |
| `S3.AccessKey` | `string` | S3 access key |
| `S3.SecretKey` | `string` | S3 secret key |
| `S3.BucketName` | `string` | S3 bucket name |
| `S3.UsePathStyle` | `bool` | Use path-style URLs (required for Garage/MinIO) |
| `S3.Region` | `string` | AWS region |

### Supported Backends

| Backend | Use Case | Configuration |
|---------|----------|---------------|
| Local filesystem | Development | `Provider: "Local"` |
| AWS S3 | Production (managed) | `Provider: "S3"` |
| Garage | Production (self-hosted, lightweight) | `Provider: "S3"` + `UsePathStyle: true` |
| MinIO | Production (self-hosted, feature-rich) | `Provider: "S3"` + `UsePathStyle: true` |
| Cloudflare R2 | Production (no egress fees) | `Provider: "S3"` |

## Storage Key Format

Files are stored with tenant isolation enforced via key prefixes:

```
{tenantId}/{bucket}/{path}/{fileId}{extension}

Examples:
- tenant-abc123/invoices/2026/02/file-xyz.pdf
- tenant-abc123/avatars/user-123.jpg
- tenant-abc123/products/sku-456/images/main.webp
```

## Database Schema

The module uses PostgreSQL with the `storage` schema.

```sql
CREATE SCHEMA storage;

CREATE TABLE storage.buckets (
    id UUID PRIMARY KEY,
    name VARCHAR(100) UNIQUE NOT NULL,
    description TEXT,
    access VARCHAR(20) NOT NULL DEFAULT 'Private',
    max_file_size_bytes BIGINT DEFAULT 0,
    allowed_content_types JSONB,
    retention_days INT,
    retention_action VARCHAR(20),
    versioning BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE storage.files (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    bucket_id UUID NOT NULL REFERENCES storage.buckets(id),
    file_name VARCHAR(500) NOT NULL,
    content_type VARCHAR(200) NOT NULL,
    size_bytes BIGINT NOT NULL,
    storage_key VARCHAR(1000) NOT NULL,
    path VARCHAR(500),
    is_public BOOLEAN DEFAULT FALSE,
    uploaded_by UUID NOT NULL,
    uploaded_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    metadata JSONB
);

CREATE INDEX idx_files_tenant ON storage.files(tenant_id);
CREATE INDEX idx_files_bucket ON storage.files(bucket_id);
CREATE INDEX idx_files_path ON storage.files(bucket_id, path);
```

## Events

The Storage module does not publish integration events. It operates as a foundational service layer that other modules consume directly via commands and queries.

## GDPR Compliance

The module implements data compliance interfaces for regulatory requirements:

### StorageDataExporter

Exports all files uploaded by a specific user.

```csharp
public async Task<object?> ExportAsync(Guid tenantId, Guid userId)
```

Returns: File metadata summary including IDs, filenames, sizes, and upload dates.

### StorageDataEraser

Deletes all files uploaded by a specific user.

```csharp
public async Task<int> EraseAsync(Guid tenantId, Guid userId)
```

Returns: Count of deleted file records.

## Dependencies

### Internal Dependencies

- `Wallow.Shared.Kernel`: Base classes (Entity, ValueObject, Result, ITenantScoped)
- `Wallow.Shared.Contracts`: Cross-module interfaces (IDataExporter, IDataEraser)

### External Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.EntityFrameworkCore` | ORM for metadata storage |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | PostgreSQL provider |
| `AWSSDK.S3` | S3-compatible storage operations |
| `FluentValidation` | Command/query validation |
| `WolverineFx` | CQRS message handling |

## Integration Points

### Consumed By

- **Other Domain Modules**: May consume Storage for file management needs

### Does Not Consume

The Storage module is self-contained and does not depend on other domain modules.

## Module Registration

Register the Storage module in your application:

```csharp
// In Program.cs or Startup
builder.Services.AddStorageModule(builder.Configuration);

// In application pipeline
await app.UseStorageModuleAsync();
```

The registration handles:
- DbContext configuration with tenant interceptors
- Repository registrations
- Storage provider selection based on configuration
- Compliance handler registrations
- Database migration on startup

## Database Migrations

```bash
# Add a new migration
dotnet ef migrations add MigrationName \
    --project src/Modules/Storage/Wallow.Storage.Infrastructure \
    --startup-project src/Wallow.Api \
    --context StorageDbContext

# Apply migrations
dotnet ef database update \
    --project src/Modules/Storage/Wallow.Storage.Infrastructure \
    --startup-project src/Wallow.Api \
    --context StorageDbContext
```

## Testing

```bash
# Run all Storage module tests
dotnet test tests/Modules/Storage/Modules.Storage.Tests
```

## Related Documentation

- [Storage Module Design](../../../docs/plans/2026-02-05-storage-module-design.md) - Architecture decisions and design rationale
- [Developer Guide](../../../docs/getting-started/developer-guide.md) - General development practices
