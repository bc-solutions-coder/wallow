# Identity Module

## Module Responsibility

Owns authentication, authorization, multi-tenancy, user/organization management, service accounts, SSO federation, and SCIM provisioning. Authentication is delegated to Keycloak (external IdP) -- this module contains no custom auth logic (no password hashing, no token generation, no login endpoints). It provides Keycloak admin API wrappers for user/org CRUD, RBAC via permission-based authorization, tenant resolution from JWT organization claims, service account lifecycle management, SSO configuration (OIDC/SAML), and SCIM directory sync.

## Layer Rules

- **Domain** (`Foundry.Identity.Domain`): Entities (`ServiceAccountMetadata`, `ApiScope`, `SsoConfiguration`, `ScimConfiguration`, `ScimSyncLog`), strongly-typed IDs (`ServiceAccountMetadataId`, `ApiScopeId`, `SsoConfigurationId`, `ScimConfigurationId`, `ScimSyncLogId`), enums (`ServiceAccountStatus`, `SsoProtocol`, `SsoStatus`, `SamlNameIdFormat`, `ScimOperation`, `ScimResourceType`), domain events (`SsoConfigurationActivatedEvent`, `ScimSyncCompletedEvent`). Domain depends only on `Shared.Kernel`.
- **Application** (`Foundry.Identity.Application`): Defines `IUserManagementService` and `IOrganizationService` interfaces, plus DTOs. Keycloak proxy operations (user/org CRUD) are called directly from controllers -- no CQRS handlers. Service accounts, SSO, and SCIM DO use proper command/query handlers.
- **Infrastructure** (`Foundry.Identity.Infrastructure`): `IdentityDbContext` (EF Core, `identity` schema) with `ServiceAccountMetadata`, `ApiScopes`, `SsoConfigurations`, `ScimConfigurations`, `ScimSyncLogs` tables. Implements Keycloak admin services, authorization pipeline (`HasPermissionAttribute`, `PermissionAuthorizationHandler`, `PermissionExpansionMiddleware`, `RolePermissionMapping`), `TenantResolutionMiddleware`, `ServiceAccountTrackingMiddleware`. Uses `Keycloak.AuthServices.Sdk` for admin API. SSO secrets encrypted via `IDataProtectionProvider`.
- **Api** (`Foundry.Identity.Api`): Controllers for Users, Organizations, Roles, Service Accounts, SSO, SCIM. Registers Keycloak OIDC JWT Bearer authentication (including SignalR query string token support).

## Key Patterns

- **Keycloak OIDC**: JWT Bearer validation configured via `Keycloak.AuthServices.Authentication`. Token issued by Keycloak, validated by ASP.NET middleware.
- **Tenant resolution**: `TenantResolutionMiddleware` reads the `organization` claim from JWT (Keycloak 26+ JSON format: `{"orgId": {"name": "orgName"}}`), parses the org GUID, and populates `ITenantContext`. Admins can override via `X-Tenant-Id` header.
- **Permission-based authorization**: `PermissionExpansionMiddleware` reads Keycloak roles from JWT, expands them to granular `PermissionType` claims via `RolePermissionMapping`. Controllers use `[HasPermission(PermissionType.X)]` attribute. Three role tiers: admin (all permissions), manager (subset), user (basic).
- **Why no CQRS for Keycloak ops**: Identity intentionally skips Wolverine CQRS for Keycloak proxy operations -- no domain logic, no local state, just CRUD wrappers around the Keycloak Admin API. Service accounts, SSO, and SCIM have local state and use proper handlers.

## Dependencies

- **Depends on**: `Foundry.Shared.Kernel` (for `ITenantContext`, `TenantId`, base types), `Keycloak.AuthServices.*` packages.
- **Depended on by**: `Foundry.Api` (registers module, uses middleware). All other modules depend on `ITenantContext` (from Shared.Kernel, resolved by Identity's middleware). Identity publishes `UserRegisteredEvent`, `UserRoleChangedEvent`, `OrganizationCreatedEvent`, etc. via `Shared.Contracts`.

## Constraints

- Do not add custom authentication (login/register endpoints, password handling, JWT generation). Keycloak owns all of that.
- Do not reference other modules directly. Publish integration events through `Shared.Contracts` for cross-module communication.
- `RolePermissionMapping` is the single source of truth for role-to-permission expansion. Update it when adding new permissions.
- The middleware registration order in `Program.cs` (Authentication -> TenantResolution -> PermissionExpansion -> Authorization) is critical. Do not reorder.
- This module uses the `identity` PostgreSQL schema.
