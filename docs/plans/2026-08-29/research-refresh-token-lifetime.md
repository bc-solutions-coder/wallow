**status: completed**

# Research: per-client refresh-token lifetime in OpenIddict

Wayfinder research ticket #118 (map #112). Extends
`docs/plans/2026-08-29/1254-external-idp-research.md`, which covers the identity seam and
external-IdP options but touches refresh tokens only in passing. This note answers one question:
**can a refresh-token lifetime be set per client, where would that setting live in Wallow, and
what does the BFF SDK assume about refresh-token lifetime and rotation?**

All third-party claims are cited to OpenIddict source on the `dev` branch of
`openiddict/openiddict-core` (Wallow pins OpenIddict **7.6.0** in `api/Directory.Packages.props`)
or to the OpenIddict documentation repository. Repo claims cite paths.

## 1. What Wallow does today

`api/src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs`
configures three **server-wide** lifetimes from configuration and turns sliding expiration off:

```csharp
options.SetAccessTokenLifetime(TimeSpan.FromMinutes(configuration.GetValue("OpenIddict:AccessTokenLifetimeMinutes", 15)));
options.SetRefreshTokenLifetime(TimeSpan.FromDays(configuration.GetValue("OpenIddict:RefreshTokenLifetimeDays", 7)));
options.SetIdentityTokenLifetime(TimeSpan.FromMinutes(configuration.GetValue("OpenIddict:IdentityTokenLifetimeMinutes", 10)));
// OpenIddict uses rolling refresh tokens by default ...
options.DisableSlidingRefreshTokenExpiration();
```

- Rolling refresh tokens are left at the OpenIddict default (on). Token storage is on and the
  validation side calls `EnableTokenEntryValidation()` (same file), so redeemed/revoked entries are
  checked on every access-token validation.
- `docs/operations/troubleshooting.md` documents the two config keys
  (`OpenIddict:AccessTokenLifetimeMinutes`, `OpenIddict:RefreshTokenLifetimeDays`).
- No per-client lifetime exists anywhere: `api/seed.json` clients carry
  `clientId, displayName, tenantName, seedMembers, seedMemberRoles, public, secret, redirectUris,
  postLogoutRedirectUris, frontchannelLogoutUri, scopes`; the bound model is
  `PreRegisteredClientDefinition` in
  `api/src/Modules/Identity/Wallow.Identity.Infrastructure/Options/PreRegisteredClientOptions.cs`.
- Wallow's existing per-client extension point is the OpenIddict application **`Properties`** JSON
  bag: `tenant_id`, `is_operator`, `frontchannel_logout_uri`
  (`api/src/Modules/Identity/Wallow.Identity.Application/Helpers/ClientApplicationProperties.cs`,
  accessors in
  `api/src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/OpenIddictApplicationExtensions.cs`)
  plus the seeder's `source` marker
  (`api/src/Modules/Identity/Wallow.Identity.Infrastructure/Services/PreRegisteredClientSyncService.cs`).
- `TokenController.HandleAuthorizationCodeOrRefreshAsync`
  (`api/src/Modules/Identity/Wallow.Identity.Api/Controllers/TokenController.cs`) re-authenticates
  the code/refresh principal, rebuilds the identity, sets scopes and the `wallow-api` resource,
  and calls `SignIn`. It does **not** look the application up; only the client-credentials path
  (`GetApplicationPropertiesAsync`) reads `Properties`.

## 2. What OpenIddict 7 offers

### 2.1 Three-tier lifetime resolution (per principal, per application, global)

`OpenIddictServerHandlers.PrepareRefreshTokenPrincipal`
(<https://raw.githubusercontent.com/openiddict/openiddict-core/dev/src/OpenIddict.Server/OpenIddictServerHandlers.cs>)
resolves the refresh-token lifetime in this order:

1. **Principal claim** — `context.Principal.GetRefreshTokenLifetime()`, set by
   `principal.SetRefreshTokenLifetime(TimeSpan?)` which stores `Claims.Private.RefreshTokenLifetime`
   as whole seconds
   (<https://raw.githubusercontent.com/openiddict/openiddict-core/dev/src/OpenIddict.Abstractions/Primitives/OpenIddictExtensions.cs>).
   `ValidateSignInDemand` (same handlers file) requires each lifetime claim to be a single integer
   claim.
2. **Application setting** — when the client is known and degraded mode is off, the handler calls
   `IOpenIddictApplicationManager.GetSettingsAsync(application)` and, if
   `Settings.TokenLifetimes.RefreshToken` is present and parses with
   `TimeSpan.TryParse(setting, CultureInfo.InvariantCulture, ...)`, uses it.
3. **Global option** — `context.Options.RefreshTokenLifetime`
   (`SetRefreshTokenLifetime` on `OpenIddictServerBuilder`,
   <https://raw.githubusercontent.com/openiddict/openiddict-core/dev/src/OpenIddict.Server/OpenIddictServerBuilder.cs>).

The same three-tier pattern is implemented for access, identity, authorization-code, device, user
and request tokens (`PrepareAccessTokenPrincipal`, `PrepareIdentityTokenPrincipal`, ... in the same
file, and `Settings.TokenLifetimes.AccessToken` etc.). So **a per-client refresh-token lifetime
is a first-class OpenIddict feature**: it lives in the application's `Settings` dictionary
(`OpenIddictApplicationDescriptor.Settings`), which is a separate column from `Properties`. Wallow's
EF model already has it — `IdentityDbContextModelSnapshot.cs` declares both `Properties` and
`Settings` string columns on the OpenIddict application entity — so no schema change is needed.

### 2.2 Sliding vs absolute expiry is server-wide, and it interacts with per-client lifetimes

From the same handler, quoted because it decides the semantics:

```csharp
// When sliding expiration is disabled, the expiration date of generated refresh tokens is fixed
// and must exactly match the expiration date of the refresh token used in the token request.
if (context.EndpointType is OpenIddictServerEndpointType.Token &&
    context.Request.IsRefreshTokenGrantType() &&
    context.Options.DisableSlidingRefreshTokenExpiration)
{
    ... principal.SetExpirationDate(notification.RefreshTokenPrincipal.GetExpirationDate());
}
else
{
    // principal claim -> application setting -> global option (see 2.1)
}
```

Consequences:

- `DisableSlidingRefreshTokenExpiration` is a global `OpenIddictServerBuilder` switch ("refresh
  tokens are issued with a fixed expiration date: when they expire, a complete authorization flow
  must be started"). There is no per-client sliding/absolute toggle.
- Because Wallow disables sliding expiration, **any per-client or per-principal lifetime is applied
  only when the chain starts (authorization-code grant, i.e. at login)**. Every refresh grant in
  that chain copies the previous token's absolute expiry. Changing a client's lifetime therefore
  affects new logins only; existing sessions keep their original 7-day deadline.
- `DisableTokenStorage()` implicitly disables sliding expiration
  (`OpenIddictServerBuilder.cs`); not relevant to Wallow, which stores tokens.

### 2.3 Rotation and reuse detection are server-wide

- Rolling refresh tokens are the default; `DisableRollingRefreshTokens()` is documented as "NOT
  recommended, for security reasons" (`OpenIddictServerBuilder.cs`). There is no
  `UseRollingRefreshTokens` — nothing to opt into.
- `RedeemTokenEntry` (sign-in pipeline, `OpenIddictServerHandlers.cs`) marks the incoming refresh
  token `Redeemed` on a refresh grant unless rolling is disabled.
- `Protection.ValidateTokenEntry`
  (<https://raw.githubusercontent.com/openiddict/openiddict-core/dev/src/OpenIddict.Server/OpenIddictServerHandlers.Protection.cs>):
  presenting a redeemed refresh token when `RefreshTokenReuseLeeway` is unset or elapsed revokes
  every token attached to the authorization (`RevokeByAuthorizationIdAsync`; the authorization
  itself is kept "to allow the legitimate client to start a new flow") and rejects with
  `invalid_token`, which `Exchange.NormalizeErrorResponse`
  (<https://raw.githubusercontent.com/openiddict/openiddict-core/dev/src/OpenIddict.Server/OpenIddictServerHandlers.Exchange.cs>)
  rewrites to `invalid_grant` on the token endpoint. `SetRefreshTokenReuseLeeway(TimeSpan?)` is
  the only knob and it is global.
- Expired refresh tokens fail `Protection.ValidateExpirationDate` (`invalid_token`, also
  normalised to `invalid_grant`).
- `AttachSignInParameters` derives `expires_in` from the **access-token** principal's expiration;
  the refresh-token expiry is never sent to the client.

Summary of the per-client surface: **lifetime yes (Settings or principal claim); sliding, rolling
and reuse leeway no (global only).**

## 3. What the SDK assumes

`packages/sdk/src/server/oidc.ts`, `proxy.ts`, `handlers.ts`, `config.ts`, `store/valkey.ts`,
`store/cookie.ts`:

- The SDK never learns the refresh-token lifetime. It tracks only `expiresAt = now + expires_in`
  (access token) with a 30 s skew (`EXPIRY_SKEW_MS`) and refreshes proactively in
  `ensureFreshSession`, or reactively in `forceRefreshSession` on an upstream 401 / login redirect.
- Rotation is already assumed: `refreshUnderLock` stores `tokens.refresh_token ?? session.refreshToken`
  and bumps `version`; the Valkey store serialises refreshes with `SET lockKey NX EX 10`
  (`DEFAULT_LOCK_TTL_SECONDS`), the cookie store with an in-process map. A peer that loses the
  lock re-reads the stored session rather than spending the one-time token twice — exactly what
  rolling tokens plus reuse detection require.
- A refresh failure (`invalid_grant` from an expired, redeemed or revoked refresh token) propagates
  from openid-client's `refreshTokenGrant` and is handled by the proxy's error path: the request
  fails and the user is sent back through login. There is no special casing by error code.
- `SESSION_TTL_SECONDS` (default `86_400`, `config.ts`; Valkey `DEFAULT_TTL_SECONDS` identical) is
  independent of the 7-day refresh lifetime. Today the BFF session dies after 24 h of Valkey TTL
  regardless of the refresh token; a per-client refresh lifetime shorter than the session TTL is
  the only case the SDK would notice (refresh fails, re-login), and it already handles it.

**Conclusion: no SDK change is required for a per-client lifetime.** Documentation
(`docs/integrations/typescript-sdk.md` § silent refresh, `docs/operations/troubleshooting.md`) should
mention that a client-level lifetime surfaces as a forced re-login, not as a distinct error.

## 4. Options

| | Option | How | Pros | Cons |
|---|---|---|---|---|
| **A** | **Native OpenIddict application setting** (recommended) | Add an optional `refreshTokenLifetime` (`TimeSpan` string, e.g. `"30.00:00:00"`) to `PreRegisteredClientDefinition`; `PreRegisteredClientSyncService` writes/clears `descriptor.Settings[OpenIddictConstants.Settings.TokenLifetimes.RefreshToken]`; expose the same field on `ClientsController`/`AppsController` create/update/response DTOs. | Zero token-endpoint code; OpenIddict resolves it in `PrepareRefreshTokenPrincipal`; column already exists; the same mechanism extends to access/identity-token lifetimes for free; keeps `OpenIddict:RefreshTokenLifetimeDays` as the global fallback. | Value format is `TimeSpan.Parse` invariant, so the admin API should validate/normalise input; only applies at login while sliding expiration is disabled (2.2). |
| B | Custom `Properties` key read in `TokenController` | Add `refresh_token_lifetime_seconds` to `ClientApplicationProperties`, look the application up in `HandleAuthorizationCodeOrRefreshAsync`, call `claimsPrincipal.SetRefreshTokenLifetime(...)`. | Mirrors the existing `tenant_id` pattern; allows non-client inputs (per user/tenant policy) to influence the value. | Re-implements what OpenIddict already does; adds a DB lookup to every code/refresh exchange; two places to keep in sync. Only worthwhile if the lifetime must depend on something other than the client (e.g. tenant policy). |
| C | Status quo (global only) | Tune `OpenIddict:RefreshTokenLifetimeDays`. | Nothing to build. | Does not answer the requirement; forces one lifetime on browser BFFs, native/mobile clients and service accounts alike. |

Not options: per-client sliding expiry, per-client rolling toggle, per-client reuse leeway — all
global in OpenIddict 7 (2.2, 2.3). Keep the current defaults (rolling on, sliding off, no leeway).

## 5. Recommendation

Adopt **Option A**. Store the per-client lifetime where OpenIddict reads it —
`OpenIddictApplicationDescriptor.Settings[Settings.TokenLifetimes.RefreshToken]` — fed from a new
optional `seed.json` field and the client admin API, with the global config value as the default.
Keep sliding expiration disabled (absolute expiry) and rolling tokens on. Document two behaviours:
the lifetime applies to new logins only, and a shorter lifetime shows up in the BFF as a re-login
after `invalid_grant`. The seam stays intact: the setting is consumed inside Identity and reaches
consumers only as token expiry, so external-IdP migration (see the external-IdP research note)
just maps it to the provider's equivalent client setting.

Open follow-ups for the implementing ticket:

- Choose the wire format for the admin API (seconds integer vs ISO 8601 duration) and normalise to
  an invariant `TimeSpan` string when writing `Settings`.
- Decide whether `SESSION_TTL_SECONDS` docs should recommend `<=` the client's refresh lifetime.
- Optionally expose `Settings.TokenLifetimes.AccessToken` in the same change, since the code path
  is identical.
