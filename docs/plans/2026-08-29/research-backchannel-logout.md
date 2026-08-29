**status: completed**

# Research: back-channel logout — OpenIddict 7 support and the SDK handler shape

_Design note for the wayfinder ticket on back-channel logout. Extends
`1254-external-idp-research.md` §4 (which found back-channel logout absent and ranked it gap
#1); it does not restate that document. Every third-party claim is cited to the spec, the
library's own docs, or the OpenIddict issue tracker; every repo claim to a path._

---

## 0. Headline

1. **OpenIddict 7.6.0 (the version Wallow pins) has no back-channel logout.** It does not emit
   logout tokens, does not know a `backchannel_logout_uri` client property, and does not track
   "visited sites". The maintainer's own tracking issue is open and now targets **8.0** because
   "new models are required, which can only be done in a major version". So on OpenIddict 7 it
   is a **Wallow-side extension of the end-session pipeline** — the same shape as the
   front-channel implementation Wallow already ships.
2. **Wallow already has three of the four OP-side building blocks:** a stable `sid` minted on
   the identity cookie and stamped into every id_token, a per-`sid` participation table
   (`SsoSessionClient`), and the discovery-metadata injection hook. The missing pieces are a
   client property, a logout-token minter, and an HTTP dispatcher.
3. **On the SDK side, `openid-client` v6 deliberately offers no logout-token validation**; the
   maintainer points at `jose`, which is already in the lockfile as `openid-client`'s
   dependency. The handler is `POST /bff/backchannel-logout`, `jose.jwtVerify` against the
   issuer's JWKS, four spec-mandated claim checks, then a **store lookup by `sid`** — which
   needs a new secondary index on `SessionStore`.
4. **Sealed-cookie sessions cannot honor back-channel logout.** A back-channel request arrives
   with no browser and therefore no cookie; the only way to kill a cookie-resident session
   server-side is a denylist, and a denylist is server-side state — exactly what
   `CookieSessionStore` is defined not to have. The honest answer is: the cookie store answers
   `200` and does nothing, the guide already says it is development-only, and the bound stays
   the access-token lifetime. Production (Valkey) honors it fully.

---

## 1. What OpenIddict 7 provides (and does not)

### 1.1 Evidence

- **Tracking issue:** [openiddict-core#2175 "Consider supporting backchannel logout"](https://github.com/openiddict/openiddict-core/issues/2175)
  (opened by the maintainer, Sep 2024; still open; milestone `8.0.0-preview.4`). Quotes:
  "It shouldn't be terribly complicated to implement, but it requires a new session
  entity/manager/store." — "I'll probably take a look as part of the 8.0 effort (it can't happen
  earlier as new models are required, which can only be done in a major version)." — "there's
  still no plans to implement frontchannel logout support as it has always been a clunky
  specification and no longer works for cross-domain communication due to the ban of
  third-party cookies enforced by most browser vendors." — and, on the intended shape: "a new
  entry in OpenIddictApplications something like BackChannelUris … Yes. And … a new 'session'
  entity to track the list of client applications that were accessed during the same
  authentication session (aka 'visited sites')."
- **Shipped surface, 7.6.0** (`api/Directory.Packages.props` pins `OpenIddict.AspNetCore` /
  `OpenIddict.EntityFrameworkCore` 7.6.0): grepping the package XML docs in the local NuGet
  cache, `OpenIddict.Abstractions.xml` matches "backchannel" only in CIBA / client-stack
  resource strings ("backchannel identity token"), and `OpenIddict.Server.xml` has no
  `Backchannel*` or `LogoutToken` member — the lone `RedeemLogoutTokenEntry` handler redeems
  the `id_token_hint` token entry during `ProcessSignOut`, unrelated to logout tokens.
- **7.0 release notes** ([kevinchalet.com, 2025-07-07](https://kevinchalet.com/2025/07/07/openiddict-7-0-is-out/))
  and the [6.0→7.0 migration guide](https://github.com/openiddict/openiddict-documentation/blob/dev/guides/migration/60-to-70.md)
  list token exchange, client-assertion audience changes, store/resolver reshapes — nothing
  about logout beyond the earlier `logout` → `end-session` endpoint rename (noted on #2175 as
  done specifically "to avoid any confusion with the `backchannel logout endpoint` we'll need
  to implement").

### 1.2 What the end-session pipeline does give us

- **Pass-through** is already on (`EnableEndSessionEndpointPassthrough()` in
  `api/src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs`),
  so `LogoutController` owns the request and can do arbitrary work before returning
  `SignOut(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)`.
- **Events model**: any metadata can be added to discovery with
  `AddEventHandler<HandleConfigurationRequestContext>` ([OpenIddict introduction § Events model](https://github.com/openiddict/openiddict-documentation/blob/dev/introduction.md#events-model));
  Wallow already injects `frontchannel_logout_supported` this way.
- **Signing material is reachable**: `OpenIddictServerOptions.SigningCredentials` ("the list of
  signing credentials used by the OpenIddict server services") and
  `OpenIddictServerOptions.JsonWebTokenHandler` ("the JWT handler used to protect and unprotect
  tokens") are public option properties (`OpenIddict.Server.xml`, 7.6.0). `LogoutController`
  already injects `IOptionsMonitor<OpenIddictServerOptions>` for the issuer. The spec requires
  logout tokens be signed with "the same keys … as are used for ID Tokens"
  ([Back-Channel §2.4](https://openid.net/specs/openid-connect-backchannel-1_0.html)), which
  these are — the development certificate or `OpenIddict:SigningCertPath`
  (`IdentityInfrastructureExtensions.cs`), published at `/.well-known/jwks`.
- `HandleEndSessionRequestContext.IdentityTokenHintPrincipal` exists (`OpenIddict.Server.xml`)
  but is unnecessary here: Wallow reads the `sid` from the identity cookie
  (`User.GetSessionId()` in `LogoutController.cs`), which is the same value stamped into every
  id_token (`AuthorizationController.EnsureSessionIdAsync` /
  `BuildClaimsIdentityAsync`, destination `IdentityToken` only).

### 1.3 Design consequence: build it so OpenIddict 8 can delete it

OpenIddict 8 will add an application property for back-channel URIs and its own session
entity. Wallow's implementation should therefore (a) store the property under the **standard
registration name** `backchannel_logout_uri` (§2.2 / IANA registry §5.1.1), exactly as
`frontchannel_logout_uri` is stored today in `ClientApplicationProperties`, and (b) isolate the
minting + dispatch in one Infrastructure service behind one Application interface, so adopting
the native feature later is a property move and a service deletion, not a rewrite.

---

## 2. The spec, reduced to what Wallow must do

Source: [OpenID Connect Back-Channel Logout 1.0 incorporating errata set 1](https://openid.net/specs/openid-connect-backchannel-1_0.html).

| Section | Requirement | Wallow status |
| --- | --- | --- |
| §2.1 | OP advertises `backchannel_logout_supported`; SHOULD also `backchannel_logout_session_supported` (and then "the sid Claim is also included in ID Tokens") | Add two metadata flags beside the front-channel ones; `sid` already in id_tokens |
| §2.2 | RP registers `backchannel_logout_uri` (absolute, no fragment, query retained; `https` SHOULD, `http` MAY **only for confidential clients** when the OP allows it) and optionally `backchannel_logout_session_required` | New client property + validation; Wallow's local/e2e stacks use `http://` container URIs, so the confidential-client carve-out matters |
| §2.3 | OP "need[s] to keep track of the set of logged-in RPs … encouraged to send logout requests to them in parallel" | `SsoSessionClient` (`sid`, `client_id`, `user_id`) is this table already (`Wallow.Identity.Domain/Entities/SsoSessionClient.cs`) |
| §2.4 | Logout token claims: `iss`, `aud`, `iat`, `exp`, `jti`, `events` = `{"http://schemas.openid.net/event/backchannel-logout": {}}`; `sub` and/or `sid`; **`nonce` PROHIBITED**; signed with the ID-token keys; RECOMMENDED `typ: logout+jwt` | New minter |
| §2.5 | `POST` to the URI, body `application/x-www-form-urlencoded`, `logout_token=…`; retransmit only on "potentially recoverable errors" with a delay | New dispatcher |
| §2.6 | RP validation: signature + `alg` as for ID tokens (never `none`); `iss`/`aud`/`iat`/`exp`; `sub` or `sid` present; `events` member present; no `nonce`; optional `jti` replay check; optional `iss`/`sub`/`sid` match against the current session; failure → **HTTP 400** | New SDK handler (§4) |
| §2.7 | RP clears state for the session(s) named by `iss`+`sub` and/or `sid`; "If the identified End-User is already logged out at the RP … the logout is considered to have succeeded"; non-`offline_access` refresh tokens SHOULD be revoked | Store-side revoke; RFC 7009 revocation is now exposed by Wallow (`RevocationController`), so the SDK can revoke the session's refresh token as part of teardown |
| §2.8 | RP answers `200` (OP must also accept `204`); `400` with optional `{error, error_description}` JSON on failure; `Cache-Control: no-store` | Handler response shape |
| §4 | OPs "encouraged to use short expiration times in Logout Tokens, preferably at most two minutes" | `exp = iat + 2 min` |
| §4.1 | Explicit `typ: logout+jwt` is best practice; requiring it "would be a good idea for new deployment profiles where compatibility with existing deployments is not a consideration" | Wallow emits it; the SDK **verifies it when present** and can be told to require it for Wallow-only deployments |

Two quotes that decide architecture: the back-channel "can be more reliable than communication
through the User Agent" (§1), but "the session state maintained between the OP and RP over the
front-channel, such as cookies … are not available when using back-channel communication. As a
result, all needed state must be explicitly communicated between the parties" (§1) — i.e. the
RP must be able to find a session from `sid`/`sub` alone (§4.3 below). And the operational
limit: "the RP's back-channel logout URI must be reachable from all the OPs used" (§1) — the
Wallow API container must be able to reach the RP's BFF.

---

## 3. OP side — the concrete pieces

All paths under `api/src/Modules/Identity/`.

### 3.1 Client registration surface

Mirror `frontchannel_logout_uri` end to end (the existing plumbing is the template):

| Layer | Today (front-channel) | Add |
| --- | --- | --- |
| Property names | `ClientApplicationProperties.FrontchannelLogoutUri = "frontchannel_logout_uri"` (`Wallow.Identity.Application/Helpers/ClientApplicationProperties.cs`) | `BackchannelLogoutUri = "backchannel_logout_uri"`, `BackchannelLogoutSessionRequired = "backchannel_logout_session_required"` |
| Descriptor extensions | `Set/GetFrontchannelLogoutUri` — duplicated in `Wallow.Identity.Infrastructure/Extensions/OpenIddictApplicationExtensions.cs` **and** `Wallow.Identity.Api/Extensions/OpenIddictApplicationExtensions.cs` | Same pair for back-channel (and consider collapsing the duplication while touching it) |
| Seed | `PreRegisteredClientOptions.FrontchannelLogoutUri`; `PreRegisteredClientSyncService` syncs it on create and drift (`Wallow.Identity.Infrastructure/Services/…`); `api/seed.json` sets `http://localhost:3000/bff/frontchannel-logout` and `http://localhost:3003/bff/frontchannel-logout` | `BackchannelLogoutUri` (+ `BackchannelLogoutSessionRequired`); seed values must be **server-reachable** URIs, which in the containerised e2e topology means the compose service hostname, not `localhost` (see §5) |
| Admin API | `CreateClientRequest` / `UpdateClientRequest` / `ClientResponse` carry `FrontchannelLogoutUri`; `ClientsController.TryParseFrontchannelLogoutUri` enforces absolute http(s) | Same three contracts + a validator that also rejects fragments (§2.2) and rejects `http://` for `public` clients (§2.2's confidential-only carve-out); regenerate `packages/sdk/openapi/v1.json` and the client |
| Self-service apps | `AppsController.cs` (developer app registration) | Expose the same field where front-channel is exposed, or explicitly leave it admin-only |

`backchannel_logout_session_required` is a promise the RP makes ("I need `sid`"). Since Wallow
always has a `sid` for a browser session, the OP can always satisfy it; store it for
spec-completeness and so a future OP without `sid` fails loudly, but nothing branches on it.

### 3.2 Discovery

Beside the two front-channel flags in `IdentityInfrastructureExtensions.cs`:

```csharp
context.Metadata["backchannel_logout_supported"] = true;
context.Metadata["backchannel_logout_session_supported"] = true;
```

`openid-client`'s `ServerMetadata` types both keys
([ServerMetadata.md](https://github.com/panva/openid-client/blob/main/docs/interfaces/ServerMetadata.md)),
so the SDK can gate its behaviour on them without a custom type.

### 3.3 Logout-token minting

One Infrastructure service (`BackchannelLogoutNotifier`, behind an Application interface next
to `ISsoClientSessionService`) that, given a `sid`, the issuer and the participating rows,
mints **one token per client** — `aud` is the client id, so tokens are not shareable:

- `iss` = `LogoutController.GetIssuer()` (already computed for front-channel; OpenIddict's
  configured issuer or the request-derived one).
- `sub` = `SsoSessionClient.UserId` (the row stores it; the claim is OPTIONAL but sending both
  `sub` and `sid` lets an RP without a `sid` index still act).
- `aud` = `SsoSessionClient.ClientId`; `iat` = now; `exp` = now + 2 min (§4); `jti` = new GUID.
- `events` = `{"http://schemas.openid.net/event/backchannel-logout": {}}`; `sid` = the sid;
  **no `nonce`**; header `typ: logout+jwt`.
- Sign with `OpenIddictServerOptions.SigningCredentials` (the first credential, i.e. the same
  key OpenIddict signs id_tokens with) through `Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler.CreateToken(SecurityTokenDescriptor)`
  — the `SecurityTokenDescriptor.TokenType` property sets `typ`. `Microsoft.IdentityModel.*`
  is already a transitive dependency of OpenIddict; verify the package reference via
  ref.tools/NuGet before adding a direct `PackageVersion`. **Do not** route this through
  OpenIddict's `ProcessSignIn`/`GenerateIdentityToken` pipeline: a logout token is not an
  id_token, must not carry `nonce`, and must not create a token entry in the store.

### 3.4 Dispatch

- Iterate the `sid`'s `SsoSessionClient` rows (existing query in
  `SsoClientSessionService.BuildLogoutNotificationUrisAsync`), skip clients without a
  `backchannel_logout_uri`, `POST` `logout_token=<jwt>` as
  `application/x-www-form-urlencoded`, **in parallel** (§2.3), each with a short timeout
  (2–3 s) and an overall bound so the user's logout redirect is never held hostage by a slow RP.
- Treat `200`/`204` as success (§2.8); log `400` with the RP's JSON body; treat transport
  errors/timeouts as "potentially recoverable" (§2.5) — **one delayed retry at most in v1, no
  queue**. A durable retry queue is a later decision; the access-token lifetime bounds the
  damage exactly as `docs/integrations/bff-pattern.md` already states for missed front-channel
  notifications.
- Needs an `HttpClient`: the Identity module registers none today (only Notifications calls
  `AddHttpClient`, `Wallow.Notifications.Infrastructure/Extensions/NotificationsModuleExtensions.cs`).
  Register a named client with a conservative timeout, no redirects, and — because the target
  URI is operator-registered data, not code — an SSRF policy decision: at minimum refuse
  link-local/metadata addresses; whether to refuse RFC 1918 ranges depends on the container
  topology (local compose targets *are* private-network hostnames), so make it a config knob
  defaulting to "allow private, deny link-local".
- **Placement in `LogoutController.Logout()`**: run dispatch in phase one, after the `sid` is
  read and **before** `ForgetAsync(sid)` — right where `BuildLogoutNotificationUrisAsync` runs
  today. Front-channel iframes then still render as best-effort; both channels share the same
  participation rows and the same forget. `LogoutPost()` (the POST variant) currently notifies
  nobody on either channel — fix that while here or document why not.

### 3.5 Second trigger: admin/session revocation (defer, but note the seam)

Back-channel logout's real prize is logouts that **do not pass through a browser**: an operator
revoking a session (`SessionController.RevokeSession` → `SessionService.RevokeSessionAsync`,
which writes a Valkey `session:revoked:*` key consulted by `SessionRevocationMiddleware`), a
password change, an account suspension. None of those notify RPs today. Wiring them needs a link
from `ActiveSession` to the cookie `sid` — the two are separate concepts now
(`ActiveSession.SessionToken` vs the `sid` claim), so this is a follow-up that starts with
storing the `sid` on `ActiveSession` at sign-in. Not required for the external-client story;
recorded so the notifier is designed as `NotifyAsync(sid, …)` rather than as a controller helper.

---

## 4. SDK side — the handler shape

All paths under `packages/sdk/src/server/`.

### 4.1 Library choice: `jose`, not `openid-client`

- `openid-client` v6's feature list ([README](https://github.com/panva/openid-client/blob/main/README.md))
  does not include back-channel logout, and the maintainer's answer to exactly this question
  ([discussion #728](https://github.com/panva/openid-client/discussions/728)): the old
  `validateJWT` "is not documented, typed, and therefore not regarded as part of public API and
  I am not actively certifying for the oidc backchannel logout profile. You can use the jose
  module to validate the assertion."
- `jose` is already resolved in the workspace lockfile as `openid-client@6.8.4`'s dependency
  (`jose 6.2.3`, `pnpm-lock.yaml`). It must become a **direct** dependency of `packages/sdk`
  (importing a transitive is a phantom dependency `pnpm lint:manifests` / strict pnpm will
  reject). Pin per the usual caret policy.
- Verification primitives (jose docs): `createRemoteJWKSet(new URL(jwks_uri))` "resolves a JWS
  JOSE Header to a public key object downloaded from a remote endpoint returning a JSON Web Key
  Set … respects the header's alg and kid" and caches with a cooldown
  ([createRemoteJWKSet](https://github.com/panva/jose/blob/main/docs/jwks/remote/functions/createRemoteJWKSet.md));
  `jwtVerify(jwt, JWKS, { issuer, audience, algorithms, typ, clockTolerance, maxTokenAge })`
  checks signature plus `iss`/`aud`/`exp`/`iat`, and "Unsecured JWTs (`alg: none`) are never
  accepted" ([jwtVerify](https://github.com/panva/jose/blob/main/docs/jwt/verify/functions/jwtVerify.md),
  [JWTVerifyOptions](https://github.com/panva/jose/blob/main/docs/jwt/verify/interfaces/JWTVerifyOptions.md)).
  The `jwks_uri` comes from `doc.configuration.serverMetadata().jwks_uri` in `oidc.ts`; it is a
  back-channel URL like `token_endpoint`, so it must **not** be rebased to the public issuer
  (see the `rebaseToIssuer` comment in `oidc.ts` — only browser-facing endpoints are rebased).

### 4.2 Handler contract

Add `backchannelLogout: BffHandler` to `BffHandlers` (`handlers.ts`) and route
`"/backchannel-logout"` in `createWallowBffServer` (`bff-server.ts`, beside
`"/frontchannel-logout"`). Behaviour:

1. `POST` only; anything else → `405` + `Allow: POST` (same pattern as `logout`/`frontchannelLogout`).
2. Body must be `application/x-www-form-urlencoded` with `logout_token`; missing → `400`
   `{"error":"invalid_request"}`.
3. **No CSRF gate, no cookie read.** The request is OP-to-BFF; the signed token is the
   authenticator (§4: "The signed Logout Token is required … to prevent denial of service
   attacks by enabling the RP to verify that the logout request is coming from a legitimate
   party").
4. `jwtVerify(token, jwks, { issuer: config.issuer, audience: config.clientId, algorithms:
   [id_token_signing_alg_values_supported ∩ asymmetric], clockTolerance: 30 })`. Then, by hand
   (jose does not know this profile): `events` is an object containing
   `http://schemas.openid.net/event/backchannel-logout`; `nonce` absent; `sub` or `sid` present;
   if `protectedHeader.typ` is present it must be `logout+jwt` (make `requireLogoutTokenType`
   an opt-in `BffConfig` flag — Wallow emits it, other OPs may not, §4.1). Any failure → `400`
   with `{ error: "invalid_request", error_description }` and `cache-control: no-store`.
5. Issuer comparison reuses `normalizeIssuer` (trailing-slash tolerance) from `handlers.ts`.
6. Optional `jti` replay guard: only meaningful with a shared store; implement as
   `store.rememberLogoutJti?(jti, ttl)` on the Valkey store, skip on the cookie store.
7. Revoke: `sid` present → `store.revokeBySid(sid)`; else `store.revokeBySubject(sub)` (all of
   that user's sessions at this RP, per §2.4 "If a sid Claim is not present, the intent is that
   all sessions at the RP for the End-User … be logged out"). Then, if the revoked session held a
   refresh token, call RFC 7009 revocation at the OP (`revocation_endpoint` from metadata;
   `openid-client` exposes `tokenRevocation`) — §2.7's SHOULD for non-`offline_access` tokens.
8. Respond `200` with an empty body and `cache-control: no-store`. **Already-gone is success**
   (§2.7) — no probing surface, and the OP will not retry.

Unlike `frontchannelLogout`, which answers a uniform `200` page to every miss because it is a
browser-reachable GET, this endpoint **must** return `400` for an invalid token (§2.6/§2.8).
That is safe: the only thing a `400` reveals is that a forged token is forged.

### 4.3 Mapping `sid` → session: the `SessionStore` change

Today sessions are addressed only by the opaque reference in the cookie
(`SessionStore.read/write/destroy(ref)`, `store/types.ts`). A back-channel request has no
cookie, so the store needs a second key. Proposed additions to `SessionStore`:

```ts
/** Destroy every session recorded for this OP session id. Optional: a store
 *  that cannot address sessions without their cookie reference omits it. */
revokeBySid?: (sid: string) => Promise<void>;
/** Destroy every session for this subject (sid-less logout tokens). */
revokeBySubject?: (sub: string) => Promise<void>;
```

- **`ValkeySessionStore`** (`store/valkey.ts`): `write()` already knows `session.sid` and
  `session.user.sub`. Add `<prefix>:sid:<sid>` → `sessionId` (`SET … EX ttl`, same TTL as the
  record) and `<prefix>:sub:<sub>` → set of session ids. `revokeBySid` reads the index, `DEL`s
  the session key and the index key. `RedisLike` (`store/types.ts`) then needs `sAdd`/`sMembers`
  (or the sub-index can be a JSON string rewritten under the existing lock) — keep `RedisLike`
  minimal; a single-key JSON list is enough at BFF scale. `destroy(ref)` and the user-initiated
  logout must also clear the index entries so the map does not leak.
- **`CookieSessionStore`** (`store/cookie.ts`): leaves both methods `undefined`. The handler
  still validates the token fully (so misconfiguration is visible in logs), answers `200`, and
  logs at warn level "back-channel logout received but the session store cannot revoke". This
  is the honest behaviour — see §4.4.
- **Capability signalling**: `createWallowBffServer` should warn once at boot when
  `backchannel_logout_supported` is advertised by the OP and the selected store has no
  `revokeBySid`, so a fork that leaves the development default in place finds out before an
  incident does.

`BffSession` already carries `sid` (captured from the id_token before the userinfo overlay,
`handlers.ts` callback) and `user.sub`, so no session-shape change is needed.

### 4.4 Can sealed-cookie sessions honor back-channel logout at all?

No — and the reason is structural, not a missing feature:

- A sealed cookie **is** the session (`session.ts`: "sealed into an opaque, encrypted string …
  and unsealed on each request"); `CookieSessionStore.destroy()` is documented as a deliberate
  no-op, and the guide already states the consequence: "Anyone holding a copy of the sealed
  cookie value … can keep using it, and the server has no way to revoke it"
  (`docs/integrations/bff-pattern.md` § "Why the cookie store cannot revoke a session").
- Back-channel logout arrives **without** the cookie (§1: front-channel state "are not available
  when using back-channel communication"), so the handler cannot even re-seal an expired blob;
  the only server-side lever is a **denylist keyed by `sid`** that every request consults.
- A denylist is server-side state with the same durability and multi-process requirements as
  the Valkey store itself. An in-memory `Map<sid, exp>` would work for one process until
  restart (the cookie store already has a single-process precedent in `withRefreshLock`'s
  coalescing map), but it would give a fork the *impression* of revocation it does not have
  across replicas or restarts — the failure mode the guide warns against. **Recommendation:
  do not add it.** Keep `CookieSessionStore` = development only, exactly as documented, and let
  its ceiling be the access-token lifetime (15 min default,
  `OpenIddict:AccessTokenLifetimeMinutes`) plus the refresh refusal path the guide already
  describes.
- What the OP *can* do for a cookie-store RP: revoke the refresh tokens minted to that client
  for that user at end-session. OpenIddict token entries are keyed by subject and application,
  not by `sid`, so this is "all of this user's refresh tokens at this client, on every device"
  — too blunt to default on; note it as a possible opt-in per client, not part of v1.

### 4.5 Configuration and registration

- **BFF config**: nothing mandatory. Optional `backchannelLogout: { requireLogoutTokenType?:
  boolean; clockToleranceSeconds?: number }`. The endpoint path is fixed under the mount
  (`/bff/backchannel-logout`) like the others.
- **What the RP registers at Wallow**: `backchannel_logout_uri =
  <server-reachable BFF origin>/bff/backchannel-logout`, `backchannel_logout_session_required =
  true`. For an external RP (bcordes.dev) this is its public origin. For the local three-origin
  e2e it is the compose service address (e.g. `http://bff-example:3003/...`) — the API
  container, not the browser, is the caller. This is the one place `metadataUrl`-style
  internal/external address splitting shows up on the OP side.
- **Ingress**: the BFF must accept `POST` from the Wallow API's egress on that path with no
  auth cookie and no CSRF header — call this out in the quickstart, because an origin-lock or
  WAF rule that assumes every `/bff/**` request is a browser request will drop it.

---

## 5. Keeping front-channel as best-effort (research §4 / §9 item 4)

Nothing is removed. Both channels read the same `SsoSessionClient` rows and both fire in
`LogoutController` phase one. A client may register either, both, or neither URI. The guide's
"notification is best-effort" paragraph becomes "front-channel is best-effort; back-channel is
the reliable path for server-side RPs" — which matches OpenIddict's own stance (#2175: no plans
for front-channel "as it … no longer works for cross-domain communication due to the ban of
third-party cookies"). The SDK's `frontchannelLogout` handler stays as is.

Verification: `apps/wallow-web/e2e-cross-app/external-origin-login.spec.ts` is the natural
place — after logging out at wallow-web, assert the external-origin `/bff/user` answers `401`
**with the iframe path disabled** (or with third-party cookies blocked), which is the property
front-channel cannot give. Today no cross-app spec references either logout channel.

---

## 6. Findings list

1. OpenIddict 7.6.0 ships no back-channel logout; native support is scheduled for 8.0 and needs
   new models (openiddict-core#2175). Wallow builds it as an end-session extension, like
   front-channel today.
2. Wallow already has the "visited sites" registry (`SsoSessionClient`), a stable `sid` on the
   cookie and in id_tokens, the discovery-injection hook, and access to the id_token signing
   credentials — the OP work is a client property, a minter, and a parallel POST dispatcher.
3. The SDK handler is `POST /bff/backchannel-logout`, validated with `jose` (promote to a direct
   dependency), with the profile checks jose does not do (`events`, no `nonce`, `sub|sid`, `typ`).
4. `SessionStore` needs `revokeBySid`/`revokeBySubject`; Valkey gets a `sid`/`sub` secondary
   index; cookie store leaves them undefined.
5. Sealed-cookie sessions cannot honor back-channel logout; the only mechanism (a denylist) is
   server state, so the answer is "use the Valkey store in production", already the guide's rule.
6. Spec-mandated response semantics differ from front-channel: `400` for invalid tokens, `200`
   for already-gone sessions, `Cache-Control: no-store`.
7. `http://` back-channel URIs are allowed only for confidential clients — the validator must
   check `public`.
8. Admin-driven session revocation is the second, browser-less trigger; deferred because
   `ActiveSession` and the cookie `sid` are not linked yet.

## 7. Recommendation

Implement in one ticket pair: **OP** (property + discovery + `BackchannelLogoutNotifier` +
dispatch in `LogoutController`, standard property names so OpenIddict 8 can subsume it) and
**SDK** (`backchannelLogout` handler + `SessionStore.revokeBySid/revokeBySubject` + Valkey index
+ boot-time capability warning + `jose` dependency), then extend the three-origin e2e to prove
an external session dies without the iframe. Keep front-channel untouched. Document in
`docs/integrations/bff-pattern.md` that back-channel logout requires a server-side session store
and a server-reachable BFF URL, and that the cookie store's ceiling remains the access-token
lifetime. Do not build a cookie-store denylist.
