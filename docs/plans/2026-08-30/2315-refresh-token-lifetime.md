# Per-client refresh-token lifetime and pinned refresh defaults (#144)

**status: completed**

Issue: #144 (parent #131). Fixed point for review: `e2c0597a`.

## Mechanism — OpenIddict native per-application setting

OpenIddict 7.6 resolves the refresh-token lifetime per application before falling back to the
global option, with no custom handler needed. The application's `Settings` dictionary carries
`OpenIddictConstants.Settings.TokenLifetimes.RefreshToken` (`"tkn_lft:reft"`); the value must be
an invariant-culture `TimeSpan` string, e.g.
`TimeSpan.FromSeconds(n).ToString("c", CultureInfo.InvariantCulture)`. Verified against
OpenIddict source: the server reads it at token-creation time via
`IOpenIddictApplicationManager.GetSettingsAsync`, so a change applies to new tokens only —
exactly the issue's semantics. This is the repo's first use of `Settings`; per-client extras
that the server does *not* interpret stay on `Properties` (`ClientApplicationProperties`).

We do **not** write a setting for every client. A client with no setting falls back to the
global `OpenIddict:RefreshTokenLifetimeDays` (7) — the issue's "global configuration remains
the fallback". Registration surfaces write the *default* setting explicitly when the caller
doesn't specify one, so newly registered clients carry their policy visibly.

## Defaults

First-party is `ConsentTypes.Implicit` (seed-only); org-registered and admin-registered
clients are third-party. Registration default when `refreshTokenLifetime` is unset:

- first-party (implicit consent): 7 days (604800 s)
- third-party: 1 day (86400 s)

Shared helper `ClientRefreshTokenLifetime` (Application/Helpers, beside
`ClientApplicationProperties`): the two defaults, seconds→setting-string, setting-string→seconds.

## Pinned refresh defaults (`IdentityInfrastructureExtensions`)

- Rolling refresh tokens: on by default — pin explicitly via
  `options.Configure(o => o.DisableRollingRefreshTokens = false)` with a comment.
- Sliding expiration: `DisableSlidingRefreshTokenExpiration()` already present (fixed family
  expiry — why changing the lifetime never stretches an existing token).
- Reuse leeway: `options.SetRefreshTokenReuseLeeway(...)` from new config key
  `OpenIddict:RefreshTokenReuseLeewaySeconds` (default 30). Config-driven so
  `WallowApiFactory`'s Testing config can shrink it to ~2 s, keeping the after-leeway
  integration test at a ~3 s wait instead of 30 s. Semantics (verified in
  OpenIddictServerHandlers.Protection): replay of a redeemed token within leeway succeeds;
  beyond it, `RevokeByAuthorizationIdAsync` kills the whole family and the request fails.

## Wire surface

`refreshTokenLifetime` — integer seconds, optional/nullable — on:

- `RegisterOrganizationClientRequest`, `UpdateOrganizationClientRequest` (PATCH: null = keep),
  `OrganizationClientResponse` + DTO chain (`OrganizationClientDto`,
  `ClientConfigurationInput`, `RegisterClientInput`).
- Admin `CreateClientRequest`, `UpdateClientRequest` (PUT replaces the mutable surface, but a
  null lifetime keeps the existing setting rather than clearing to fallback — clearing a
  policy silently on an unrelated PUT is a trap), `ClientResponse`.
- Seed: `PreRegisteredClientDefinition.RefreshTokenLifetime` (+ positive-value validation),
  descriptor build in `PreRegisteredClientSyncService.BuildDescriptorAsync`, **and a diff
  branch in `UpdateClientAsync`** so re-seeding an edited value applies it.
- `OrganizationClientService.ApplyConfiguration` writes it; `ToDto` reads it back.

Validation: when provided, must be ≥ 60 and ≤ 31_536_000 (1 year) — guards nonsense values on
every surface with one shared constant pair.

## Tests (pre-agreed seams)

Integration (`Wallow.Identity.IntegrationTests`, new `OAuth2/RefreshTokenLifetimeTests.cs`),
registering through product surfaces (`IOrganizationClientService` for third-party,
`IPreRegisteredClientSyncService` for first-party), asserting token expiry via
`IOpenIddictTokenManager` on the stored refresh-token entry:

1. Client registered with `refreshTokenLifetime: 60` → refresh token expires ≈ 60 s after issue.
2. Unset third-party (org-registered) → ≈ 1 day; unset first-party (seed-sync) → ≈ 7 days.
3. Update client's lifetime → existing refresh token entry unchanged, still refreshes, and the
   refreshed token inherits the original family expiry (sliding off).
4. Refresh, then reuse the old token immediately (within 2 s Testing leeway) → succeeds.
5. Refresh, wait past the leeway, reuse the old token → rejected; the new token is also dead
   (family revoked).

Unit/service coverage rides the existing suites: seeder diff test in
`PreRegisteredClientSyncServiceTests`.

Frontend: browser-mode spec for the new client settings field in wallow-web
(`OrganizationDetail.clients.*` idiom). `organizationClientsUpdateMutation` exists in the SDK
but nothing consumes it yet — add a small per-client settings editor (refresh-token lifetime
field) beside the existing branding editor, plus the optional field on `RegisterClient`.

## Order of work

1. Server config pins + Testing leeway override; red integration tests 1–5 (harness gains
   nothing — registration goes through services).
2. Backend field threading (org service, admin controller, seeder) to green.
3. OpenAPI regen + SDK regen.
4. wallow-web settings editor + spec.
5. Docs: `docs/api/service-accounts.md`, `docs/getting-started/configuration.md`,
   `docs/integrations/bff-pattern.md`, `docs/operations/troubleshooting.md` (config keys),
   seed docs (`fork-guide`/`developer-guide` where clients are described).
6. Gates, two-axis `/code-review` vs `e2c0597a`, fix, push, close #144.
