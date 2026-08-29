**status: completed**

# Research: rate limiting and audit logging around client registration

Wayfinder research ticket #119 (map #112). Extends
`docs/plans/2026-08-29/1254-external-idp-research.md` §2.3 (the three registration
surfaces) and §7 (inventory); does not repeat them. Scope: what protects and records client
registration, secret rotation, and organization creation today, and where the gaps are.
Decisions are deferred to the registration prototype ticket; this is an inventory plus a gap
list.

Every claim cites a repo path or a primary external source. Line numbers are as of this
commit and will drift.

## 0. Headline

- Rate limiting exists (Redis-backed fixed window, four named policies) but is **disabled in
  `Development` and `Testing`**, so integration tests never exercise it, and the middleware
  runs **before authentication and tenant resolution**, so every "per-user" and "per-tenant"
  partition silently degrades to per-IP. Only `POST /v1/identity/apps/register` has a
  registration-specific policy; admin client CRUD, secret rotation, service-account
  endpoints, and organization creation have none beyond the 1000/h global limiter.
- Audit logging: two stores exist (`audit` entity-change table via an EF interceptor, and
  `auth_audit` event rows via Wolverine handlers). The entity-change interceptor is
  **registered but never attached to any DbContext**, so it writes nothing. The auth-audit
  store records logins, lockouts, membership transitions, and MFA lockout only. **No client
  create/update/delete/rotate event exists** anywhere (no integration event, no audit row),
  and "who created this client and when" is answerable only for developer apps (creator id
  in OpenIddict `Properties` JSON, no timestamp) and service accounts (`ServiceAccountMetadata`
  sidecar). No API or UI reads either audit table.
- Token-endpoint brute force: the `auth` policy (30/min, effectively per-IP) is the only
  defence for client secrets. There is no per-client failed-authentication counter, no
  lockout, and no audit row for `invalid_client`. Secret validation is entirely OpenIddict's
  built-in pipeline, with no custom handler.

## 1. Rate limiting inventory

### 1.1 Registration and limits

`api/src/Wallow.Api/Extensions/RateLimitDefaults.cs` defines the constants:

| Policy | Permit | Window | Partition key (intended) |
|---|---|---|---|
| `auth` | 30 | 1 min | tenant |
| `upload` | 10 | 1 h | tenant |
| `developer-app-registration` | 5 | 1 h | user |
| global limiter | 1000 | 1 h | tenant |

`AddWallowRateLimiting` in `api/src/Wallow.Api/Extensions/ServiceCollectionExtensions.cs`
(≈ lines 100-180) builds each as a `RedisFixedWindowRateLimiterOptions` partition through
`RedisRateLimitPartition.GetFixedWindowRateLimiter` against the shared
`IConnectionMultiplexer`, and installs an `OnRejected` callback that writes a 429
`application/problem+json` body with `Retry-After`, `X-RateLimit-Limit`, and
`X-RateLimit-Remaining: 0`. This is the ASP.NET Core rate-limiting middleware
(`Microsoft.AspNetCore.RateLimiting`) with a distributed store, which is the right shape.

Partition helpers in the same file:

```csharp
private static string GetUserPartitionKey(HttpContext httpContext)
{ string? userId = httpContext.User.GetUserId(); return userId ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"; }

private static string GetTenantPartitionKey(HttpContext httpContext)
{ if (httpContext.Items.TryGetValue("TenantId", out object? tenantId) && tenantId is string tenantIdStr) return tenantIdStr;
  return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"; }
```

### 1.2 Where the policies are applied

| Endpoint | Attribute | File |
|---|---|---|
| `POST /v1/identity/apps/register` | `[EnableRateLimiting("developer-app-registration")]` | `api/src/Modules/Identity/Wallow.Identity.Api/Controllers/AppsController.cs` |
| `POST /connect/token` (whole controller) | `[EnableRateLimiting("auth")]` | `api/src/Modules/Identity/Wallow.Identity.Api/Controllers/TokenController.cs` |
| `AccountController` (whole controller: login, register, password reset, etc.) | `[EnableRateLimiting("auth")]` | `api/src/Modules/Identity/Wallow.Identity.Api/Controllers/AccountController.cs` |
| Storage upload | `[EnableRateLimiting("upload")]` | `api/src/Modules/Storage/Wallow.Storage.Api/Controllers/StorageController.cs` |

**Not rate limited beyond the global limiter** (grep for `EnableRateLimiting` finds no
other hits under `api/src`):

- `ClientsController` (`api/src/Modules/Identity/Wallow.Identity.Api/Controllers/ClientsController.cs`):
  `POST /v1/identity/clients` (create, `AdminAccess`), `PUT {id}`, `DELETE {id}`,
  `POST {id}/rotate-secret`, plus the service-account endpoints
  `POST service-accounts` (`ServiceAccountsWrite`), `PUT .../scopes`,
  `POST .../rotate-secret`, `DELETE ...` (`ServiceAccountsManage`).
- `OrganizationsController` (`.../Controllers/OrganizationsController.cs`):
  `POST /v1/identity/organizations` (`OrganizationsCreate`) and everything else on it.

### 1.3 Two custom Redis counters outside the middleware

These show the existing pattern for a per-principal limit that does not depend on
middleware ordering:

- Passwordless: `api/src/Modules/Identity/Wallow.Identity.Infrastructure/Services/PasswordlessService.cs`
  keys `pwdless:rate:{email}`; `Options/PasswordlessOptions.cs` defaults
  `RateLimitMaxRequests = 3`, `RateLimitWindow = 15 min`;
  `api/src/Wallow.Api/appsettings.Development.json` raises it to 1000.
- Email change: `AccountController.cs` (≈ lines 959-968) keys
  `email:change:rate:{userId}`, 3 per hour, returns 429 `rate_limited`.

### 1.4 Findings (rate limiting)

**F1. Disabled in Development and Testing.** `api/src/Wallow.Api/Program.cs` guards both
`AddWallowRateLimiting()` (≈ lines 541-544) and `app.UseRateLimiter()` (≈ lines 705-709)
with `!IsDevelopment() && !IsEnvironment("Testing")`. `api/tests/Wallow.Tests.Common/Factories/WallowApiFactory.cs`
sets `UseEnvironment("Testing")`, so no integration test ever hits a limiter. The only test
coverage is `api/tests/Wallow.Api.Tests/Extensions/ServiceCollectionExtensionsTests.cs`
(≈ lines 90-115), which asserts that `IConfigureOptions<RateLimiterOptions>` is registered.
The 5/h registration limit and the 30/min token limit are therefore untested behaviour.

**F2. Middleware order makes user and tenant partitions degrade to IP.** In `Program.cs`
the pipeline is `UseRouting` (≈ 651) → `UseRateLimiter` (≈ 708) →
`ApiKeyAuthenticationMiddleware` (≈ 715) → `UseAuthentication` (≈ 719) →
`TenantResolutionMiddleware` (≈ 723) → `UseAuthorization` (≈ 753) → `MapControllers`
(≈ 769). When the limiter runs, `HttpContext.User` is unauthenticated and
`Items["TenantId"]` is unset (`TenantResolutionMiddleware.cs` ≈ line 64 is the only writer,
and it runs later), so both helpers fall through to `RemoteIpAddress`. Consequences:

- `developer-app-registration` is 5/h **per IP**, not per user: a NAT'd office shares one
  budget, and one user with many IPs has many budgets.
- The `auth` policy on `/connect/token` is 30/min per IP, shared across every client and
  user behind that IP.
- The global limiter is 1000/h per IP.

Microsoft's guidance is that `UseRateLimiter` must run after `UseRouting` for
endpoint-specific policies (satisfied) and shows user partitions keyed from
`httpContext.User.Identity?.Name`, which presupposes authentication has run
([MS docs: Rate limiting middleware in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)).
The same page warns that partitioning solely by client IP is vulnerable to source-address
spoofing DoS unless BCP 38 ingress filtering is in place. Moving `UseRateLimiter` after
`UseAuthentication` and `TenantResolutionMiddleware` (still after `UseRouting`) would make
the existing keys mean what they say; the JWT is validated locally so the cost is small.

**F3. No registration-class limit on admin client CRUD, secret rotation, service accounts,
or org creation.** Only the per-IP 1000/h global limiter applies (F2). Secret rotation in
particular is unbounded per client and unlogged (see §2).

**F4. `developer-app-registration` is a per-user cap only.** There is no per-tenant or
global cap on the number of apps a tenant can hold, and no cap on total registrations
across all users; `GetUserAppsAsync` in
`api/src/Modules/Identity/Wallow.Identity.Infrastructure/Services/OpenIddictDeveloperAppService.cs`
scans `applicationManager.ListAsync(int.MaxValue, 0)` and filters by property, so listing
cost grows with the total number of OpenIddict applications, not the caller's.

## 2. Audit logging inventory

### 2.1 Store A: entity-change audit (`audit.audit_entries`)

`api/src/Shared/Wallow.Shared.Infrastructure.Core/Auditing/`:

- `AuditingExtensions.AddWallowAuditing` registers a pooled `AuditDbContext` (schema
  `audit`) and `services.AddSingleton<AuditInterceptor>()`.
- `AuditInterceptor : SaveChangesInterceptor` captures Added/Modified/Deleted entries into
  `AuditEntry { EntityType, EntityId, Action, OldValues, NewValues, UserId, TenantId, Timestamp }`
  (jsonb old/new values, honours `[AuditIgnore]`).
- `Program.cs` calls `AddWallowAuditing` (≈ line 183).

**F5. The interceptor is never attached.** `grep -rn AddInterceptors api/src` finds only
`TenantSaveChangesInterceptor` in Announcements, Inquiries, Notifications, Branding, ApiKeys,
and Storage; Identity attaches none; no `DbContext` under `api/src` adds `AuditInterceptor`.
Tests (`api/tests/Wallow.Shared.Infrastructure.Tests/Auditing/AuditingExtensionsTests.cs`)
assert only DI registration; `AuditTenantIsolationTests.cs` attaches it by hand. In
production `audit.audit_entries` is never written. Even if it were attached to
`IdentityDbContext`, OpenIddict applications are the stock
`OpenIddictEntityFrameworkCoreApplication<Guid>` (`IdentityDbContext.cs` ≈ line 66,
`builder.UseOpenIddict<Guid>()`), so a client row's diff would include the hashed secret
column in `OldValues`/`NewValues` unless excluded.

### 2.2 Store B: auth audit (`auth_audit.auth_audit_entries`)

- `AuthAuditingExtensions.AddAuthAuditing` (same directory) registers
  `AuthAuditDbContext` (schema `auth_audit`) and `IAuthAuditService → AuthAuditService`;
  `AuthAuditService.RecordAsync` swallows exceptions and logs at Error.
- Row shape: `AuthAuditEntry { Id, EventType, UserId, ActorId?, TenantId?, IpAddress?, UserAgent?, OccurredAt }`.
- Contract: `api/src/Shared/Wallow.Shared.Kernel/Auditing/IAuthAuditService.cs`.
- Writer: `api/src/Modules/Identity/Wallow.Identity.Infrastructure/Handlers/AuthAuditEventHandlers.cs`
  (`[WolverineHandler]` static) records `LoginSucceeded`, `LoginFailed`, `AccountLockedOut`,
  `Membership{Transition}` (with `ActorId`), and `MfaLockedOut`.
- Operator doc: `docs/operations/audit-events.md` documents the table, the event types
  above, SQL-only querying, no retention policy, and the "add a Wolverine handler" extension
  path.

### 2.3 Integration events that exist

`api/src/Shared/Wallow.Shared.Contracts/Identity/Events/` contains: AccessRequested,
EmailVerificationRequested, EmailVerified, InvitationCreated, MagicLinkRequested,
MembershipTransition(ed), OrganizationArchived/Created/Deleted/MemberAdded/MemberRemoved/
Reactivated/SettingsUpdated, OtpCodeRequested, PasswordChanged, PasswordResetRequested,
UserAccountLockedOut, UserEmailChanged/ChangeRequested, UserLoginFailed/Succeeded,
UserMfa{BackupCodesRegenerated,Disabled,Enabled,LockedOut,LockoutCleared}, UserRegistered,
UserRoleChanged, UserSessionEvicted.

**F6. No `Client*`, `App*`, or `ServiceAccount*` event exists.** None of the client
create/update/delete/rotate paths publishes anything:

| Operation | Code path | Log | Event | Audit row | Actor recorded |
|---|---|---|---|---|---|
| Developer app register | `AppsController.Register` → `OpenIddictDeveloperAppService.RegisterClientAsync` | 2 × `LoggerMessage` Information (client id only) | none | none | `creatorUserId` in OpenIddict `Properties` JSON; no timestamp (`DeveloperAppInfo.CreatedAt` is always `null`) |
| Admin client create | `ClientsController.Create` → `IOpenIddictApplicationManager.CreateAsync` | none | none | none | none |
| Admin client update | `ClientsController.Update` | none | none | none | none |
| Admin client delete | `ClientsController.Delete` → `DeleteAsync` | none | none | none | none |
| Admin secret rotate | `ClientsController.RotateSecret` → `PopulateAsync` + new secret + `UpdateAsync` | none | none | none | none (secret returned in `ClientResponse`) |
| Service account create | `OpenIddictServiceAccountService.CreateAsync` | ILogger info | none | none | `ServiceAccountMetadata.CreatedBy/CreatedAt` (`AuditableEntity`), `currentUserService.UserId ?? Guid.Empty` |
| Service account rotate | `OpenIddictServiceAccountService.RotateSecretAsync` | ILogger info | none | none | returns `SecretRotatedResult(newSecret, UtcNow)` but persists nothing on metadata |
| Service account revoke | `OpenIddictServiceAccountService.RevokeAsync` | ILogger info | none | none | `metadata.Revoke(userId, timeProvider)` |
| Org create | `OrganizationsController.Create` → `OrganizationService.CreateOrganizationAsync` | `LogCreatingOrganization` | `OrganizationCreatedEvent { OrganizationId, TenantId, Name, Domain, CreatorEmail }` | indirect: `MembershipOwnerMarked` row with `ActorId` via `PublishTransitionAsync(MembershipTransition.OwnerMarked)` | `Organization.Create(..., createdByUserId, timeProvider)` (`AuditableEntity`) |

Files: `api/src/Modules/Identity/Wallow.Identity.Infrastructure/Services/OpenIddictServiceAccountService.cs`,
`.../Services/OrganizationService.cs` (≈ lines 32-95),
`api/src/Modules/Identity/Wallow.Identity.Domain/Entities/ServiceAccountMetadata.cs`,
`api/src/Shared/Wallow.Shared.Kernel/Domain/AuditableEntity.cs`.
`OrganizationCreatedEvent`'s only consumer is
`api/src/Modules/Notifications/.../OrganizationCreatedNotificationHandler.cs`; there is no
auth-audit handler for it, so org creation is visible in `auth_audit` only through the
owner-marking side effect.

**F7. "Who created this client and when" has no single answer.** Three client kinds, three
different answers: developer apps carry the creator id but no time; admin-created clients
carry nothing; service accounts carry both on the sidecar entity but lose rotation history.
The OpenIddict application table has no created/updated columns. The UI
(`apps/wallow-web/src/features/apps/components/RegisterAppForm.tsx`,
`apps/wallow-web/src/features/organizations/components/OrganizationDetail.tsx`) displays
neither `createdAt` nor `createdBy`, and grep finds no `audit` reference under `apps/*/src`
or `packages/*/src`.

**F8. Nothing reads either audit table.** No code under `api/src` outside `Auditing/`
touches `AuditEntries` or `AuthAuditEntries`; there is no API endpoint and no admin UI.
`docs/operations/audit-events.md` documents SQL as the only access path and states there is
no retention policy.

## 3. Token endpoint: brute force on client secrets

### 3.1 What exists

- `TokenController` (`api/src/Modules/Identity/Wallow.Identity.Api/Controllers/TokenController.cs`)
  is `[Route("~/connect/token")]` with class-level `[EnableRateLimiting("auth")]` (30/min,
  per IP after F2). It logs `LogTokenRequest(grant_type, client_id)` at Information,
  unsupported grants at Warning, and token issuance at Information. It contains no
  client-authentication logic: a request with a wrong secret is rejected by OpenIddict before
  the controller's passthrough action runs, so the controller never sees the failure.
- OpenIddict configuration
  (`api/src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs`
  ≈ lines 60-195): stock EF stores, code+PKCE / client credentials / refresh, revocation
  endpoint, `EnableTokenEntryValidation()`. The only custom server handler is for
  `HandleConfigurationRequestContext` (frontchannel logout metadata). No handler observes
  token-request validation or responses.
- `api/tests/Modules/Identity/Wallow.Identity.IntegrationTests/OAuth2/DcrFlowTests.cs`
  has `Should_Reject_Token_Request_With_Wrong_Credentials`, which proves rejection but not
  any counting or lockout.
- Version: OpenIddict 7.6.0 (`api/Directory.Packages.props` ≈ lines 83-84).

### 3.2 What OpenIddict provides (primary source)

Reading `src/OpenIddict.Server/OpenIddictServerHandlers.Exchange.cs` on the `dev` branch
([source](https://github.com/openiddict/openiddict-core/blob/dev/src/OpenIddict.Server/OpenIddictServerHandlers.Exchange.cs)):

- `ValidateClientCredentialsParameters` rejects malformed credential combinations; the
  actual credential check is delegated to `ValidateAuthentication`, which dispatches a
  `ProcessAuthenticationContext` and copies its rejection (`error`, `description`) onto the
  token request. Neither handler counts failures or consults any lockout state.
- `NormalizeErrorResponse` runs on `ApplyTokenResponseContext` and rewrites `invalid_token`
  to `invalid_grant` for code/device/refresh/exchange grants.

`src/OpenIddict.Server/OpenIddictServerEvents.Exchange.cs`
([source](https://github.com/openiddict/openiddict-core/blob/dev/src/OpenIddict.Server/OpenIddictServerEvents.Exchange.cs))
defines the hook points a custom handler can use:

- `ValidateTokenRequestContext : BaseValidatingClientContext` (has `ClientId`, and the
  ability to `Reject`) — the place to consult a per-client failure counter and refuse early.
- `ApplyTokenResponseContext : BaseRequestContext` exposes `Request`, `Response`, and
  `Error => Response.Error` — the place to observe `invalid_client` outcomes and increment a
  counter or record an audit row, before the response is written.

Wallow registers no handler for either event.

### 3.3 Findings (token endpoint)

**F9. No per-client failed-authentication counter, lockout, or audit row.** The only
control is the per-IP `auth` policy. A distributed attacker with many IPs gets 30 guesses per
minute per IP against any confidential client id, and nothing records the attempts:
`UserLoginFailed`/`AccountLockedOut` exist for users, but there is no client analogue.
OpenIddict's `ApplyTokenResponseContext.Error` (§3.2) is the hook to count and audit
`invalid_client`, mirroring the existing `AuthAuditEventHandlers` pattern; a Redis counter
keyed by `client_id` (the `pwdless:rate:{email}` pattern in §1.3) is the shape for a per-client
limit independent of middleware order.

**F10. User lockout relies on framework defaults.** `AccountController` login calls
`CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)`, and no
`options.Lockout` is configured anywhere under `api/src`, so ASP.NET Core Identity's
defaults apply: `MaxFailedAccessAttempts = 5`, `DefaultLockoutTimeSpan = 5 minutes`,
`AllowedForNewUsers = true`, reset on successful authentication
([MS docs: Configure ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0)).
That is a reasonable baseline but is undocumented in the repo; the `auth_audit` store does
record `AccountLockedOut` for it.

**F11. Secret strength is fine; secret handling around registration is not.** Secrets are
32 random bytes base64-encoded (`OpenIddictDeveloperAppService.GenerateClientSecret`) and
stored by OpenIddict's application manager (Wallow never writes the secret column
itself). But `RegisterClientAsync` returns `RegistrationAccessToken: clientSecret`, so the
"registration access token" the response advertises is the client secret itself, not an
RFC 7592 registration token; nothing consumes it, and no RFC 7592
`GET/PUT/DELETE /register/{client_id}` endpoint exists (consistent with the prior doc's
finding that DCR is not implemented).

## 4. Gap list

Ordered by decision relevance for the registration prototype ticket:

1. **Limiter ordering (F2)** — `UseRateLimiter` before `UseAuthentication` /
   `TenantResolutionMiddleware` turns every user/tenant key into an IP key. Fixing this is
   a one-line move in `Program.cs` and is a prerequisite for any per-user registration
   limit meaning anything.
2. **Limiter untested (F1)** — disabled in `Testing`; no integration test can assert a 429.
   Either allow the limiter in `Testing` with generous defaults, or add a targeted factory
   that enables it.
3. **No client lifecycle events (F6)** — no `ClientRegistered` / `ClientSecretRotated` /
   `ClientDeleted` (and service-account equivalents) integration events, so no auth-audit
   rows and no notifications. The `AuthAuditEventHandlers` + `docs/operations/audit-events.md`
   extension path already exists; this is additive.
4. **No uniform creator/timestamp on clients (F7)** — `ServiceAccountMetadata` is the
   proven sidecar pattern (`AuditableEntity` + `ITenantScoped` keyed by client id); developer
   apps and admin clients have no equivalent, and rotation is not persisted for any kind.
5. **No registration-class limits on admin CRUD, rotation, service accounts, org create (F3)**.
6. **No per-client failed-auth counter or `invalid_client` audit (F9)** — hook exists in
   OpenIddict (`ApplyTokenResponseContext.Error`).
7. **Entity-change interceptor dead (F5)** — either attach `AuditInterceptor` where wanted
   (with `[AuditIgnore]` or an exclusion for the OpenIddict secret column) or delete it.
8. **No read path for audit data (F8)** — SQL only, no retention.
9. **`RegistrationAccessToken` is the client secret (F11)** — misleading field; drop it or
   implement RFC 7592.
10. **Per-user-only registration cap and full-table listing scan (F4)**.

## 5. Recommendation (for the prototype ticket to decide)

- Treat 1 and 2 as hygiene to land before or with the prototype: move `UseRateLimiter`
  after tenant resolution and make the limiter testable, otherwise the prototype's
  registration limit is unverifiable and per-IP.
- For the prototype's audit shape, reuse what exists rather than adding a third store: emit
  `Client*` integration events from the three registration surfaces and from rotation and
  deletion, record them via a new case in `AuthAuditEventHandlers` (with `ActorId`, `TenantId`,
  `IpAddress`), and document them in `docs/operations/audit-events.md`. Carry client id in
  the event so the row answers "who did what to which client, when".
- For "who created this client": a `ClientMetadata`-style sidecar (the
  `ServiceAccountMetadata` pattern) that covers all three client kinds, with
  `LastSecretRotatedAt`/`RotatedBy`, is the minimal answer that keeps OpenIddict's table
  stock. Exposing `createdAt`/`createdBy` on `ClientResponse`/`DeveloperAppInfo` then falls
  out.
- Token-endpoint brute force: add an OpenIddict `ApplyTokenResponseContext` handler that
  records `invalid_client` to `auth_audit` and increments a Redis counter per `client_id`,
  with a `ValidateTokenRequestContext` handler that rejects once the counter trips. This is
  independent of the middleware limiter and survives the ordering issue.
- Items 7-10 are cleanup candidates that do not block the prototype.

## 6. Sources

Repo (paths above, plus): `api/src/Wallow.Api/Program.cs`,
`api/src/Modules/Identity/Wallow.Identity.Infrastructure/MultiTenancy/TenantResolutionMiddleware.cs`,
`api/src/Modules/Identity/Wallow.Identity.Infrastructure/Persistence/IdentityDbContext.cs`,
`api/src/Shared/Wallow.Shared.Contracts/Identity/Events/OrganizationCreatedEvent.cs`,
`api/tests/Modules/Identity/Wallow.Identity.IntegrationTests/` (`OAuth2/DcrFlowTests.cs`,
`ClientsControllerTests.cs`, `ServiceAccounts/RotateSecretTests.cs`,
`MembershipTransitionAuditTests.cs`, `AuthAuditEventHandlersTests.cs`).

External:

- ASP.NET Core rate limiting middleware — https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0
- ASP.NET Core Identity configuration (lockout defaults) — https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0
- OpenIddict server token-endpoint handlers — https://github.com/openiddict/openiddict-core/blob/dev/src/OpenIddict.Server/OpenIddictServerHandlers.Exchange.cs
- OpenIddict server token-endpoint events — https://github.com/openiddict/openiddict-core/blob/dev/src/OpenIddict.Server/OpenIddictServerEvents.Exchange.cs
- RFC 7591 / RFC 7592 (registration and registration-management) — covered in
  `1254-external-idp-research.md` §2.1; not repeated here.
