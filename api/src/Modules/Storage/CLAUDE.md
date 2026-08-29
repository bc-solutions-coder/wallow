# Storage Module — Agent Guide

## Module Purpose

Tenant-isolated file storage: buckets, uploads, metadata, presigned URLs and virus scanning,
over a pluggable backend (local filesystem or any S3-compatible service).

## Non-Obvious Locations

- **Presigned URL signer** is `Wallow.Storage.Application/Services/LocalPresignedUrlSigner.cs` —
  in Application because the Api layer may not reference Infrastructure.
- Settings registry: `Wallow.Storage.Application/Settings/StorageSettingKeys.cs`.
- Backends: `Wallow.Storage.Infrastructure/Providers/` (`LocalStorageProvider`,
  `S3StorageProvider`); scanners: `Infrastructure/Scanning/` (`ClamAvFileScanner`,
  `NoOpFileScanner`); background job: `Infrastructure/Jobs/OrphanedObjectSweepJob.cs`.

## Shared Contracts

Unusually for a module, Storage's outward surface is an **interface plus a command record**, not
an event. Both live in `Wallow.Shared.Contracts/Storage/`:

- `IStorageProvider` — the low-level backend (`UploadAsync`, `DownloadAsync`, `DeleteAsync`,
  `ExistsAsync`, `ListAsync`, `GetPresignedUrlAsync`). This module registers the implementation;
  anyone can inject the interface. `ListAsync` yields `StorageObjectInfo`, prefix-filtered.
- `UploadFileCommand` — the multipart upload command `StorageController` sends over Wolverine.

## Cross-Module Relationships

- **Branding** injects `IStorageProvider` directly to mint a download URL for `LogoStorageKey` —
  legal only because the interface sits in `Shared.Contracts`.
- **Publishes nothing.** The domain events in `StorageEvents.cs` are raised on the aggregates and
  asserted in unit tests, but nothing consumes them and there are no Storage entries in
  `Shared.Contracts/…/Events`. Do not assume a subscriber exists.
- **Consumes no** integration events from other modules.

## Important Patterns

- **Two upload paths with different guarantees.** `POST /upload` (multipart) runs
  `UploadFileValidator` over the request stream (magic-byte/content-type agreement,
  blocked-signature check, path-traversal rejection), scans inline via `IFileScanner`, writes,
  then creates the row as `FileStatus.Available`. `POST /presigned-upload` never sees the bytes,
  so its validator checks metadata only: the row is created `PendingValidation`, and after PUTting
  the bytes the client MUST call `POST /files/{id}/complete` — `CompletePresignedUploadHandler`
  verifies the object exists, scans it inline, and marks the row `Available` or `Rejected`. The
  completion call is idempotent: once the file leaves `PendingValidation` it reports current
  status without rescanning. Byte-level validation applies only to the multipart path.
- **`FileNameSanitizer` runs on the multipart path only** — `GetUploadPresignedUrlHandler` takes
  `query.FileName` raw.
- **Storage key** — `tenant-{tenantId}/{bucketName}/{path}/{fileId}{extension}`. `BuildStorageKey`
  is duplicated **verbatim** in `UploadFileHandler` and `GetUploadPresignedUrlHandler`; change
  both or neither.
- **Presigned expiry is clamped, never trusted** — `PresignedUrlOptions.MaxUploadExpiryMinutes` /
  `MaxDownloadExpiryMinutes` cap whatever the caller asks. Download defaults to one hour.
- **Downloads require `FileStatus.Available`** — `GetPresignedUrlHandler` fails with
  `File.NotAvailable` for anything else.
- **Provider and scanner are chosen at registration time, from configuration.** `Storage:Provider`
  selects `S3StorageProvider` (scoped, singleton `IAmazonS3`) or `LocalStorageProvider`
  (singleton, the default). `Storage:ClamAv:Enabled` selects `ClamAvFileScanner` (plus a TCP
  health check tagged `clamav`) or `NoOpFileScanner`. Switches in
  `StorageInfrastructureExtensions`, not runtime strategy lookups.
- **Local presigned URLs are served by `LocalStorageController`.** The provider mints
  `/v1/storage/local/files?key=...&expires=...&sig=...` — an HMAC (from the singleton
  `LocalPresignedUrlSigner`, registered under every provider) over method + key + expiry,
  GET-signed for downloads and PUT-signed for uploads. The controller is `[AllowAnonymous]` (the
  signature IS the authorization, S3-style) and `[ApiExplorerSettings(IgnoreApi = true)]` —
  deliberately absent from the OpenAPI document and SDK; callers receive the URL as an opaque string.
- **`LocalStorageProvider` guards path traversal itself** — `GetFilePath` throws if a key escapes
  `BasePath`.

## Permissions

| Permission | Used By |
|------------|---------|
| `StorageWrite` | Create/delete bucket, upload, delete file, presigned upload, tenant-scoped settings endpoints |
| `StorageRead` | Get bucket, get file metadata, download, list files, presigned download |

`GET /config` and the `/settings/user` endpoints carry `[Authorize]` only — a signed-in user
manages their own overrides without a Storage permission.

## Database

- Schema: `storage` (the one copy of the name is `StorageModule.Schema`, `internal const`).
- Context: `StorageDbContext`, registered via `AddPooledDbContextFactory` +
  `AddTenantAwareScopedContext<StorageDbContext>`; reads via `AddReadDbContext<StorageDbContext>`.
- **Both** aggregates declare `ITenantScoped` — buckets are tenant-scoped, not platform-wide.
- Settings storage registers with `AddSettings<StorageDbContext, StorageSettingKeys>("storage")`,
  which is why both settings services resolve with `[FromKeyedServices("storage")]`.

## Things to Watch

- **A presigned upload that never calls `/complete` stays `PendingValidation` forever.** No
  background sweep promotes or expires abandoned presigned rows; the completion endpoint is the
  only promotion path, and downloads stay blocked until it runs.
- **Local presigned URLs die on API restart.** `LocalPresignedUrlSigner` holds a random
  per-process HMAC key (dev-only provider, URLs live minutes — accepted). The signature is the
  *entire* authorization on `LocalStorageController`'s anonymous endpoints; do not "fix" a 403
  there by adding auth or loosening validation.
- **Tenant limits are enforced from tenant-scope settings only.** Both upload handlers resolve
  `IStorageLimitsProvider` (built on `ITenantSettingRepository<StorageDbContext>` — deliberately
  NOT the keyed `ISettingsService`, which Wolverine codegen cannot construct) and check, in order:
  max upload size, extension allowlist (`*` default = allow all), then quota. Quota counts
  **every** `StoredFile` row — `PendingValidation` reservations and `Rejected` rows included.
  User-scope setting overrides are ignored on purpose: a user must not raise their own limits.
  `[RequestSizeLimit(100MB)]` and the bucket's `MaxFileSizeBytes` still apply on top.
- **Some test classes exist twice**, flat and in a nested namespace — add a fact to the nested
  copy; it is the superset in every pair.
- **The orphan sweep only reaches the DEFAULT S3 bucket.** `OrphanedObjectSweepJob` (daily,
  registered in `Program.cs` behind the Storage module flag) deletes `tenant-`-prefixed objects
  with no matching `StoredFile.StorageKey`, skipping anything younger than 24 h. It runs in a
  background scope where `ITenantContext` is unresolved, so `S3StorageProvider.ResolveBucket()`
  falls back to `BucketName` — a multi-region `RegionBuckets` deployment's non-primary buckets are
  never swept.

## Testing

`./scripts/run-tests.sh storage` (add `integration` for the Testcontainers suites).
