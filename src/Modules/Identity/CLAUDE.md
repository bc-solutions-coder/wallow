# Identity Module

## Module Responsibility

Owns authentication, authorization, multi-tenancy, user/organization management, service accounts, SSO federation, and SCIM provisioning. Authentication is handled by OpenIddict (embedded OAuth 2.0 / OpenID Connect server) backed by ASP.NET Core Identity for user/role storage. The module provides user/org CRUD, RBAC via permission-based authorization, tenant resolution from JWT `org_id` claims, service account lifecycle management (OpenIddict client credentials), developer app registration, SSO configuration (OIDC/SAML), and SCIM directory sync.

## Layer Rules

- **Domain** (`Wallow.Identity.Domain`): Entities (`WallowUser`, `WallowRole`, `Organization`, `ServiceAccountMetadata`, `ApiScope`, `SsoConfiguration`, `ScimConfiguration`, `ScimSyncLog`), strongly-typed IDs, enums (`ServiceAccountStatus`, `SsoProtocol`, `SsoStatus`, `SamlNameIdFormat`, `ScimOperation`, `ScimResourceType`), domain events (`SsoConfigurationActivatedEvent`, `ScimSyncCompletedEvent`). Domain depends only on `Shared.Kernel`.
- **Application** (`Wallow.Identity.Application`): Defines `IUserManagementService`, `IOrganizationService`, `IServiceAccountService`, `IDeveloperAppService` interfaces, plus DTOs. Service accounts, SSO, and SCIM use proper command/query handlers.
- **Infrastructure** (`Wallow.Identity.Infrastructure`): `IdentityDbContext` (EF Core, `identity` schema) inherits `IdentityDbContext<WallowUser, WallowRole, Guid>` and integrates OpenIddict EF Core stores. Implements ASP.NET Core Identity services, OpenIddict service account and developer app services (`OpenIddictServiceAccountService`, `OpenIddictDeveloperAppService`), authorization pipeline (`HasPermissionAttribute`, `PermissionAuthorizationHandler`, `PermissionExpansionMiddleware`, `RolePermissionLookup`), `TenantResolutionMiddleware`, `ServiceAccountTrackingMiddleware`. SSO secrets encrypted via `IDataProtectionProvider`.
- **Api** (`Wallow.Identity.Api`): Controllers for Users, Organizations, Roles, Service Accounts, SSO, SCIM, Clients, Apps, API Keys. Auth endpoints: `AuthorizationController` (authorize), `TokenController` (token), `LogoutController` (end-session), `AuthController` (login/register views). `ClientsController` provides admin CRUD for OpenIddict applications.

## Key Patterns

- **OpenIddict OIDC**: Embedded OAuth 2.0 / OpenID Connect server. Supports authorization code flow (with PKCE), client credentials, and refresh tokens. Endpoints: `/connect/authorize`, `/connect/token`, `/connect/logout`, `/connect/userinfo`. Development uses auto-generated encryption/signing certificates.
- **ASP.NET Core Identity**: `WallowUser` and `WallowRole` stored in the `identity` PostgreSQL schema. Password hashing, email confirmation, and token generation handled by Identity framework.
- **Tenant resolution**: `TenantResolutionMiddleware` reads the `org_id` claim from JWT (flat GUID string, e.g. `"org_id": "d4f8a..."`) and the `org_name` claim, then populates `ITenantContext`. Admins and operator service accounts (client ID prefixed `sa-`) can override via `X-Tenant-Id` header.
- **Permission-based authorization**: `PermissionExpansionMiddleware` reads roles from JWT, expands them to granular `PermissionType` claims via `RolePermissionLookup`. Controllers use `[HasPermission(PermissionType.X)]` attribute. Three role tiers: admin (all permissions), manager (subset), user (basic).
- **Service accounts**: Implemented as OpenIddict client credentials applications via `OpenIddictServiceAccountService`. Client IDs prefixed with `sa-`.
- **Developer apps**: Implemented as OpenIddict authorization code applications via `OpenIddictDeveloperAppService`. Client IDs prefixed with `app-`.
- **ClientsController**: Admin-only CRUD API (`/api/v1/identity/clients`) for managing OpenIddict applications directly. Supports create, update, delete, and secret rotation.
- **API scope seeding**: `ApiScopeSeeder` seeds default scopes (billing, identity, storage, communications, etc.) at startup. Idempotent.

## Dependencies

- **Depends on**: `Wallow.Shared.Kernel` (for `ITenantContext`, `TenantId`, base types), `OpenIddict` packages, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`.
- **Depended on by**: `Wallow.Api` (registers module, uses middleware). All other modules depend on `ITenantContext` (from Shared.Kernel, resolved by Identity's middleware). Identity publishes `UserRegisteredEvent`, `UserRoleChangedEvent`, `OrganizationCreatedEvent`, etc. via `Shared.Contracts`.

## Constraints

- Do not add external identity providers. OpenIddict and ASP.NET Core Identity own all authentication, token generation, and user storage.
- Do not reference other modules directly. Publish integration events through `Shared.Contracts` for cross-module communication.
- `RolePermissionLookup` is the single source of truth for role-to-permission expansion. Update it when adding new permissions.
- The middleware registration order in `Program.cs` (Authentication -> TenantResolution -> PermissionExpansion -> Authorization) is critical. Do not reorder.
- This module uses the `identity` PostgreSQL schema.
