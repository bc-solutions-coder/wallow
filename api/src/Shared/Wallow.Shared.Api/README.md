# Wallow.Shared.Api

Utilities shared by module Api projects and the host.

Three files in total:

- `ApiHealthCheck` — the shared health-check implementation
- `Extensions/ResultExtensions.cs` — `ToActionResult()` over `Result` / `Result<T>`, mapping
  failures to RFC 7807 problem details
- `Settings/SettingUpdateRequest.cs` — the `(Key, Value)` request record that each module's own
  settings controller binds

See [`../README.md`](../README.md) for the full shared-library index.
