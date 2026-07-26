# Identity Module

## Overview

The Identity module owns authentication, authorization, multi-tenancy, and user/organization management. Authentication is handled by **OpenIddict** (embedded OAuth 2.0 / OpenID Connect server) backed by **ASP.NET Core Identity** for user/role storage.

The module provides:

- OpenIddict OIDC server with authorization code flow (PKCE), client credentials, and refresh tokens
- ASP.NET Core Identity for user/role storage and password management
- Permission-based RBAC via role expansion
- Tenant resolution from JWT `org_id` claims
- Multi-tenancy support enabling tenant isolation across all modules
- Service account lifecycle management (OpenIddict client credentials)
- Developer app registration (OpenIddict authorization code applications)
- API key management for simple service-to-service authentication

## Key Features

### Authentication
- **OpenIddict OIDC**: Authorization code flow (with PKCE), client credentials, and refresh tokens
- **Endpoints**: `/connect/authorize`, `/connect/token`, `/connect/logout`, `/connect/userinfo`
- **ASP.NET Core Identity**: `WallowUser` and `WallowRole` stored in the `identity` PostgreSQL schema
- **SignalR Support**: Query string token authentication for WebSocket connections

### Authorization
- **Permission-Based RBAC**: Granular permissions across Users, Roles, Billing, Organizations, and Admin
- **Role Expansion**: Roles expanded to permissions at request time via `PermissionExpansionMiddleware`
- **Three Role Tiers**: `admin` (all permissions), `manager` (subset), `user` (basic access)
- **RolePermissionLookup**: Single source of truth for role-to-permission expansion

### Multi-Tenancy
- **JWT-Based Resolution**: Tenant extracted from `org_id` claim in JWT
- **Admin Override**: Superadmins and operator service accounts (client ID prefixed `sa-`) can override via `X-Tenant-Id` header

### Service Accounts
- **OpenIddict Client Credentials**: Machine-to-machine authentication via OpenIddict applications
- **Client ID Prefix**: Service account client IDs prefixed with `sa-`
- **Scope-Based Permissions**: Fine-grained API access via OAuth2 scopes
- **Secret Rotation**: Rotate client secrets without downtime
- **Last-Used Tracking**: Monitor service account activity via `ServiceAccountTrackingMiddleware`

### Developer Apps
- **OpenIddict Authorization Code**: Developer applications via `OpenIddictDeveloperAppService`
- **Client ID Prefix**: Developer app client IDs prefixed with `app-`

## Architecture

### Clean Architecture Layers

```
src/Modules/Identity/
+-- Wallow.Identity.Domain         # Entities, Enums, Domain Events
+-- Wallow.Identity.Application    # Interfaces, DTOs, Commands, Queries
+-- Wallow.Identity.Infrastructure # DbContext, OpenIddict, Authorization, Middleware
+-- Wallow.Identity.Api            # Controllers, Auth endpoints, Module registration
```

**Database Schema**: `identity` (PostgreSQL)

### Middleware Pipeline

The following middleware executes in strict order:

1. **Authentication** - JWT Bearer validation via OpenIddict
2. **TenantResolutionMiddleware** - Parses JWT `org_id` claim, sets `ITenantContext`
3. **PermissionExpansionMiddleware** - Expands roles to granular permission claims via `RolePermissionLookup`
4. **Authorization** - ASP.NET Core policy-based authorization with `[HasPermission]` attribute

## Domain Entities

- **WallowUser** - ASP.NET Core Identity user entity
- **WallowRole** - ASP.NET Core Identity role entity
- **Organization** - Represents a tenant organization
- **ApiScope** - System-defined OAuth2 scopes assignable to service accounts
- **ServiceAccountMetadata** - Local reference to an OpenIddict service account application with usage tracking

## Commands and Queries

### Service Account Commands

| Command | Description |
|---------|-------------|
| `CreateServiceAccountCommand` | Create a new OAuth2 service account |
| `RotateServiceAccountSecretCommand` | Generate a new client secret |
| `RevokeServiceAccountCommand` | Revoke and delete a service account |
| `UpdateServiceAccountScopesCommand` | Update assigned OAuth2 scopes |

### Service Account Queries

| Query | Description |
|-------|-------------|
| `GetServiceAccountsQuery` | List all service accounts for the tenant |
| `GetServiceAccountQuery` | Get a specific service account by ID |
| `GetApiScopesQuery` | List available API scopes |

## Integration Events Published

Events published via Wolverine for cross-module communication:

| Event | Trigger |
|-------|---------|
| `UserRegisteredEvent` | User created |
| `UserRoleChangedEvent` | Role assigned/removed |
| `OrganizationCreatedEvent` | Organization created |
| `OrganizationMemberAddedEvent` | Member added to organization |
| `PasswordResetRequestedEvent` | Password reset initiated |

## Integration Events Consumed

None. Identity is a source module, not a consumer.

## API Endpoints

### Auth (`/connect`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET/POST | `/authorize` | OAuth 2.0 authorization endpoint |
| POST | `/token` | OAuth 2.0 token endpoint |
| GET/POST | `/logout` | End-session endpoint |
| GET/POST | `/userinfo` | OpenID Connect userinfo |

### Users (`/api/users`)

| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List users with search/pagination | UsersRead |
| GET | `/{id}` | Get user by ID | UsersRead |
| GET | `/me` | Get current user's profile | Authenticated |
| POST | `/` | Create new user | UsersCreate |
| POST | `/{id}/deactivate` | Deactivate user | UsersUpdate |
| POST | `/{id}/activate` | Activate user | UsersUpdate |
| POST | `/{userId}/roles` | Assign role to user | RolesUpdate |
| DELETE | `/{userId}/roles/{roleName}` | Remove role from user | RolesUpdate |

### Organizations (`/api/organizations`)

| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| POST | `/` | Create organization | OrganizationsCreate |
| GET | `/` | List organizations | OrganizationsRead |
| GET | `/{id}` | Get organization by ID | OrganizationsRead |
| GET | `/{id}/members` | List organization members | OrganizationsRead |
| POST | `/{id}/members` | Add member | OrganizationsManageMembers |
| DELETE | `/{id}/members/{userId}` | Remove member | OrganizationsManageMembers |
| GET | `/mine` | Get current user's organizations | Authenticated |

### Roles (`/api/roles`)

| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List all roles | RolesRead |
| GET | `/{roleName}/permissions` | Get permissions for role | RolesRead |

### Clients (`/api/v1/identity/clients`)

| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List OpenIddict applications | AdminAccess |
| POST | `/` | Create application | AdminAccess |
| PUT | `/{id}` | Update application | AdminAccess |
| DELETE | `/{id}` | Delete application | AdminAccess |
| POST | `/{id}/rotate-secret` | Rotate client secret | AdminAccess |

### Service Accounts (`/api/service-accounts`)

| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List service accounts | ApiKeysRead |
| POST | `/` | Create service account (returns secret once) | ApiKeysCreate |
| GET | `/{id}` | Get service account by ID | ApiKeysRead |
| PUT | `/{id}/scopes` | Update scopes | ApiKeysUpdate |
| POST | `/{id}/rotate-secret` | Rotate client secret | ApiKeysUpdate |
| DELETE | `/{id}` | Revoke service account | ApiKeysDelete |

### API Keys (`/api/auth/keys`)

| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| POST | `/` | Create API key | Authenticated |
| GET | `/` | List user's API keys | Authenticated |
| DELETE | `/{keyId}` | Revoke API key | Authenticated |

### API Scopes (`/api/scopes`)

| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List available scopes | Authenticated |

## Configuration

OpenIddict configuration (encryption/signing certificates, client registrations) is managed through `IdentityDbContext` and seeded at startup via `ApiScopeSeeder`. Development uses auto-generated encryption/signing certificates.

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Wallow.Shared.Kernel` | `ITenantContext`, `TenantId`, base entity types |
| `Wallow.Shared.Contracts` | Integration event definitions |

## Adding a New Permission

1. Add to `PermissionType` enum in Domain layer
2. Update `RolePermissionLookup` in Infrastructure layer
3. If scope-based, add to `PermissionExpansionMiddleware.MapScopeToPermission()`
4. Apply `[HasPermission(PermissionType.NewPermission)]` to controller actions

## Testing

```bash
./scripts/run-tests.sh identity
```

## EF Core Migrations

```bash
dotnet ef migrations add MigrationName \
    --project src/Modules/Identity/Wallow.Identity.Infrastructure \
    --startup-project src/Wallow.Api \
    --context IdentityDbContext
```
