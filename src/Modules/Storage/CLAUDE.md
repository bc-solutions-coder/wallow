# Storage Module

## Module Responsibility

Owns file storage lifecycle: bucket management, file upload/download/deletion, and presigned URL generation. Supports pluggable storage backends (local filesystem, S3). Retention policies are defined but not yet enforced.

## Layer Rules

- **Domain** (`Wallow.Storage.Domain`): Entities (`StorageBucket` -- NOT tenant-scoped, `StoredFile` -- tenant-scoped), value objects (`RetentionPolicy` with period + action: Archive/Delete/Anonymize). Domain depends only on `Shared.Kernel`.
- **Application** (`Wallow.Storage.Application`): Commands (`CreateBucket`, `DeleteBucket`, `UploadFile`, `DeleteFile`), queries (`GetBucketByName`, `GetFileById`, `GetFilesByBucket`, `GetPresignedUrl`). Defines `IStorageProvider` interface.
- **Infrastructure** (`Wallow.Storage.Infrastructure`): `StorageDbContext` (EF Core, `storage` schema), `LocalStorageProvider`, `S3StorageProvider` implementations.
- **Api** (`Wallow.Storage.Api`): `StorageController` (file upload/download/deletion).

## Key Patterns

- **Provider abstraction**: `IStorageProvider` interface with `StoreAsync`, `RetrieveAsync`, `DeleteAsync`. New backends implement this interface and register in `StorageModuleExtensions.cs`.
- **Bucket vs file tenancy**: `StorageBucket` is global (shared across tenants). `StoredFile` is tenant-scoped via `ITenantScoped`. This is intentional -- buckets are infrastructure-level groupings.

## Dependencies

- **Depends on**: `Wallow.Shared.Kernel` (base entities, `ITenantScoped`, Result pattern).
- **Depended on by**: `Wallow.Api` (registers module). No integration events published.

## Constraints

- Do not reference other modules directly.
- This module uses the `storage` PostgreSQL schema.
- `StorageBucket` is NOT tenant-scoped -- all tenants share buckets.
- No domain events are published currently.

## Known Gaps

- Retention policies defined but no enforcement mechanism (no background job)
- Versioning flag exists but not implemented
