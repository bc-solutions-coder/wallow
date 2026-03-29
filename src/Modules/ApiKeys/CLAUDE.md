# ApiKeys Module -- Agent Guide

## Module Purpose

Manages API keys for service-to-service authentication. Dual-writes to PostgreSQL and Valkey (Redis). The `IApiKeyService` interface lives in `Wallow.Shared.Contracts` so other modules can validate keys without referencing this module directly.

## Key File Locations

| File | Purpose |
|------|---------|
| `Domain/Entities/ApiKey.cs` | Aggregate with `Create` and `Revoke` methods |
| `Domain/ApiKeys/ApiKeyId.cs` | Strongly-typed ID |
| `Application/Interfaces/IApiKeyRepository.cs` | Repository contract |
| `Infrastructure/Services/RedisApiKeyService.cs` | Core service -- key creation, validation, listing, revocation |
| `Infrastructure/Services/IRedisDatabase.cs` | Abstraction over `StackExchange.Redis.IDatabase` for testability |
| `Infrastructure/Authorization/ApiKeyAuthenticationMiddleware.cs` | Middleware that authenticates `X-Api-Key` header requests |
| `Infrastructure/Persistence/ApiKeysDbContext.cs` | EF Core context, schema `apikeys` |
| `Infrastructure/Persistence/Configurations/ApiKeyConfiguration.cs` | EF Core entity configuration |
| `Infrastructure/Extensions/ApiKeysModuleExtensions.cs` | Module registration and auto-migration |
| `Api/Controllers/ApiKeysController.cs` | REST endpoints at `api/v{version}/identity/auth/keys` |

## Shared Contracts

The `IApiKeyService` interface and its DTOs (`ApiKeyCreateResult`, `ApiKeyValidationResult`, `ApiKeyMetadata`) are defined in `src/Shared/Wallow.Shared.Contracts/ApiKeys/IApiKeyService.cs`. Scope validation uses `ApiScopes` and `ScopePermissionMapper` from `Wallow.Shared.Contracts.Identity` and `Wallow.Shared.Kernel.Identity.Authorization`.

## Cross-Module Relationships

- **Identity module**: Provides `IScopeSubsetValidator` used by the controller to validate service account scope escalation
- **Shared.Kernel**: Provides `ClaimsPrincipalExtensions` (use `GetPermissions()`, `GetClientId()`, etc. -- never raw `FindFirst`)
- **No Wolverine events**: This module does not currently publish or consume Wolverine in-memory messages

## Patterns and Conventions

- **Dual-write**: `RedisApiKeyService` writes to PostgreSQL first (via `IApiKeyRepository`), then Valkey. Validation reads from Valkey first, falls back to PostgreSQL on cache miss and repopulates the cache.
- **Key format**: `sk_live_<base64url secret>`. The SHA-256 hash is stored; the plaintext key is never persisted.
- **Redis key layout**: `apikey:{hash}` for validation, `apikey:id:{keyId}` for metadata, `apikeys:user:{userId}` as a set of key IDs.
- **Logging**: Uses `[LoggerMessage]` source generator pattern with `partial` classes. Log methods are defined in a separate partial class declaration at the bottom of the file.
- **Tenant isolation**: `ApiKeysDbContext` extends `TenantAwareDbContext` with automatic query filters. Repository methods take `tenantId` as a parameter.
- **No CQRS handlers**: The controller calls `IApiKeyService` directly rather than dispatching commands/queries through Wolverine.

## Testing

```bash
./scripts/run-tests.sh apikeys
```

Test files are in `tests/Modules/ApiKeys/Wallow.ApiKeys.Tests/` covering domain logic, controller behavior, middleware, Redis service, and EF Core configuration.

## Things to Watch

- The `IRedisDatabase` interface is a thin wrapper for testability -- any new Redis operations need to be added to both `IRedisDatabase` and `RedisDatabaseWrapper`.
- `GetByHashAsync` with `Guid.Empty` tenantId is used during validation to search across all tenants (cache miss path).
- Scope validation in the controller has two layers: permission-based (all users) and scope-subset (service accounts only, identified by `sa-` prefix on `clientId`).
- Per-user key count is tracked via Redis set length, not a database query.
