# Identity Module — Agent Guide

## What This Module Does

The auth core: users, organizations (the Organization IS the tenant), roles/permissions,
API scopes, and the **OpenIddict OIDC server** (authorization code + PKCE, client
credentials, refresh tokens — see `IdentityInfrastructureExtensions`). Also: MFA
(TOTP + lockout + backup codes + per-org MFA policy), passwordless login (magic link
and OTP via `PasswordlessService`), invitations, sessions, service accounts, and
first-run setup/bootstrap-admin.

## Conventions and Patterns

- **Service-heavy, thin CQRS**: unlike Inquiries, most logic lives in Infrastructure services
  (`Wallow.Identity.Infrastructure/Services/` — UserService, MfaService, PasswordlessService,
  SessionService, OrganizationService, InvitationService, BootstrapAdminService, …) called from
  controllers; Wolverine commands/queries exist mainly for service accounts and setup/bootstrap.
- **OIDC flows**: authorization code **with PKCE required**, client credentials, and
  refresh tokens only — no password or device flow.
- **Passwordless**: magic-link tokens are signed via Data Protection's persisted key
  ring and stored under Valkey key prefixes (`pwdless:magic:`); OTP is the same service.
- **MFA**: `MfaService` (TOTP enroll/verify, backup codes), `MfaLockoutService`,
  `MfaPartialAuthService` (step-up state), `OrganizationMfaPolicyService`, `MfaExemptionChecker`.
- **Tenancy**: `Organization.Create` mints the tenant id (org.Id == TenantId);
  `PreRegisteredClientSyncService`/seeding must go through it, never mint ids ad hoc.
- **TestSupportController** exists for test-only endpoints — not a pattern for production features.

## Cross-Module Communication

- **Publishes** (via Wolverine, defined in `Shared.Contracts/Identity/Events/`): user lifecycle
  (EmailVerified, PasswordChanged, UserLoginSucceeded/Failed, UserAccountLockedOut,
  UserEmailChanged…), passwordless (MagicLinkRequested, OtpCodeRequested), MFA, organization
  lifecycle (Created/Archived/Deleted/Reactivated, member add/remove, settings), and
  InvitationCreated.
- **Consumes**: nothing — there is no `EventHandlers/` directory in the Application layer.

## Permissions

`Wallow.Shared.Kernel.Identity.Authorization.PermissionType` declares every permission across all
modules — **read `PermissionType.cs` for the full list**. This module enforces mostly
`UsersRead/Create/Update/Delete`, `RolesRead/Create/Update/Delete`,
`OrganizationsRead/Create/Update/ManageMembers`, `ScopeRead`, plus `AdminAccess`/`SystemSettings`.
Enforced with `[HasPermission]`; scopes map to permissions via `ScopePermissionMapper` (also Kernel).

## Database

Schema: `identity` (`HasDefaultSchema` in `IdentityDbContext`).

## Running Tests

```bash
./scripts/run-tests.sh identity      # Wallow.Identity.Tests (unit + Testcontainers)

# Wallow.Identity.IntegrationTests has no shorthand: every spec carries Category=Integration, so
# reach it by project path AND the tier — without the tier the default filter excludes every spec
# and the run fails on zero tests.
./scripts/run-tests.sh api/tests/Modules/Identity/Wallow.Identity.IntegrationTests integration
```
