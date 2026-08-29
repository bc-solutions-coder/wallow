# ApiKeys Module — Agent Guide

## Module Purpose

API keys for service-to-service authentication. Dual-writes to PostgreSQL and Valkey
(Redis-compatible; type names keep the `Redis` prefix because they wrap `StackExchange.Redis`).
`IApiKeyService` lives in `Wallow.Shared.Contracts` so other modules validate keys without
referencing this module.

## Patterns and Conventions

- **No CQRS/Wolverine**: `ApiKeysController` (endpoints at `/v1/identity/auth/keys`) calls
  `IApiKeyService` directly. The module publishes and consumes no Wolverine messages.
- **Dual-write**: `RedisApiKeyService` writes PostgreSQL first (via `IApiKeyRepository`), then
  Valkey. Validation reads Valkey first, falls back to PostgreSQL on cache miss and repopulates.
- **Key format**: `sk_live_<base64url secret>`. Only the SHA-256 hash is stored; plaintext is
  never persisted.
- **Valkey key layout**: `apikey:{hash}` (validation), `apikey:id:{keyId}` (metadata),
  `apikeys:user:{userId}` (set of key IDs).
- **`X-Api-Key` requests** authenticate through
  `Infrastructure/Authorization/ApiKeyAuthenticationMiddleware.cs`.
- Scope validation uses `ApiScopes` + `ScopePermissionMapper` from Shared; Identity provides
  `IScopeSubsetValidator`, used by the controller to validate service-account scope escalation.

## Things to Watch

- `IRedisDatabase` is a thin testability wrapper over `StackExchange.Redis.IDatabase` — new
  Valkey operations must be added to both `IRedisDatabase` and `RedisDatabaseWrapper`.
- `GetByHashAsync` with `Guid.Empty` tenantId searches across all tenants (cache-miss
  validation path).
- Scope validation in the controller has two layers: permission-based (all users) and
  scope-subset (service accounts only, identified by the `sa-` prefix on `clientId`).
- Per-user key count is tracked via Valkey set length, not a database query.

## Database

Schema: `apikeys`; context: `ApiKeysDbContext`. Repository methods take `tenantId` as a parameter.

## Testing

`./scripts/run-tests.sh apikeys`
