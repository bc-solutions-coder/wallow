# Identity Module — Agent Guide

## What This Module Does

The auth core: users, organizations (the Organization IS the tenant), roles/permissions,
API scopes, and the **OpenIddict OIDC server** (authorization code + PKCE, client
credentials, refresh tokens — see `IdentityInfrastructureExtensions`). Also: MFA
(TOTP + lockout + backup codes + per-org MFA policy), passwordless login (magic link
and OTP via `PasswordlessService`), invitations, sessions, service accounts, and
first-run setup/bootstrap-admin.

## Key File Locations

| Area | Path |
|------|------|
| Aggregates/entities | `Wallow.Identity.Domain/Entities/` (WallowUser, WallowRole, Organization, OrganizationBranding, OrganizationSettings, Membership, MembershipRole, Invitation, ActiveSession, ApiScope, ServiceAccountMetadata) |
| Controllers | `Wallow.Identity.Api/Controllers/` (Account, Authorization, Token, Userinfo, Logout, Mfa, Me, Users, Roles, Scopes, Organizations, Invitations, Session, Clients, Apps, Setup, IdentitySettings, TestSupport) |
| Services (most logic) | `Wallow.Identity.Infrastructure/Services/` (UserService, MfaService, PasswordlessService, SessionService, OrganizationService, InvitationService, BootstrapAdminService, PreRegisteredClientSyncService, …) |
| Commands/Queries | `Wallow.Identity.Application/Commands/`, `Queries/` (service accounts, bootstrap admin, setup status) |
| OpenIddict setup | `Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs` |
| DbContext | `Wallow.Identity.Infrastructure/Persistence/IdentityDbContext.cs` (schema: `identity`) |
| Integration events | `src/Shared/Wallow.Shared.Contracts/Identity/Events/` |
| Tests | `tests/Modules/Identity/Wallow.Identity.Tests/` + `Wallow.Identity.IntegrationTests/` |

## Conventions and Patterns

- **Service-heavy, thin CQRS**: unlike Inquiries, most logic lives in Infrastructure
  services called from controllers; Wolverine commands/queries exist mainly for service
  accounts and setup/bootstrap.
- **OIDC flows**: authorization code **with PKCE required**, client credentials, and
  refresh tokens only — no password or device flow.
- **Passwordless**: magic-link tokens are signed via Data Protection's persisted key
  ring and stored under Valkey key prefixes (`pwdless:magic:`); OTP is the same service.
- **MFA**: `MfaService` (TOTP enroll/verify, backup codes), `MfaLockoutService`,
  `MfaPartialAuthService` (step-up state), `OrganizationMfaPolicyService`,
  `MfaExemptionChecker`.
- **Tenancy**: `Organization.Create` mints the tenant id (org.Id == TenantId);
  `PreRegisteredClientSyncService`/seeding must go through it, never mint ids ad hoc.
- **TestSupportController** exists for test-only endpoints — do not use it as a
  pattern for production features.

## Cross-Module Communication

- **Publishes** (via Wolverine, defined in `Shared.Contracts/Identity/Events/`):
  user lifecycle (EmailVerified, PasswordChanged, UserLoginSucceeded/Failed,
  UserAccountLockedOut, UserEmailChanged…), passwordless (MagicLinkRequested,
  OtpCodeRequested), MFA (UserMfaBackupCodesRegenerated…), organization lifecycle
  (OrganizationCreated/Archived/Deleted/Reactivated, member add/remove, settings),
  and InvitationCreated.
- **Consumes**: nothing — there is no `EventHandlers/` directory in the Application layer.

## Permissions

`Wallow.Shared.Kernel.Identity.Authorization.PermissionType` declares **39** `public const string`
permissions across all modules. The ones this module enforces most: `UsersRead/Create/Update/Delete`,
`RolesRead/Create/Update/Delete`, `OrganizationsRead/Create/Update/ManageMembers`, `ScopeRead`, plus
`AdminAccess`/`SystemSettings`. **Read `PermissionType.cs` for the full list** — this is a sample,
not an inventory. Enforced with `[HasPermission]`; scopes map to permissions via
`ScopePermissionMapper` (also Kernel).

## Database

- Schema: `identity` (`HasDefaultSchema` in `IdentityDbContext`)
- Extends `TenantAwareDbContext`; default `NoTracking`; migrated inline only in the `Testing`
  environment — everywhere else `Wallow.MigrationService` applies migrations

## Running Tests

```bash
./scripts/run-tests.sh identity      # Wallow.Identity.Tests (unit + Testcontainers)

# `integration` is no longer this module's shorthand -- it selects Category=Integration across the
# whole solution (seven assemblies). Every spec in Wallow.Identity.IntegrationTests carries that
# category, so narrowing to it takes the project path AND the tier; without the tier the default
# filter excludes every spec in the project and the run now fails on zero tests.
./scripts/run-tests.sh api/tests/Modules/Identity/Wallow.Identity.IntegrationTests integration
```

## Related Documentation

- Module reference: [`README.md`](README.md)
- Backend conventions and commands: [`api/CLAUDE.md`](../../../CLAUDE.md)
