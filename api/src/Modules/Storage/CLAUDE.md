# Storage module

## Upload lifecycle

- **Two upload paths, different guarantees.** `POST /upload` (multipart) validates the bytes
  (magic-byte/content-type agreement, blocked signatures, path traversal), scans inline via
  `IFileScanner`, writes, and creates the row `Available`. `POST /presigned-upload` never sees
  the bytes: the row is created `PendingValidation`, and after PUTting the bytes the client MUST
  call `POST /files/{id}/complete` — it verifies the object exists, scans inline, and marks the
  row `Available` or `Rejected`. Completion is idempotent: once the file leaves
  `PendingValidation` it reports current status without rescanning. Byte-level validation is
  multipart-only.
- **No background sweep promotes or expires abandoned presigned rows** — a presigned upload that
  never calls `/complete` stays `PendingValidation` forever, and downloads stay blocked
  (`GetPresignedUrlHandler` requires `Available`).
- `FileNameSanitizer` runs on the multipart path only — `GetUploadPresignedUrlHandler` takes
  `query.FileName` raw.
- `BuildStorageKey` (`tenant-{tenantId}/{bucketName}/{path}/{fileId}{extension}`) is duplicated
  **verbatim** in `UploadFileHandler` and `GetUploadPresignedUrlHandler` — change both or neither.
- Presigned expiry is clamped by `PresignedUrlOptions.Max{Upload,Download}ExpiryMinutes`, never
  trusted from the caller.

## Local presigned URLs

- `LocalPresignedUrlSigner` lives in **Application** because the Api layer may not reference
  Infrastructure. It holds a random per-process HMAC key, so local presigned URLs die on API
  restart (dev-only provider, URLs live minutes — accepted).
- The signature IS the authorization: `LocalStorageController` is `[AllowAnonymous]`, S3-style.
  Do not "fix" a 403 there by adding auth or loosening validation.
- The controller is `[ApiExplorerSettings(IgnoreApi = true)]` — deliberately absent from the
  OpenAPI document and SDK; callers receive the URL as an opaque string.

## Tenant limits

- Both upload handlers resolve `IStorageLimitsProvider` — built on
  `ITenantSettingRepository<StorageDbContext>`, deliberately NOT the keyed `ISettingsService`
  (Wolverine codegen cannot construct keyed services). Settings register via
  `AddSettings<StorageDbContext, StorageSettingKeys>("storage")`, which is why the settings
  services resolve with `[FromKeyedServices("storage")]`.
- Checks in order: max upload size, extension allowlist (`*` default = allow all), then quota.
  Quota counts **every** `StoredFile` row — `PendingValidation` reservations and `Rejected` rows
  included.
- User-scope setting overrides are ignored on purpose — a user must not raise their own limits.

## Other invariants

- **Publishes nothing.** The domain events in `StorageEvents.cs` have no subscribers, and there
  are no Storage entries in `Shared.Contracts/…/Events` — do not assume a consumer exists.
- **Buckets are tenant-scoped, not platform-wide** — both aggregates declare `ITenantScoped`.
- **The orphan sweep only reaches the DEFAULT S3 bucket.** `OrphanedObjectSweepJob` runs in a
  background scope where `ITenantContext` is unresolved, so `S3StorageProvider.ResolveBucket()`
  falls back to `BucketName` — a multi-region `RegionBuckets` deployment's non-primary buckets
  are never swept.
