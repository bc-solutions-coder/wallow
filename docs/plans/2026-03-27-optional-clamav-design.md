# Optional ClamAV Virus Scanning

**Date:** 2026-03-27
**Status:** Approved

## Problem

ClamAV is always-on with no way to disable it. When ClamAV is unreachable, file uploads fail with a SocketException. CI doesn't need it, and most development workflows don't either. Forks shouldn't be forced to run ClamAV.

## Design

### Configuration

New `ClamAv` sub-section under `Storage` in `StorageOptions`:

```csharp
public sealed class ClamAvOptions
{
    public bool Enabled { get; set; } = false;  // disabled by default
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3310;
}
```

`StorageOptions` replaces the flat `ClamAvHost`/`ClamAvPort` with a `ClamAv` property of type `ClamAvOptions`.

**appsettings.json example:**
```json
"Storage": {
  "ClamAv": {
    "Enabled": true,
    "Host": "clamav",
    "Port": 3310
  }
}
```

**Environment variables:** `Storage__ClamAv__Enabled=true`, `Storage__ClamAv__Host=clamav`, `Storage__ClamAv__Port=3310`

### DI Registration

In `StorageInfrastructureExtensions`, registration becomes conditional:

- **Enabled:** Register `ClamAvFileScanner` (scoped) + ClamAV health check
- **Disabled:** Register `NoOpFileScanner` (singleton) — returns `FileScanResult.Clean()` with debug-level log

`NoOpFileScanner` lives in `Wallow.Storage.Infrastructure/Scanning/`. Handlers don't change — they always receive an `IFileScanner`.

### Docker Compose

Add `profiles: ["clamav"]` to the clamav service in `docker-compose.yml`. Start with:
```bash
docker compose --profile clamav up -d
```

Remove clamav port override from `docker-compose.dev.yml`.

### Tests

- Existing `ClamAvFileScannerTests` unchanged
- Update `StorageExtensionsTests` for both paths (enabled/disabled)
- Add `NoOpFileScannerTests`
- CI works as-is (disabled by default)

### Migration

- `ClamAvHost`/`ClamAvPort` removed from `StorageOptions`, replaced by `ClamAv.Host`/`ClamAv.Port`
- Env vars change from `Storage__ClamAvHost` to `Storage__ClamAv__Host` (plus `Storage__ClamAv__Enabled=true`)
- `.env.production.example` updated with new keys
