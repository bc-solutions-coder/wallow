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
- **Permission-Based RBAC**: Granular permissions across Users, Roles, Organizations, API Keys, and Admin
- **Role Expansion**: Roles expanded to permissions at request time via `PermissionExpansionMiddleware`
- **Three Role Tiers**: `admin` (all permissions), `manager` (subset), `user` (basic access)
- **RolePermissionLookup**: Single source of truth for role-to-permission expansion

### Multi-Tenancy
- **JWT-Based Resolution**: Tenant extracted from `org_id` claim in JWT
- **Admin Override**: Superadmins and operator service accounts (client ID prefixed `sa-`) can override via `X-Tenant-Id` header

### Organization Clients
- **Self-service registration**: an organization admin or manager registers a confidential
  authorization-code application (`kind: application`) or a client-credentials service account
  (`kind: service-account`) on the org-scoped client surface (`OrganizationClientsController`
  / `OrganizationClientService`), gated by `OrganizationClientsManage`
- **Client ID derivation**: `app-<org-slug>-<name-slug>` / `sa-<org-slug>-<name-slug>`; name and
  id are immutable
- **Organization binding**: registration binds both kinds to the organization, so a service
  account's client-credentials token carries `org_id`
- **Service accounts ignore every URI field** and can only use the `client_credentials` grant
- **Last-Used Tracking**: `ServiceAccountTrackingMiddleware` records `sa-`/`app-` client activity
  on the registered-client ledger
- **Registered-client ledger**: one `RegisteredClient` row per OpenIddict application (organization,
  kind, status, created by/at, last used)
- **Redirect URI rule**: absolute, fragment-free, HTTPS or loopback HTTP, enforced on the org
  surface, the admin surface, and seed sync (`ClientUriRules`)
- **Platform-only scopes**: `ApiScope.PlatformOnly` marks scopes an organization's application
  can never be granted

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
- **OrganizationBranding** - Per-organization branding overrides
- **OrganizationSettings** - Per-organization policy and enrollment settings
- **Membership** - A user's membership in an organization, with lifecycle state
- **MembershipRole** - A role assigned within a membership
- **Invitation** - A pending invitation to join an organization
- **ActiveSession** - A tracked sign-in session
- **ApiScope** - System-defined OAuth2 scopes grantable to registered clients
- **RegisteredClient** - An organization's registered application or service account, bound to the OpenIddict application by client id

## Commands and Queries

Organization clients (applications and service accounts) go through `OrganizationClientService`
directly rather than Wolverine messages.

### Scope Queries

| Query | Description |
|-------|-------------|
| `GetApiScopesQuery` | List available API scopes |

## Integration Events Published

Identity publishes roughly thirty integration events via Wolverine, covering user lifecycle,
passwordless, MFA, organization lifecycle, membership transitions, and invitations. The
**canonical catalogue is [`Wallow.Shared.Contracts/README.md`](../../Shared/Wallow.Shared.Contracts/README.md)**;
the records themselves live in `src/Shared/Wallow.Shared.Contracts/Identity/Events/`. This README
deliberately does not restate the list, because a partial copy here has drifted before.

## Integration Events Consumed

None. Identity is a source module, not a consumer.

## API Endpoints

> [!NOTE]
> Routes are served at `/v1/...`. The `/api` prefix only exists when a reverse proxy adds
> it via the opt-in PathBase (`api/src/Wallow.Api/Program.cs`).

The tables below are a guide, not a contract. `packages/sdk/openapi/v1.json` is generated from the
controllers and is the authoritative endpoint list.

### Auth (`/connect`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET/POST | `/authorize` | OAuth 2.0 authorization endpoint |
| POST | `/token` | OAuth 2.0 token endpoint |
| GET/POST | `/logout` | End-session endpoint |
| GET/POST | `/userinfo` | OpenID Connect userinfo |

### Users (`/v1/identity/users`)

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

### Organizations (`/v1/identity/organizations`)

| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| POST | `/` | Create organization | none — any signed-in account, org-less token included |
| GET | `/` | List organizations | OrganizationsRead |
| GET | `/{id}` | Get organization by ID | OrganizationsRead |
| DELETE | `/{id}` | Delete organization | OrganizationsUpdate |
| POST | `/{id}/archive` | Archive organization | OrganizationsUpdate |
| POST | `/{id}/reactivate` | Reactivate organization | OrganizationsUpdate |
| POST | `/{id}/platform-suspension` | Place the platform's suspension on the organization, with a reason: every member's and bound client's tokens are revoked and every change to the organization is refused until it is lifted | Global admin |
| DELETE | `/{id}/platform-suspension` | Lift the organization's platform suspension; people sign in again, and clients the organization suspended itself stay suspended | Global admin |
| POST | `/{id}/leave` | Leave the organization | Authenticated |
| GET | `/{id}/members` | List organization members | OrganizationsRead |
| POST | `/{id}/members` | Add member | OrganizationsManageMembers |
| DELETE | `/{id}/members/{userId}` | Remove member | OrganizationsManageMembers |
| GET | `/{id}/members/pending` | List pending memberships | OrganizationsManageMembers |
| GET | `/{id}/members/suspended` | List suspended memberships | OrganizationsManageMembers |
| GET | `/{id}/members/denied` | List denied memberships | OrganizationsManageMembers |
| POST | `/{id}/members/{userId}/approve` | Approve a pending member | OrganizationsManageMembers |
| POST | `/{id}/members/{userId}/deny` | Deny a pending member | OrganizationsManageMembers |
| DELETE | `/{id}/members/{userId}/denial` | Clear a denial | OrganizationsManageMembers |
| POST | `/{id}/members/{userId}/suspend` | Suspend a member | OrganizationsManageMembers |
| POST | `/{id}/members/{userId}/reinstate` | Reinstate a member | OrganizationsManageMembers |
| PUT | `/{id}/enrollment` | Update enrollment policy | OrganizationsManageMembers |
| GET | `/{id}/branding` | Get organization branding | OrganizationsRead |
| PUT | `/{id}/branding` | Update organization branding | OrganizationsUpdate |
| POST | `/{id}/branding/logo` | Upload branding logo | OrganizationsUpdate |
| GET | `/{id}/settings` | Get organization settings | OrganizationsRead |
| PUT | `/{id}/settings` | Update organization settings | OrganizationsUpdate |

### Me (`/v1/identity/me`)

| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/organizations` | Get current user's organizations | Authenticated |

### Roles (`/v1/identity/roles`)

| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List all roles | RolesRead |
| GET | `/{roleName}/permissions` | Get permissions for role | RolesRead |

### Clients (`/v1/identity/clients`)

`ClientsController` guards each action separately (the class level stays bare so the service-account
actions can carry their own permissions); every
row below — including the service-account actions — requires `AdminAccess`.

| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List OpenIddict applications | AdminAccess |
| GET | `/{id}` | Get application by client ID | AdminAccess |
| POST | `/` | Create application | AdminAccess |
| PUT | `/{id}` | Update application | AdminAccess |
| DELETE | `/{id}` | Delete application | AdminAccess |
| POST | `/{id}/rotate-secret` | Rotate client secret | AdminAccess |

### Organization Clients (`/v1/identity/organizations/{orgId}/clients`)

Every action requires `OrganizationClientsManage` (built-in `admin` and `manager` roles) and
answers 404 to a caller who cannot address the organization — except the two platform-suspension
actions, which require a global admin and answer 403 to everyone else. While an organization is
platform-suspended, every non-read action here and on the organization itself answers 422 for
everyone but global admins.

| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List the organization's registered clients | OrganizationClientsManage |
| POST | `/` | Register an application or service account (returns secret, issuer, API base URL once) | OrganizationClientsManage |
| GET | `/{clientId}` | Get a registered client | OrganizationClientsManage |
| PATCH | `/{clientId}` | Update scopes, and for an application its redirect and logout URIs | OrganizationClientsManage |
| DELETE | `/{clientId}` | Delete a registered client: revokes its tokens, then removes the application, its consents and its branding | OrganizationClientsManage |
| POST | `/{clientId}/rotate-secret` | Rotate the secret (returned once); `revokeActiveTokens` also ends every token the client holds | OrganizationClientsManage |
| POST | `/{clientId}/suspend` | Suspend the client: every token is revoked and its realtime connections closed; authorize shows the auth host's error page and the token endpoint answers `invalid_client` until it is reinstated | OrganizationClientsManage |
| POST | `/{clientId}/reinstate` | Put a suspended client back in service; prior consents still stand | OrganizationClientsManage |
| POST | `/{clientId}/platform-suspension` | Place the platform's suspension on the client, with a reason the organization reads but cannot lift | Global admin |
| DELETE | `/{clientId}/platform-suspension` | Lift the client's platform suspension; the organization's own suspension, if any, still stands | Global admin |

Registration, rotation, suspension, reinstatement and deletion publish `ClientRegisteredEvent` /
`ClientSecretRotatedEvent` / `ClientSuspendedEvent` / `ClientReinstatedEvent` / `ClientDeletedEvent`,
which the auth-audit handler records with the actor, organization, client and caller IP; the Branding
module also listens for `ClientDeletedEvent` to drop the deleted client's branding and logo.
Rotate-with-revoke, suspension, deletion and organization-membership revocation all go through the one
`IAccessRevoker`. Platform suspension and its lift publish `ClientSuspendedByPlatformEvent` /
`ClientReinstatedByPlatformEvent` (and, on the organization, `OrganizationSuspendedByPlatformEvent` /
`OrganizationReinstatedByPlatformEvent`), which the auth-audit handler records with the operator and
— on the suspensions — the stated reason.

### Authorize Context (`/v1/identity/auth/authorize-context`)

| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | The client behind a pending authorize transaction: branding, owning organization, first-party flag and scope descriptions. Takes the transaction's `returnUrl` (plus an optional `scope` override); the embedded `redirect_uri` must exactly match one the client registered, so nothing can be read by client id alone. Every failure is the same 404. | Anonymous (auth rate limit) |

### Service Accounts

Service accounts have no surface of their own: they are registered, listed, updated and deleted
on the Organization Clients routes above with `kind: service-account`. See
`docs/api/service-accounts.md`.

### API Keys (`/v1/identity/auth/keys`)

Owned by the ApiKeys module — see [`../ApiKeys/README.md`](../ApiKeys/README.md). Every action
requires `ApiKeyManage`.

### API Scopes (`/v1/identity/scopes`)

| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List available scopes | ScopeRead |

## Configuration

OpenIddict configuration (encryption/signing certificates, client registrations) is managed through `IdentityDbContext` and seeded at startup via `ApiScopeSeeder`. Development uses auto-generated encryption/signing certificates.

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Wallow.Shared.Kernel` | `ITenantContext`, `TenantId`, base entity types |
| `Wallow.Shared.Contracts` | Integration event definitions |

## Adding a New Permission

`PermissionType` is a `public static class` of `public const string` members in
`Wallow.Shared.Kernel/Identity/Authorization/PermissionType.cs` — not an enum, and not in this
module's Domain layer.

1. Add a `public const string` to `PermissionType` (Kernel)
2. Map it to the roles that should carry it in `RolePermissionMapping` (Kernel).
   `RolePermissionLookup` in Identity Infrastructure is a passthrough to that mapping
3. If scope-based, add the mapping to `ScopePermissionMapper` (Kernel).
   `PermissionExpansionMiddleware` only calls it
4. Apply `[HasPermission(PermissionType.NewPermission)]` to controller actions

## Testing

```bash
./scripts/run-tests.sh identity      # Wallow.Identity.Tests (unit + Testcontainers)

# `integration` selects Category=Integration solution-wide, not just this module. To run only this
# module's integration suite, pass its path plus the tier -- the tier is required, because the
# default filter excludes Category=Integration and a zero-test run is now a failure.
./scripts/run-tests.sh api/tests/Modules/Identity/Wallow.Identity.IntegrationTests integration
```

## EF Core Migrations

```bash
dotnet ef migrations add MigrationName \
    --project api/src/Modules/Identity/Wallow.Identity.Infrastructure \
    --startup-project api/src/Wallow.Api \
    --context IdentityDbContext
```

## Related Documentation

- Agent guide for this module: [`CLAUDE.md`](CLAUDE.md)
- Backend conventions and commands: [`api/CLAUDE.md`](../../../CLAUDE.md)
- Integration event catalogue: [`Wallow.Shared.Contracts/README.md`](../../Shared/Wallow.Shared.Contracts/README.md)
