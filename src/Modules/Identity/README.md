# Identity Module

## Overview

The Identity module owns authentication, authorization, multi-tenancy, and user/organization management for the Wallow platform. Authentication is handled by **OpenIddict** (embedded OAuth 2.0 / OpenID Connect server) backed by **ASP.NET Core Identity** for user/role storage.

The module provides:

- OpenIddict OIDC server with authorization code flow (PKCE), client credentials, and refresh tokens
- ASP.NET Core Identity for user/role storage and password management
- Role-based access control (RBAC) via permission-based authorization
- Tenant resolution from JWT `org_id` claims
- Multi-tenancy support enabling tenant isolation across all modules
- Service account lifecycle management (OpenIddict client credentials)
- Developer app registration (OpenIddict authorization code applications)
- Enterprise SSO federation (SAML 2.0 and OIDC)
- SCIM 2.0 provisioning for enterprise identity provider integration
- API key management for simple service-to-service authentication

## Key Features

### Authentication
- **OpenIddict OIDC**: Embedded OAuth 2.0 / OpenID Connect server supporting authorization code flow (with PKCE), client credentials, and refresh tokens
- **Endpoints**: `/connect/authorize`, `/connect/token`, `/connect/logout`, `/connect/userinfo`
- **ASP.NET Core Identity**: `WallowUser` and `WallowRole` stored in the `identity` PostgreSQL schema. Password hashing, email confirmation, and token generation handled by Identity framework
- **SignalR Support**: Query string token authentication for WebSocket connections

### Authorization
- **Permission-Based RBAC**: Granular permissions across Users, Roles, Billing, Organizations, and Admin
- **Role Expansion**: Roles are expanded to permissions at request time via `PermissionExpansionMiddleware`
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

### Enterprise SSO
- **SAML 2.0**: Support for enterprise SAML identity providers
- **OIDC**: Support for enterprise OIDC providers (Okta, Azure AD, etc.)
- **User Provisioning**: Auto-create users on first SSO login with configurable default roles
- **SSO Secrets**: Encrypted via `IDataProtectionProvider`

### SCIM Provisioning
- **SCIM 2.0 Compliant**: RFC 7644 implementation for enterprise provisioning
- **User Lifecycle**: Create, update, deactivate, delete users via SCIM
- **Group Management**: SCIM group resources mapped to roles
- **Sync Logging**: Full audit trail of all SCIM operations

## Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  Controllers    │────>│  Application    │────>│  OpenIddict     │
│  (Auth, Users,  │     │  Services       │     │  + ASP.NET Core │
│   Orgs, etc.)   │     │                 │     │  Identity       │
└─────────────────┘     └─────────────────┘     └─────────────────┘
         │
         ▼
┌─────────────────┐     ┌─────────────────┐
│  Integration    │     │  Identity       │
│  Events (MQ)    │     │  DbContext      │
└─────────────────┘     └─────────────────┘
```

### Clean Architecture Layers

```
┌────────────────────────────────────────────────────────────────────┐
│ Api (Wallow.Identity.Api)                                         │
│ - Controllers: Users, Organizations, Roles, Service Accounts,     │
│   SSO, SCIM, Clients, Apps, API Keys                              │
│ - Auth: AuthorizationController, TokenController, LogoutController│
│ - Module registration via AddIdentityModule()                     │
└────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────┐
│ Application (Wallow.Identity.Application)                         │
│ - Interfaces (IUserManagementService, IOrganizationService,       │
│   IServiceAccountService, IDeveloperAppService)                   │
│ - DTOs (UserDto, OrganizationDto, SsoConfigurationDto)            │
│ - Commands/Queries for Service Accounts, SSO, SCIM               │
└────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────┐
│ Infrastructure (Wallow.Identity.Infrastructure)                   │
│ - IdentityDbContext (identity schema, inherits                    │
│   IdentityDbContext<WallowUser, WallowRole, Guid>)               │
│ - OpenIddict EF Core stores integration                           │
│ - OpenIddictServiceAccountService, OpenIddictDeveloperAppService │
│ - Authorization pipeline (HasPermissionAttribute, handlers)       │
│ - Middleware (TenantResolution, PermissionExpansion,              │
│   ServiceAccountTracking)                                         │
└────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────┐
│ Domain (Wallow.Identity.Domain)                                   │
│ - Entities: WallowUser, WallowRole, Organization, ApiScope,      │
│   ServiceAccountMetadata, SsoConfiguration, ScimConfiguration,   │
│   ScimSyncLog                                                     │
│ - Enums: PermissionType, SsoProtocol, SsoStatus,                 │
│   ServiceAccountStatus, SamlNameIdFormat, ScimOperation,         │
│   ScimResourceType                                                │
│ - Domain Events: SsoConfigurationActivatedEvent,                 │
│   ScimSyncCompletedEvent                                          │
└────────────────────────────────────────────────────────────────────┘
```

### Middleware Pipeline

The following middleware executes in strict order:

1. **Authentication** - JWT Bearer validation via OpenIddict
2. **TenantResolutionMiddleware** - Parses JWT `org_id` claim, sets `ITenantContext`
3. **PermissionExpansionMiddleware** - Expands roles to granular permission claims via `RolePermissionLookup`
4. **Authorization** - ASP.NET Core policy-based authorization with `[HasPermission]` attribute

## Domain Entities

### WallowUser
ASP.NET Core Identity user entity, stored in the `identity` PostgreSQL schema.

### WallowRole
ASP.NET Core Identity role entity, stored in the `identity` PostgreSQL schema.

### Organization
Represents a tenant organization.

### ApiScope
System-defined OAuth2 scopes that can be assigned to service accounts. Scopes map to permissions for client credentials flow.

| Property | Type | Description |
|----------|------|-------------|
| Code | string | Unique scope code (e.g., "invoices.read") |
| DisplayName | string | Human-readable name |
| Category | string | Category for UI grouping (e.g., "Billing") |
| Description | string? | What this scope grants access to |
| IsDefault | bool | Included by default for new service accounts |

### ServiceAccountMetadata
Local reference to an OpenIddict service account application. Stores metadata for tenant-specific queries and usage tracking.

| Property | Type | Description |
|----------|------|-------------|
| TenantId | TenantId | Owning tenant |
| Name | string | Human-readable name |
| Status | ServiceAccountStatus | Active or Revoked |
| Scopes | List&lt;string&gt; | Assigned OAuth2 scopes |
| LastUsedAt | DateTime? | Last API call timestamp |

### SsoConfiguration
Enterprise SSO identity provider configuration for a tenant.

| Property | Type | Description |
|----------|------|-------------|
| TenantId | TenantId | Owning tenant |
| Protocol | SsoProtocol | SAML or OIDC |
| Status | SsoStatus | Draft, Testing, Active, or Disabled |
| DisplayName | string | Name shown to users |
| EnforceForAllUsers | bool | Require SSO for all tenant users |
| AutoProvisionUsers | bool | Create users on first SSO login |
| SyncGroupsAsRoles | bool | Map IdP groups to roles |

### ScimConfiguration
SCIM 2.0 provisioning configuration for a tenant.

| Property | Type | Description |
|----------|------|-------------|
| TenantId | TenantId | Owning tenant |
| IsEnabled | bool | Whether SCIM endpoint is active |
| BearerToken | string | Hashed authentication token |
| TokenPrefix | string | Token prefix for identification |
| AutoActivateUsers | bool | Activate users on creation |
| DeprovisionOnDelete | bool | Delete users on SCIM delete |

### ScimSyncLog
Audit log of SCIM provisioning operations.

## Enums

### PermissionType
40+ granular permissions for RBAC:

```csharp
// User Management (100-103)
UsersRead, UsersCreate, UsersUpdate, UsersDelete

// Role Management (200-203)
RolesRead, RolesCreate, RolesUpdate, RolesDelete

// Billing (500-507)
BillingRead, BillingManage, InvoicesRead, InvoicesWrite,
PaymentsRead, PaymentsWrite, SubscriptionsRead, SubscriptionsWrite

// Organizations (600-603)
OrganizationsRead, OrganizationsCreate, OrganizationsUpdate, OrganizationsManageMembers

// API Keys (700-703)
ApiKeysRead, ApiKeysCreate, ApiKeysUpdate, ApiKeysDelete

// Webhooks (850)
WebhooksManage

// SSO/SCIM (860-862)
SsoRead, SsoManage, ScimManage

// Admin (900-901)
AdminAccess, SystemSettings
```

### Role-Permission Mapping

| Role | Permissions |
|------|-------------|
| `admin` | All permissions |
| `manager` | UsersRead, BillingRead, OrganizationsRead, OrganizationsManageMembers, ApiKeys*, SsoRead |
| `user` | OrganizationsRead |

## Commands and Queries

Service account management uses CQRS:

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

## Domain Events

Events raised within the module:

| Event | When Raised |
|-------|-------------|
| `SsoConfigurationActivatedEvent` | SSO configuration is activated for a tenant |
| `ScimSyncCompletedEvent` | A SCIM provisioning operation completes |

## Integration Events Published

Events published to RabbitMQ for cross-module communication:

| Event | Trigger | Consumers |
|-------|---------|-----------|
| `UserRegisteredEvent` | User created | Communications (welcome email) |
| `UserRoleChangedEvent` | Role assigned/removed | Communications |
| `OrganizationCreatedEvent` | Organization created | Billing (setup subscription) |
| `OrganizationMemberAddedEvent` | Member added to organization | Communications |
| `PasswordResetRequestedEvent` | Password reset initiated | Communications |

## Integration Events Consumed

None - Identity is a source module, not a consumer.

## API Endpoints

### Auth (`/connect`)

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| GET/POST | `/authorize` | OAuth 2.0 authorization endpoint | No |
| POST | `/token` | OAuth 2.0 token endpoint | No |
| GET/POST | `/logout` | End-session endpoint | No |
| GET/POST | `/userinfo` | OpenID Connect userinfo | Yes |

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

### SSO (`/api/sso`)

| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | Get SSO configuration | SsoRead |
| POST | `/saml` | Configure SAML SSO | SsoManage |
| POST | `/oidc` | Configure OIDC SSO | SsoManage |
| POST | `/test` | Test SSO connection | SsoManage |
| POST | `/activate` | Activate SSO | SsoManage |
| POST | `/disable` | Disable SSO | SsoManage |
| GET | `/saml/metadata` | Get SAML SP metadata XML | SsoRead |
| GET | `/oidc/callback-info` | Get OIDC callback URLs | SsoRead |
| POST | `/validate` | Validate IdP configuration | SsoManage |

### SCIM (`/scim/v2`)

SCIM 2.0 endpoints use Bearer token authentication (not OAuth):

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/Users` | List users |
| GET | `/Users/{id}` | Get user |
| POST | `/Users` | Create user |
| PUT | `/Users/{id}` | Replace user |
| PATCH | `/Users/{id}` | Partial update user |
| DELETE | `/Users/{id}` | Delete user |
| GET | `/Groups` | List groups |
| POST | `/Groups` | Create group |
| PUT | `/Groups/{id}` | Replace group |
| DELETE | `/Groups/{id}` | Delete group |
| GET | `/ServiceProviderConfig` | SCIM capabilities |
| GET | `/Schemas` | SCIM schemas |
| GET | `/ResourceTypes` | Available resource types |

## Configuration Requirements

### appsettings.json

```json
{
  "ConnectionStrings": {
    "Identity": "Host=localhost;Database=wallow;Username=postgres;Password=postgres"
  }
}
```

OpenIddict configuration (encryption/signing certificates, client registrations) is managed through `IdentityDbContext` and seeded at startup via `ApiScopeSeeder`. Development uses auto-generated encryption/signing certificates.

## Dependencies on Other Modules

The Identity module depends only on shared infrastructure:

| Dependency | Purpose |
|------------|---------|
| Wallow.Shared.Kernel | `ITenantContext`, `TenantId`, base entity types |
| Wallow.Shared.Contracts | Integration event definitions |

## Other Modules Depending on Identity

All modules depend on Identity's infrastructure:

- **TenantContext**: Resolved by `TenantResolutionMiddleware` for tenant-scoped queries
- **Permission Claims**: Expanded by `PermissionExpansionMiddleware` for authorization
- **Integration Events**: Consumed by Communications (welcome emails, notifications), Billing (org setup)

## Adding a New Permission

1. Add to `PermissionType` enum in Domain layer
2. Update `RolePermissionLookup` in Infrastructure layer
3. If scope-based, add to `PermissionExpansionMiddleware.MapScopeToPermission()`
4. Apply `[HasPermission(PermissionType.NewPermission)]` to controller actions

## EF Core Migrations

```bash
# Create a new migration
dotnet ef migrations add MigrationName \
    --project src/Modules/Identity/Wallow.Identity.Infrastructure \
    --startup-project src/Wallow.Api \
    --context IdentityDbContext

# Apply migrations
dotnet ef database update \
    --project src/Modules/Identity/Wallow.Identity.Infrastructure \
    --startup-project src/Wallow.Api \
    --context IdentityDbContext
```

## Libraries

| Package | Purpose |
|---------|---------|
| OpenIddict | OAuth 2.0 / OpenID Connect server |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | User/role storage |
| Microsoft.EntityFrameworkCore | Database access |
| Npgsql.EntityFrameworkCore.PostgreSQL | PostgreSQL provider |
| StackExchange.Redis | API key caching |
