# Identity Module

## Overview

The Identity module owns authentication, authorization, multi-tenancy, and user/organization management for the Foundry platform. All authentication is delegated to **Keycloak** (external identity provider) - this module contains zero custom auth logic (no password hashing, no token generation, no login endpoints).

The module provides:

- Keycloak admin API wrappers for user and organization CRUD
- Role-based access control (RBAC) via permission-based authorization
- Tenant resolution from JWT organization claims
- Multi-tenancy support enabling tenant isolation across all modules
- Enterprise SSO federation (SAML 2.0 and OIDC)
- SCIM 2.0 provisioning for enterprise identity provider integration
- OAuth2 service accounts for machine-to-machine authentication
- API key management for simple service-to-service authentication

## Key Features

### Authentication
- **Keycloak OIDC**: JWT Bearer validation via Keycloak's JWKS endpoint
- **Token Proxy**: `/api/auth/token` endpoint proxies to Keycloak, hiding IdP configuration from clients
- **SignalR Support**: Query string token authentication for WebSocket connections

### Authorization
- **Permission-Based RBAC**: Granular permissions across Users, Roles, Billing, Organizations, and Admin
- **Role Expansion**: Keycloak roles are expanded to permissions at request time via `PermissionExpansionMiddleware`
- **Three Role Tiers**: `admin` (all permissions), `manager` (subset), `user` (basic access)

### Multi-Tenancy
- **Keycloak Organizations**: Single realm, multiple organizations (Keycloak 26+ feature)
- **JWT-Based Resolution**: Tenant extracted from `organization` claim in JWT
- **Admin Override**: Superadmins can impersonate tenants via `X-Tenant-Id` header

### Enterprise SSO
- **SAML 2.0**: Support for enterprise SAML identity providers
- **OIDC**: Support for enterprise OIDC providers (Okta, Azure AD, etc.)
- **User Provisioning**: Auto-create users on first SSO login with configurable default roles
- **Group Sync**: Optionally sync IdP groups as Keycloak roles

### SCIM Provisioning
- **SCIM 2.0 Compliant**: RFC 7644 implementation for enterprise provisioning
- **User Lifecycle**: Create, update, deactivate, delete users via SCIM
- **Group Management**: SCIM group resources mapped to Keycloak groups
- **Sync Logging**: Full audit trail of all SCIM operations

### Service Accounts
- **OAuth2 Client Credentials**: Machine-to-machine authentication via Keycloak
- **Scope-Based Permissions**: Fine-grained API access via OAuth2 scopes
- **Secret Rotation**: Rotate client secrets without downtime
- **Last-Used Tracking**: Monitor service account activity

## Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  Controllers    │────>│  Keycloak       │────>│  Keycloak       │
│  (Direct calls) │     │  Services       │     │  Admin API      │
└─────────────────┘     └─────────────────┘     └─────────────────┘
         │
         ▼
┌─────────────────┐     ┌─────────────────┐
│  Integration    │     │  Identity       │
│  Events (MQ)    │     │  DbContext      │
└─────────────────┘     └─────────────────┘
```

**Key architectural differences from other modules:**

- Controllers call Keycloak services directly (not through Wolverine CQRS handlers)
- User and organization data lives in Keycloak, not in the module's database
- The module's `IdentityDbContext` stores only local metadata (service accounts, SSO config, SCIM config, API scopes)
- Services publish integration events after Keycloak Admin API calls succeed

### Middleware Pipeline

The following middleware executes in strict order:

1. **Authentication** - JWT Bearer validation via Keycloak OIDC
2. **TenantResolutionMiddleware** - Parses JWT `organization` claim, sets `ITenantContext`
3. **PermissionExpansionMiddleware** - Expands Keycloak roles to granular permission claims
4. **Authorization** - ASP.NET Core policy-based authorization with `[HasPermission]` attribute

### Clean Architecture Layers

```
┌────────────────────────────────────────────────────────────────────┐
│ Api (Foundry.Identity.Api)                                         │
│ - Controllers, Request/Response contracts                          │
│ - Module registration via AddIdentityModule()                      │
└────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────┐
│ Application (Foundry.Identity.Application)                         │
│ - Interfaces (IUserManagementService, ISsoService, IScimService)    │
│ - DTOs (UserDto, OrganizationDto, SsoConfigurationDto)             │
│ - Commands/Queries for Service Accounts                            │
└────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────┐
│ Infrastructure (Foundry.Identity.Infrastructure)                   │
│ - Keycloak service implementations                                 │
│ - Authorization pipeline (HasPermissionAttribute, handlers)        │
│ - Middleware (TenantResolution, PermissionExpansion)               │
│ - EF Core DbContext for local metadata                             │
│ - SCIM filter parser and Keycloak translator                       │
└────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────┐
│ Domain (Foundry.Identity.Domain)                                   │
│ - Entities: ApiScope, ServiceAccountMetadata, SsoConfiguration,   │
│   ScimConfiguration, ScimSyncLog                                   │
│ - Enums: PermissionType, SsoProtocol, SsoStatus, ServiceAccountStatus │
│ - Domain Events: SsoConfigurationActivatedEvent, ScimSyncCompletedEvent │
└────────────────────────────────────────────────────────────────────┘
```

## Domain Entities

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
Local reference to a Keycloak service account client. Stores metadata for tenant-specific queries and usage tracking.

| Property | Type | Description |
|----------|------|-------------|
| TenantId | TenantId | Owning tenant |
| KeycloakClientId | string | Keycloak client ID |
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
| SamlEntityId/SsoUrl/etc. | string? | SAML-specific configuration |
| OidcIssuer/ClientId/etc. | string? | OIDC-specific configuration |
| EnforceForAllUsers | bool | Require SSO for all tenant users |
| AutoProvisionUsers | bool | Create users on first SSO login |
| SyncGroupsAsRoles | bool | Map IdP groups to Keycloak roles |

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

| Property | Type | Description |
|----------|------|-------------|
| TenantId | TenantId | Owning tenant |
| Operation | ScimOperation | Create, Update, Delete, etc. |
| ResourceType | ScimResourceType | User or Group |
| ExternalId | string | IdP's identifier |
| InternalId | string? | Keycloak's identifier |
| Success | bool | Operation result |
| ErrorMessage | string? | Error details if failed |

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

The module uses a hybrid approach - most operations call Keycloak services directly from controllers, but service account management uses CQRS:

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
| `UserRegisteredEvent` | User created via Keycloak Admin API | Communications (welcome email) |
| `UserRoleChangedEvent` | Role assigned/removed | Communications |
| `OrganizationCreatedEvent` | Organization created in Keycloak | Billing (setup subscription) |
| `OrganizationMemberAddedEvent` | Member added to organization | Communications |
| `PasswordResetRequestedEvent` | Password reset initiated | Communications |

## Integration Events Consumed

None - Identity is a source module, not a consumer.

## API Endpoints

### Authentication (`/api/auth`)

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| POST | `/token` | Get access token (proxies to Keycloak) | No |
| POST | `/refresh` | Refresh access token | No |

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
  "Keycloak": {
    "realm": "foundry",
    "auth-server-url": "http://localhost:8080/",
    "ssl-required": "none",
    "resource": "foundry-api",
    "verify-token-audience": true,
    "credentials": {
      "secret": "your-client-secret"
    }
  },
  "ConnectionStrings": {
    "Identity": "Host=localhost;Database=foundry;Username=postgres;Password=postgres"
  }
}
```

### Keycloak Setup

1. **Keycloak 26+** - Organizations feature required
2. **Realm**: `foundry` with Organizations enabled
3. **Clients**:
   - `foundry-api` (confidential) - API backend with service account
   - `foundry-spa` (public) - Frontend SPA with PKCE
4. **Realm Roles**: `admin`, `manager`, `user`
5. **Service Account**: `foundry-api` client needs `manage-users` and `manage-organizations` roles

## Dependencies on Other Modules

The Identity module depends only on shared infrastructure:

| Dependency | Purpose |
|------------|---------|
| Foundry.Shared.Kernel | `ITenantContext`, `TenantId`, base entity types |
| Foundry.Shared.Contracts | Integration event definitions |

## Other Modules Depending on Identity

All modules depend on Identity's infrastructure:

- **TenantContext**: Resolved by `TenantResolutionMiddleware` for tenant-scoped queries
- **Permission Claims**: Expanded by `PermissionExpansionMiddleware` for authorization
- **Integration Events**: Consumed by Communications (welcome emails, notifications), Billing (org setup)

## Adding a New Permission

1. Add to `PermissionType` enum in Domain layer
2. Update `RolePermissionMapping` in Infrastructure layer
3. If scope-based, add to `PermissionExpansionMiddleware.MapScopeToPermission()`
4. Apply `[HasPermission(PermissionType.NewPermission)]` to controller actions

## EF Core Migrations

```bash
# Create a new migration
dotnet ef migrations add MigrationName \
    --project src/Modules/Identity/Foundry.Identity.Infrastructure \
    --startup-project src/Foundry.Api \
    --context IdentityDbContext

# Apply migrations
dotnet ef database update \
    --project src/Modules/Identity/Foundry.Identity.Infrastructure \
    --startup-project src/Foundry.Api \
    --context IdentityDbContext
```

## Libraries

| Package | Purpose |
|---------|---------|
| Keycloak.AuthServices.Authentication | OIDC JWT validation |
| Keycloak.AuthServices.Sdk | Admin API client |
| Microsoft.EntityFrameworkCore | Local metadata storage |
| Npgsql.EntityFrameworkCore.PostgreSQL | PostgreSQL provider |
| StackExchange.Redis | API key caching |
