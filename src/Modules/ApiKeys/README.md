# ApiKeys Module

## Overview

The ApiKeys module provides API key management for service-to-service authentication. Users create API keys scoped to their tenant, which can then be used to authenticate requests via the `X-Api-Key` header as an alternative to JWT tokens.

Keys are generated with a `sk_live_` prefix, hashed with SHA-256 before storage, and dual-written to PostgreSQL (persistence) and Valkey/Redis (fast validation lookups). The plaintext key is returned only once at creation time.

## Architecture

```
src/Modules/ApiKeys/
+-- Wallow.ApiKeys.Domain           # ApiKey entity, ApiKeyId strongly-typed ID
+-- Wallow.ApiKeys.Application      # IApiKeyRepository interface
+-- Wallow.ApiKeys.Infrastructure   # EF Core persistence, Redis service, auth middleware
+-- Wallow.ApiKeys.Api              # Controller, request/response contracts
```

**Database Schema**: `apikeys` (PostgreSQL)
**Cache**: Valkey (Redis-compatible) for key validation and metadata lookups

## Domain

### ApiKey (Entity)

Represents a hashed API key bound to a service account within a tenant.

**Properties**: `ServiceAccountId`, `HashedKey`, `DisplayName`, `Scopes` (JSONB), `ExpiresAt`, `IsRevoked`

**Operations**:
- `ApiKey.Create(...)` -- factory method with validation
- `ApiKey.Revoke(...)` -- marks key as permanently revoked

## Authentication Flow

1. `ApiKeyAuthenticationMiddleware` checks for the `X-Api-Key` header on incoming requests
2. If present, the key is validated via `RedisApiKeyService` (Valkey first, PostgreSQL fallback)
3. On success, a `ClaimsPrincipal` is created with the key's user, tenant, and scope claims
4. The tenant context is set, and the request proceeds
5. If no API key header is found, the request falls through to JWT authentication

## API Endpoints

All endpoints require authentication and `ApiKeyManage` permission.

**Route**: `api/v{version}/identity/auth/keys`

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/` | Create a new API key |
| `GET` | `/` | List all API keys for the current user |
| `DELETE` | `/{keyId}` | Revoke an API key |

## Key Behaviors

- **Key format**: `sk_live_<base64url-encoded random bytes>`
- **Scope validation**: Requested scopes are validated against `ApiScopes.ValidScopes` and the user's permissions; service accounts cannot escalate beyond their own scopes
- **Per-user limit**: Configurable via `ApiKeys:MaxPerUser` (default 10)
- **Expiration**: Optional; keys without expiration never expire
- **Last used tracking**: Updated in Valkey on each successful validation (fire-and-forget)

## Configuration

Uses the shared `DefaultConnection` connection string and the shared Valkey/Redis `IConnectionMultiplexer`. Auto-migrates its schema in Development and Testing environments.

| Setting | Default | Purpose |
|---------|---------|---------|
| `ApiKeys:MaxPerUser` | `10` | Maximum API keys per user |

## Dependencies

| Project | Purpose |
|---------|---------|
| `Wallow.Shared.Kernel` | Base entities, strongly-typed IDs, multi-tenancy, `ClaimsPrincipalExtensions` |
| `Wallow.Shared.Contracts` | `IApiKeyService` interface, `ApiScopes`, `ScopePermissionMapper` |
| `Wallow.Shared.Infrastructure.Core` | `TenantAwareDbContext`, shared persistence utilities |

## Testing

```bash
./scripts/run-tests.sh apikeys
```

## EF Core Migrations

```bash
dotnet ef migrations add MigrationName \
    --project src/Modules/ApiKeys/Wallow.ApiKeys.Infrastructure \
    --startup-project src/Wallow.Api \
    --context ApiKeysDbContext
```
