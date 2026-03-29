# Storage Module

## Overview

The Storage module provides a unified file storage abstraction supporting multiple backends (local filesystem and S3-compatible services). It handles file uploads, metadata tracking, presigned URL generation, and virus scanning.

## Key Features

- **Multi-Backend Support**: Switch between local filesystem (development) and S3-compatible services (production) via configuration
- **Bucket Policies**: Per-bucket settings for access control, file size limits, allowed content types, and retention policies
- **Tenant Isolation**: Files stored with tenant-prefixed keys ensuring complete data separation
- **Presigned URLs**: Time-limited URLs for direct client access to storage without proxying through the API
- **Virus Scanning**: Optional ClamAV integration for uploaded file scanning (falls back to `NoOpFileScanner` when disabled)

## Architecture

```
src/Modules/Storage/
+-- Wallow.Storage.Domain         # Entities, Value Objects, Enums, Strongly-typed IDs
+-- Wallow.Storage.Application    # Commands, Queries, Handlers, DTOs, Interfaces
+-- Wallow.Storage.Infrastructure # EF Core, Storage Providers, Configuration, Scanning
+-- Wallow.Storage.Api            # Controllers, Request/Response Contracts
```

**Database Schema**: `storage` (PostgreSQL)

## Domain Model

### StorageBucket (Entity)

Platform-wide logical grouping of files with shared settings. Buckets define storage policies and are not tenant-scoped.

Key properties: `Name`, `Access` (Private/Public), `MaxFileSizeBytes`, `AllowedContentTypes`, `Retention` (RetentionPolicy), `Versioning`.

### StoredFile (Entity)

Metadata for a stored file. Actual bytes live in the storage backend. Tenant-scoped for isolation.

Key properties: `TenantId`, `BucketId`, `FileName`, `ContentType`, `SizeBytes`, `StorageKey`, `Path`, `IsPublic`, `UploadedBy`, `Status` (FileStatus).

### FileStatus (Enum)

| Value | Description |
|-------|-------------|
| `PendingValidation` | File uploaded, awaiting virus scan |
| `Available` | File passed validation |
| `Rejected` | File failed validation |

### Value Objects

- **RetentionPolicy**: Defines file lifecycle rules with `Days` and `RetentionAction` (Delete or Archive)

### Enums

| Enum | Values |
|------|--------|
| `AccessLevel` | `Private`, `Public` |
| `StorageProvider` | `Local`, `S3` |
| `RetentionAction` | `Delete`, `Archive` |

## Commands and Queries

### Commands

| Command | Description |
|---------|-------------|
| `CreateBucketCommand` | Create a new storage bucket with policies |
| `DeleteBucketCommand` | Delete a bucket |
| `UploadFileCommand` | Upload a file to storage |
| `DeleteFileCommand` | Delete a file from storage |
| `ScanUploadedFileCommand` | Scan an uploaded file for viruses |

### Queries

| Query | Description |
|-------|-------------|
| `GetBucketByNameQuery` | Retrieve bucket by name |
| `GetFileByIdQuery` | Retrieve file metadata by ID |
| `GetFilesByBucketQuery` | List files in a bucket with optional path prefix |
| `GetPresignedUrlQuery` | Generate presigned download URL |
| `GetUploadPresignedUrlQuery` | Generate presigned upload URL |

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

## Storage Providers

### LocalStorageProvider

For development environments. Stores files on the local filesystem. Presigned URLs return API endpoints (no true presigning).

### S3StorageProvider

For production environments. Works with any S3-compatible service (AWS S3, Garage, MinIO, Cloudflare R2). Supports path-style addressing for self-hosted services and true presigned URL generation.

Both providers implement `IStorageProvider` (defined in `Wallow.Shared.Contracts`).

## Storage Key Format

Files are stored with tenant isolation enforced via key prefixes:

```
{tenantId}/{bucket}/{path}/{fileId}{extension}
```

## Configuration

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

| Backend | Use Case | Configuration |
|---------|----------|---------------|
| Local filesystem | Development | `Provider: "Local"` |
| AWS S3 | Production (managed) | `Provider: "S3"` |
| Garage | Production (self-hosted) | `Provider: "S3"` + `UsePathStyle: true` |
| MinIO | Production (self-hosted) | `Provider: "S3"` + `UsePathStyle: true` |
| Cloudflare R2 | Production (no egress fees) | `Provider: "S3"` |

## Events

The Storage module does not publish integration events. It operates as a foundational service layer.

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Wallow.Shared.Kernel` | Base classes, multi-tenancy, Result pattern |
| `Wallow.Shared.Contracts` | `IStorageProvider` interface, cross-module contracts |

## Testing

```bash
./scripts/run-tests.sh storage
```

## EF Core Migrations

```bash
dotnet ef migrations add MigrationName \
    --project src/Modules/Storage/Wallow.Storage.Infrastructure \
    --startup-project src/Wallow.Api \
    --context StorageDbContext
```
