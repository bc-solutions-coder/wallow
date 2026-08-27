# Storage Module — Agent Guide

## Module Purpose

Tenant-isolated file storage: buckets, uploads, metadata, presigned URLs and virus scanning,
over a pluggable backend (local filesystem or any S3-compatible service).

## Key File Locations

| Area | Path |
|------|------|
| Domain entities | `Wallow.Storage.Domain/Entities/` (`StorageBucket`, `StoredFile`) |
| Enums | `Wallow.Storage.Domain/Enums/` (`FileStatus`, `AccessLevel`, `StorageProvider`, `RetentionAction`) |
| Strongly-typed IDs | `Wallow.Storage.Domain/Identity/` |
| Domain events | `Wallow.Storage.Domain/Events/StorageEvents.cs` |
| Commands & handlers | `Wallow.Storage.Application/Commands/` |
| Queries & handlers | `Wallow.Storage.Application/Queries/` |
| Repository interfaces | `Wallow.Storage.Application/Interfaces/` |
| Scanner interface | `Wallow.Storage.Application/Interfaces/IFileScanner.cs` |
| Settings registry | `Wallow.Storage.Application/Settings/StorageSettingKeys.cs` |
| Filename sanitizer | `Wallow.Storage.Application/Utilities/FileNameSanitizer.cs` |
| Storage backends | `Wallow.Storage.Infrastructure/Providers/` (`LocalStorageProvider`, `S3StorageProvider`) |
| Scanners | `Wallow.Storage.Infrastructure/Scanning/` (`ClamAvFileScanner`, `NoOpFileScanner`) |
| Repository implementations | `Wallow.Storage.Infrastructure/Persistence/Repositories/` |
| EF configurations | `Wallow.Storage.Infrastructure/Persistence/Configurations/` |
| Module registration | `Wallow.Storage.Infrastructure/Modules/StorageModule.cs` + `Extensions/StorageInfrastructureExtensions.cs` |
| Controllers | `Wallow.Storage.Api/Controllers/` (`StorageController`, `StorageSettingsController`) |
| Request/response contracts | `Wallow.Storage.Api/Contracts/` |
| Tests | `tests/Modules/Storage/Wallow.Storage.Tests/` |

## Shared Contracts

Unusually for a module, Storage's outward surface is an **interface plus a command record**, not
an event. Both live in `Wallow.Shared.Contracts/Storage/`:

- `IStorageProvider` — the low-level backend (`UploadAsync`, `DownloadAsync`, `DeleteAsync`,
  `ExistsAsync`, `GetPresignedUrlAsync`). This module registers the implementation; anyone can
  inject the interface.
- `UploadFileCommand` — the multipart upload command `StorageController` sends over Wolverine.

## Cross-Module Relationships

- **Branding** injects `IStorageProvider` directly (`ClientBrandingService`,
  `ClientBrandingController`) to mint a download URL for `LogoStorageKey`. It reaches the backend,
  not this module's application layer, and that is legal only because the interface sits in
  `Shared.Contracts`.
- **Identity** intends to upload through this module but does not yet —
  `OrganizationService.cs` carries a `TODO` naming `UploadFileCommand`.
- **Publishes nothing.** The four domain events in `StorageEvents.cs` (`FileUploadedEvent`,
  `FileDeletedEvent`, `BucketCreatedEvent`, `BucketDeletedEvent`) are raised on the aggregates and
  asserted in unit tests, but nothing consumes them and there are no Storage entries in
  `Shared.Contracts/…/Events`. Do not assume a subscriber exists.
- **Consumes no** integration events from other modules.

## Important Patterns

- **Two upload paths with different guarantees.** `POST /upload` (multipart) runs
  `UploadFileValidator` over the request stream — magic-byte/content-type agreement for five
  types, a blocked-signature check (MZ, `<html`, `<!doctype`, `<svg`), path-traversal rejection —
  then scans inline via `IFileScanner`, then writes, then creates the row as
  `FileStatus.Available`. `POST /presigned-upload` never sees the bytes, so
  `GetUploadPresignedUrlValidator` can only check the metadata: the row is created
  `PendingValidation` and an async `ScanUploadedFileCommand` is meant to promote it. See
  **Things to Watch** — that promotion is currently broken.
- **`FileNameSanitizer` runs on the multipart path only.** `UploadFileHandler` sanitizes before
  deriving the extension and storing `FileName`; `GetUploadPresignedUrlHandler` takes
  `query.FileName` raw.
- **Storage key** — `tenant-{tenantId}/{bucketName}/{path}/{fileId}{extension}`. `BuildStorageKey`
  is duplicated **verbatim** in `UploadFileHandler` and `GetUploadPresignedUrlHandler`; change
  both or neither.
- **Presigned expiry is clamped, never trusted.** `PresignedUrlOptions.MaxUploadExpiryMinutes` /
  `MaxDownloadExpiryMinutes` cap whatever the caller asked for. Download defaults to one hour.
- **Downloads require `FileStatus.Available`.** `GetPresignedUrlHandler` fails with
  `File.NotAvailable` for anything else, which is what makes the `PendingValidation` bug below
  fatal rather than cosmetic.
- **Provider and scanner are chosen at registration time, from configuration.**
  `Storage:Provider` selects `S3StorageProvider` (scoped, with a singleton `IAmazonS3`) or
  `LocalStorageProvider` (singleton, the default). `Storage:ClamAv:Enabled` selects
  `ClamAvFileScanner` plus a TCP health check tagged `clamav`, or `NoOpFileScanner`. Both are
  switches in `StorageInfrastructureExtensions`, not runtime strategy lookups.
- **`LocalStorageProvider` guards path traversal itself** — `GetFilePath` resolves the key against
  `BasePath` and throws `InvalidOperationException` if it escapes.
- **Handler shapes are mixed.** Most are `public sealed class` with a primary constructor;
  `ScanUploadedFileHandler` is a `public static partial class` using Wolverine method injection
  and `[LoggerMessage]` source-gen logging.

## Permissions

| Permission | Used By |
|------------|---------|
| `StorageWrite` | Create/delete bucket, upload, delete file, presigned upload, and all tenant-scoped settings endpoints |
| `StorageRead` | Get bucket, get file metadata, download, list files, presigned download |

`GET /config` and the three `/settings/user` endpoints carry `[Authorize]` only — a signed-in user
manages their own overrides without a Storage permission.

## Database

- Schema: `storage` (the one copy of the name is `StorageModule.Schema`, `internal const`)
- Context: `StorageDbContext` (extends `TenantAwareDbContext`), registered via
  `AddPooledDbContextFactory` + `AddTenantAwareScopedContext<StorageDbContext>`
- Reads go through `AddReadDbContext<StorageDbContext>`; there is no Dapper here
- **Both** aggregates declare `ITenantScoped`, so `ApplyTenantQueryFilters` covers `StorageBucket`
  as well as `StoredFile`. The README used to say buckets were platform-wide; they are not
- Settings storage is registered with `AddSettings<StorageDbContext, StorageSettingKeys>("storage")`,
  which is why both settings services are resolved with `[FromKeyedServices("storage")]`

## Testing

```bash
./scripts/run-tests.sh storage
```

That excludes the Testcontainers-backed repository suites under
`Wallow.Storage.Tests/Integration/`; add `integration` (`./scripts/run-tests.sh storage integration`)
to run those, which needs Docker.

## Things to Watch

- **The presigned-upload path's scan never succeeds** (`Wallow-p9p4`).
  `GetUploadPresignedUrlHandler` publishes `ScanUploadedFileCommand` *before* it mints the upload
  URL, so `ScanUploadedFileHandler` calls `DownloadAsync` on a key that does not exist yet. Both
  providers throw and the handler catches nothing, so the file is stranded `PendingValidation` and
  can never be downloaded. Do not document this path as working.
- **`LocalStorageProvider`'s presigned URLs 404** (`Wallow-p23n`). It returns `/api/storage/...`
  paths; the API serves `/v1/storage/...` and has no key-addressed endpoint at all.
- **The three `StorageSettingKeys` are read/written by the settings API and enforced nowhere**
  (`Wallow-cf33`). Upload limits actually come from `[RequestSizeLimit(100MB)]` and the bucket's
  own `MaxFileSizeBytes`; there is no extension allowlist and no quota accounting.
- **Five test classes exist twice**, flat and in a nested namespace (`Wallow-xku9`). Add a fact to
  the nested copy — it is the superset in every pair.
- **Orphaned objects are not swept** (`Wallow-41it`): a write that reaches the backend but fails
  before `SaveChangesAsync` leaves the object behind.

## Related Documentation

- Module reference: [`README.md`](README.md)
- Backend conventions and commands: [`api/CLAUDE.md`](../../../CLAUDE.md)
