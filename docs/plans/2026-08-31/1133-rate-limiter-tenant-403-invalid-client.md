**status: active**

# Rate limiter after tenant resolution; fail-closed tenant 403; invalid_client counting (#150)

Parent spec #131. Blocker #134 (fail-closed tenant 403) already shipped; this plan hardens
the remaining three legs: the rate limiter actually partitions per user/tenant, credential
stuffing against the token endpoint is audited and throttled per client id, and the dead
entity-audit stack goes away.

## 1. Rate limiter: order, environments, options

**Bug being fixed:** `app.UseRateLimiter()` currently runs before `UseAuthentication` and
`TenantResolutionMiddleware`, so `GetUserPartitionKey`/`GetTenantPartitionKey` never see a
user or tenant and every policy silently degrades to per-IP. It is also disabled outright in
the Testing environment, so nothing can prove otherwise.

- **Move `app.UseRateLimiter()`** in `Program.cs` to after `PermissionExpansionMiddleware`
  (post-authentication, post-tenant-resolution) and before the endpoint-less 404 shim /
  `UseAuthorization`. Requests count toward the limit regardless of downstream status —
  that is acceptable and is what lets tests observe partitioning through 403 responses.
- **Environment gate becomes `if (!app.Environment.IsDevelopment())`** — Testing runs the
  limiter with generous limits (below).
- **`RateLimitingOptions`** (new, `Wallow.Api/Extensions`), bound from a `"RateLimiting"`
  configuration section: nested `Auth { PermitLimit, WindowMinutes }`,
  `Upload { PermitLimit, WindowHours }`, `Registration { PermitLimit, WindowHours }`,
  `Global { PermitLimit, WindowHours }`. Property initializers default to the existing
  `RateLimitDefaults` constants, which stay as the single source of default values
  (`DeveloperAppRegistration*` consts rename to `Registration*`). Policy factories resolve
  `IOptions<RateLimitingOptions>` from `httpContext.RequestServices` per partition creation.
- **`appsettings.Testing.json`** gains a `RateLimiting` section with generous values
  (e.g. Auth 10000/1min, Upload 10000/1hr, Registration 10000/1hr, Global 100000/1hr) so
  existing suites never trip; the partition-independence test tightens them via
  `UseSetting`, not by editing this file.
- **Partition keys:** `GetTenantPartitionKey` reads `ITenantContext` from
  `RequestServices` (covers both `TenantResolutionMiddleware` and
  `ApiKeyAuthenticationMiddleware`, which sets the context but not
  `HttpContext.Items`); falls back to the authenticated user id, then remote IP.
  `GetUserPartitionKey` unchanged (claims → IP).
- **Policy rename:** `developer-app-registration` → `registration` (it now covers more than
  app creation). Applied per the spec — "registration-class limits apply to every
  org-surface mutation and organization create":
  - `OrganizationClientsController`: POST (already had it), rotate-secret, PATCH, suspend,
    reinstate, platform-suspension POST/DELETE, DELETE.
  - `OrganizationClientBrandingController`: PUT, DELETE logo.
  - `OrganizationsController`: POST (organization create) only — member/settings/branding
    mutations are org-membership surface, not org-surface registration.

## 2. Fail-closed tenant 403 — verification only

Shipped by #134: `TenantResolutionMiddleware.RequiresOrganization` +
`AllowWithoutOrganizationAttribute` + 403 "Organization context required."
`OrganizationlessTokenTests` already covers the AC (org-less endpoints 200, tenant-scoped
endpoints 403 problem+json). No new code; the review verifies coverage against the AC and
extends only if a gap shows.

## 3. invalid_client audit + per-client counter + lockout

- **`AuthAuditRecord.UserId` / `AuthAuditEntry.UserId` become `Guid?`** — a failed client
  authentication has no user. New migration `AllowUserlessAuthAuditEntries` on
  `AuthAuditDbContext` (applies cleanly to a fresh database; pre-release, no backfill).
- **`InvalidClientLockoutOptions`** (Identity.Infrastructure), section
  `Identity:InvalidClientLockout`: `FailureThreshold` (default 5), `WindowMinutes` (5),
  `LockoutMinutes` (5).
- **`IInvalidClientLockout`** (Identity.Application contract) with Infrastructure
  implementation over `IConnectionMultiplexer` (already referenced; HybridCache is a no-op
  fake in tests, so Redis is the seam): failure counter key
  `identity:invalid-client:failures:{clientId}` (INCR, EXPIRE window on first increment);
  at threshold, set `identity:invalid-client:lockout:{clientId}` with lockout TTL.
  `IsLockedOutAsync` = EXISTS on the lockout key. Fixed lockout window, not sliding.
- **`RejectLockedOutClientTokenRequests`** — `IOpenIddictServerHandler
  <ValidateTokenRequestContext>`, scoped, order `Exchange.ValidateAuthentication - 600`
  (before secret validation, so a locked client is rejected even with the right secret):
  when the presented `client_id` is locked out, stamp a transaction property and
  `Reject(Errors.InvalidClient, "The client is temporarily rejected.")`.
- **`AuditInvalidClientTokenResponses`** — `IOpenIddictServerHandler
  <ApplyTokenResponseContext>`, scoped: when the response error is `invalid_client`, the
  request carried a `client_id`, and the lockout-rejection property is absent (don't count
  our own rejections), record an auth-audit event `ClientAuthenticationFailed` (ClientId,
  IP/UserAgent from the HTTP request, `UserId = null`) and bump the counter. This observes
  OpenIddict's own bad-secret rejections, not just custom ones.
- Both handlers register in `IdentityInfrastructureExtensions` beside
  `RefuseUnserviceableClientTokenRequests`.

## 4. Dead entity-audit stack — remove

`AuditInterceptor` is registered in DI but attached to no production DbContext; nothing
writes or reads `AuditEntry`; `[AuditIgnore]` is used nowhere else. Pre-release rules say
remove, not wire up:

- Delete `AuditInterceptor`, `AuditEntry`, `AuditDbContext`, `AuditDbContextFactory`,
  `AuditingExtensions`, the `[AuditIgnore]` attribute, and the `AuditDbContext` migrations
  (`Migrations/20260329204654_InitialCreate*`, `AuditDbContextModelSnapshot`).
- Remove `AddWallowAuditing`/`InitializeAppAuditingAsync` calls in `Program.cs`, the
  `AuditDbContext` migration runner in `MigrationService`, and the `MigrateAsync` call in
  `WallowModules`.
- Delete the stack's tests; fix the false "automatic pickup" claim in
  `docs/getting-started/developer-guide.md` (§ Auditing).
- **AuthAudit\* stays** — that is the live trail.

## 5. Docs

`docs/operations/audit-events.md`: add missing `OrganizationDeleted` rows/section, add
`ClientAuthenticationFailed`, revise the UserId column note (nullable — empty for
client-authentication failures) and ClientId note. `docs/getting-started/configuration.md`
(or the guide that documents `Identity:*` settings) gains `RateLimiting` and
`Identity:InvalidClientLockout` sections.

## Seams under test (pre-agreed)

1. **Partition independence + Testing enablement** — new integration class in
   `Wallow.Api.Tests/Integration` (own tightened factory: derived `WallowApiFactory` /
   `WithWebHostBuilder` + `UseSetting("RateLimiting:Registration:PermitLimit", "2")`),
   `Category=Integration`: user A hits a registration-limited endpoint 3× → third answers
   429 with the problem body + `Retry-After`; user B on the same IP (same host) still gets a
   non-429; proves the limiter runs in Testing and partitions per user after authentication.
2. **Token-endpoint lockout** — new `Identity.IntegrationTests/OAuth2/
   InvalidClientLockoutTests.cs` off `OrganizationClientsTestBase`: register a client,
   present a bad secret `FailureThreshold` times → each attempt audited
   (`ClientAuthenticationFailed` rows via `AuditRowAsync`), next attempt with the CORRECT
   secret still `invalid_client` (locked); a second client with one bad attempt is audited
   but its correct secret still yields a token.
3. **Options defaults** — unit tests: `RateLimitingOptions` and
   `InvalidClientLockoutOptions` defaults equal the documented constants; existing
   `RateLimitDefaultsTests` updated for the rename.
4. **Registration policy coverage** — unit reflection test: every mutating action
   (non-GET) on `OrganizationClientsController` + `OrganizationClientBrandingController`,
   and `OrganizationsController.Create`, carries `[EnableRateLimiting("registration")]`.
5. **Tenant 403** — existing `OrganizationlessTokenTests` (from #134) cited as coverage;
   extended only if review finds an AC gap.

## Out of scope

- Sliding/exponential lockout, per-IP token-endpoint limits (the registration/auth policies
  and the client lockout cover the AC).
- Wiring the entity-audit interceptor up instead of removing it (nothing consumes it).
