**status: completed**

# Research: bcordes.dev vs `@bc-solutions-coder/sdk` — the gap table

Answers wayfinder ticket #120 (map #112). Extends `1254-external-idp-research.md` §3 (which
already describes the SDK's shape); this note diffs what `bc-solutions-coder/bcordes` does by
hand against what the SDK provides, capability by capability, so the SDK is known sufficient
(or not) for that consumer. Feeds #121 (service-account grilling) and #127 (RP prototype).

Sources are primary: the bcordes repo at HEAD (paths below are relative to that repo unless
prefixed `packages/sdk/`, `apps/` or `api/`), the SDK sources in this repo, the Wallow API
sources, and openid-client's own docs (`docs/functions/clientCredentialsGrant.md` in
`panva/openid-client`).

## Verdict in one paragraph

The SDK covers the whole **interactive** surface bcordes hand-rolled — login/callback/logout,
sealed sessions, Valkey store with a refresh lock, silent + reactive refresh, 429/`Retry-After`,
problem-details parsing, POST-only logout — and does several of them *better* (nonce,
`__Host-` cookie, id_token validation through openid-client, front-channel logout,
cookie-password rotation, request-id correlation, forwarded client IP). It has **no
client-credentials (M2M) helper**: `src/lib/wallow/service-client.ts` has no SDK equivalent
and is the one real gap. Everything else on bcordes's list is either "SDK has it", "bcordes
owns it anyway" (security headers, SSE fan-out, TanStack server functions), or a contract
difference the prototype must absorb (route prefix, CSRF model, error-code placement,
`expiresAt` units).

## Gap table

Legend: **HAS** = SDK has it; **LACKS** = SDK lacks it; **N/A** = bcordes does not need it
from the SDK (host-owned or not needed at all); **DIFF** = both have it but the contract
differs and the consumer must adapt.

| # | Capability | bcordes today | SDK | Verdict |
| --- | --- | --- | --- | --- |
| 1 | Discovery via openid-client `discovery()`, cached, `allowInsecureRequests` in dev | `src/lib/auth/oidc.ts` `getOidcConfig` (cache reset on failure; insecure only when `NODE_ENV !== production`) | `packages/sdk/src/server/oidc.ts` `discover()` — cached per metadata URL; insecure exactly when the metadata URL is `http:` (`shouldAllowInsecureRequests`); split-horizon `OIDC_METADATA_URL` re-pinning | **HAS** (SDK's insecure rule is URL-driven rather than env-driven — better, since bundlers fold `NODE_ENV`) |
| 2 | Authorization URL: code + PKCE S256 + `state` | `getAuthorizationUrl(state, codeVerifier)`; **no `nonce`** | `buildAuthorizeUrl` via `buildAuthorizationUrl` with `state`, `nonce`, S256 (`oidc.ts`, `handlers.ts`) | **HAS** (SDK adds nonce) |
| 3 | Login-transaction state between authorize and callback | Three plain cookies `__oauth_state`, `__oauth_code_verifier`, `__oauth_return_to` (HttpOnly, 600 s; `src/routes/auth/login.ts`) | One **sealed** `${cookieName}_tx` cookie carrying `{state, nonce, verifier, returnTo}`, 600 s, always `Lax` (`txstate.ts`, `handlers.ts`) | **HAS** |
| 4 | Redirect-loop breaker (`__oauth_attempts`, max 3 → `/auth/error?reason=too_many_redirects`) | `src/routes/auth/login.ts` | none | **LACKS** — minor, bcordes-specific UX; cheap to keep host-side in front of `/bff/login` |
| 5 | Callback: state check; PKCE verifier; code exchange | `src/routes/auth/callback.ts` uses `timingSafeEqual` for state, then `authorizationCodeGrant(..., {pkceCodeVerifier, expectedState})` | `handlers.ts` compares `state !== tx.state`, then hands `expectedState`, `expectedNonce`, `pkceCodeVerifier` to `authorizationCodeGrant` (`oidc.ts` `exchangeCode`) | **HAS** (see hardening table on the timing question) |
| 6 | id_token validation (signature, iss/aud/exp, nonce) | Not done: `parseUserFromToken` decodes the JWT payload unverified | Delegated to openid-client's `authorizationCodeGrant` with `expectedNonce` | **HAS** (bcordes gains this) |
| 7 | Userinfo fetch + claim merge | `fetchUserProfile` (`fetchUserInfo` with expected subject) merged with unverified token claims | `fetchUserInfo` with `skipSubjectCheck`, overlaid on id_token claims, then `mapClaims` | **HAS** |
| 8 | Claims → user shape | `src/lib/auth/claims.ts` → `{id,name,email,roles,permissions,tenantId,tenantName}`; `role` string-or-array; reads `org_id`/`org_name` | `claims.ts` `mapClaims` → `{sub,email,name,roles,permissions,tenantId,tenantName,…}`; `role`/`roles` merged; `tenant_id`/`tenant_name` (NOT `org_id`/`org_name`); `scope` folded into `permissions` | **DIFF** — `id`→`sub`, tenant claim names differ. Raw claims pass through the index signature so `org_*` stay readable; needs a one-line adapter, or #121/#127 decides which claim names the IdP emits |
| 9 | Session record shape | `SessionData {sessionId, accessToken, refreshToken, idToken?, expiresAt (Unix **seconds**), user, version, csrfToken?}` (`src/lib/auth/types.ts`) | `BffSession` — same fields plus `sid`; `expiresAt` in epoch **milliseconds** (`session.ts`) | **DIFF** (units only) |
| 10 | Session cookie | `__session`, HttpOnly, `Secure` only in production, `Lax`, `Path=/`, `Max-Age=86400`; value = sealed session id (`src/lib/auth/session.ts`) | `__Host-wallow_bff` (`wallow_bff` when `COOKIE_SECURE=false`), HttpOnly, Secure, `Lax`/`Strict`, `Path=/`, `Max-Age=SESSION_TTL_SECONDS` (86400 default); value = sealed session id under the Valkey store (`config.ts`, `handlers.ts`) | **HAS** (stronger: `__Host-` + `COOKIE_PASSWORDS` rotation) |
| 11 | Sealed cookies via `iron-webcrypto` | `iron-webcrypto ^2.0.0`, `SESSION_SECRET` ≥ 32 chars | `iron-webcrypto ^1.2.1`, `COOKIE_PASSWORD` ≥ 32 chars validated at boot (`config.ts`) | **HAS** (env name differs) |
| 12 | Valkey session store, TTL 86400 | `bcordes:session:<id>` via ioredis (`src/lib/valkey/*`) | `ValkeySessionStore` over the `RedisLike` port: `<prefix>:session:<id>`, default prefix `wallow`, TTL from config (`store/valkey.ts`); ioredis adapter recipe in `packages/sdk/README.md`; SDK never imports a redis client itself | **HAS** (key prefix configurable) |
| 13 | Distributed refresh lock (`SET NX EX 10`) | `withRefreshLock` in `session.ts` → `undefined` when held | `ValkeySessionStore.withRefreshLock` SET NX EX (10 s) → `undefined` when held; proxy re-reads the peer's write (`proxy.ts` `refreshUnderLock`) | **HAS** |
| 14 | Proactive refresh 30 s before expiry; keep old refresh token when none returned | `getAuthUser()` in `src/lib/auth/middleware.ts`; `refreshToken()` in `oidc.ts` | `ensureFreshSession` (`EXPIRY_SKEW_MS` = 30 000); `refreshTokens` result merged with `?? session.refreshToken` (`proxy.ts`) | **HAS** |
| 15 | Refresh-failure behaviour: clear session, treat as anonymous | `getAuthUser` → `clearSession()` + `null` (asserted by `auth-hardening.test.ts`) | Proxy answers bare `401` and does NOT destroy the store record or clear cookies (`proxy.ts` catch); `/bff/user` keeps answering the stale claims while the record exists | **DIFF** — see hardening table |
| 16 | Reactive 401 → refresh once → retry; `3xx` to `/Account/Login` treated as 401 | `src/lib/wallow/client.ts` + `request.ts` `isAuthRedirect` | `forwardWithResilience`: `401`, or `3xx` whose `Location` contains `/account/login` → `forceRefreshSession` under lock → one replay (`proxy.ts`, `redirect: "manual"`) | **HAS** |
| 17 | 429 → wait `Retry-After` → retry once | `parseRetryDelay` (seconds, default 1000 ms, unbounded) | `retryAfterMs` (delta-seconds **or** HTTP-date, bounded by `MAX_RETRY_AFTER_MS` = 5 s, default 0) | **HAS** |
| 18 | 30 s upstream timeout; network/timeout → 503 `NETWORK_ERROR` / `NETWORK_TIMEOUT` | `request.ts` `toNetworkError`, `AbortSignal.timeout(30000)` | `FORWARD_TIMEOUT_MS` = 30 000; same two codes, 503 (`proxy.ts`) | **HAS** (identical codes) |
| 19 | RFC 7807 parsing → typed error | `WallowError {status, code, traceId, validationErrors}` reads **top-level** `code`/`traceId` (`src/lib/wallow/errors.ts`, `types.ts`) | `parseProblemDetails` probes `extensions.code` → `code` → `error`; `extensions.traceId` → `traceId`; `errors` → `fieldErrors`; `WallowError {status, code, title, detail, requestId, traceId, fieldErrors}` (`server/errors.ts`, `src/errors.ts`) | **HAS** — superset; `validationErrors` ↔ `fieldErrors` rename |
| 20 | Upstream error body relayed verbatim to the browser | bcordes throws `WallowError` inside a server function (never relays) | Proxy relays status/headers/body of the upstream failure and synthesises problem+json with `requestId` for its own faults (`respondToFailure`) | **HAS** |
| 21 | Request-id correlation | none | `x-request-id` minted/echoed on every proxied response and forwarded upstream (`request-id.ts`, `proxy.ts`) | **HAS** (bonus) |
| 22 | `X-Forwarded-*` / client-IP forwarding for per-user API rate limiting | none | `applyForwardedHeaders` + `CLIENT_IP_HEADER` seam (`forwarded.ts`); host stamps it (`apps/wallow-web/src/app/lib/bff.server.ts`) | **HAS** (bonus) |
| 23 | Logout: POST-only, clear session, redirect to end-session with `id_token_hint` + `post_logout_redirect_uri` | `src/routes/auth/logout.ts` POST only; `getLogoutUrl` uses `end_session_endpoint ?? ${issuer}/connect/logout` | `/bff/logout`: 405 to non-POST, **CSRF-gated** (403), `store.destroy`, cookies cleared, `buildLogoutUrl` via `buildEndSessionUrl` with the same `/connect/logout` fallback (`handlers.ts`, `oidc.ts`) | **HAS** (see §end_session) |
| 24 | Front-channel logout | none | `/bff/frontchannel-logout` matching `iss` + `sid` (`handlers.ts`) | **HAS** (bonus) |
| 25 | Back-channel logout | none | none (1254 §gaps; ticket #115) | **N/A** for bcordes today |
| 26 | `GET /auth/me` → user JSON or `null` | `src/routes/auth/me.ts` (always 200) | `GET /bff/user` → claims + `csrfToken`, `cache-control: no-store`; **401** when anonymous | **DIFF** — 200-null vs 401; SDK browser `getCurrentUser()` already maps 401 → `null` |
| 27 | CSRF | Synchronizer token in session; `x-csrf-token` header; h3 middleware `src/server/middleware/csrf-validation.ts` skips safe methods + anonymous; token fetched via server fn `src/server-fns/csrf.ts` | Same synchronizer token, ALSO written to a readable `${cookieName}-csrf` cookie (double-submit); proxy gates POST/PUT/PATCH/DELETE on `/api/**` and logout (403 `CSRF_INVALID`); browser `wireCsrfInterceptor`/`readCsrfCookie` (`server/csrf.ts`, `src/csrf.ts`) | **HAS** — superset. The SDK gate covers only paths that go **through the proxy**; bcordes's own mutating server functions still need a host gate |
| 28 | Security headers (CSP, HSTS, COOP, …) | `src/server/middleware/security-headers.ts` | none — out of SDK scope | **N/A** (host-owned; keep) |
| 29 | Client-credentials (M2M) service account: `clientCredentialsGrant`, Valkey-cached token with lock/poll, 401-refetch-retry-once, 429 | `src/lib/wallow/service-client.ts` (`OIDC_SERVICE_CLIENT_ID/SECRET`, scope `inquiries.write inquiries.read`) | **none** — no `clientCredentialsGrant` call in `packages/sdk/src` (grep); every SDK request path presupposes a browser session (`proxy.ts` 401 without cookie) | **LACKS** — the one substantive gap. See §M2M |
| 30 | Anonymous contact form → API via service account | `src/server-fns/inquiries.ts` `submitInquiry` falls back to `serviceClient.post('/v1/inquiries')` | Not expressible through the SDK | **LACKS** (consequence of 29) |
| 31 | Typed API client for `/v1/inquiries*` | Hand-written paths on `client.ts`/`service-client.ts` | Generated `@hey-api` operations + TanStack Query `{op}Options/Mutation` from `openapi/v1.json` (`src/generated`, `src/query`) | **HAS** (bonus, for the user-session paths) |
| 32 | SSE proxy `/api/notifications/stream` → `GET /events?subscribe=…` with bearer, keepalive, reconnect-on-end with refresh, envelope camelCasing | `src/routes/api/notifications/stream.ts` (SseManager, 4 h max, SIGTERM drain) | Proxy streams the upstream body and clears the 30 s abort once headers arrive, so `GET /api/events?subscribe=…` streams through with a fresh bearer (`proxy.ts`); API exposes `/events` as `text/event-stream` (`api/src/Wallow.Api/Program.cs`, `SseEndpoint.cs`). No keepalive injection, reconnect, or envelope rewrite | **DIFF** — the plain stream works through `/api/events`; the manager layer stays host-owned (or rely on `EventSource`'s native reconnect) |
| 33 | `requireAuth`/`requireAdmin` server-side guards (403 unless `admin`) | `src/lib/auth/middleware.ts` | Browser `requireAuth` (`route-context.ts`) redirects to login; no role guard; server exports `readSession`/`ensureFreshSession` to build one | **N/A** (host-owned; trivial over `readSession`) |
| 34 | Plain-HTTP dev (`Secure` off) | `secure: isProd` | `COOKIE_SECURE=false` for dev; fails secure otherwise | **HAS** |
| 35 | Host framework: TanStack Start + Nitro + h3 | h3 `defineEventHandler` middleware, TanStack server routes | Handlers are web-standard `(Request)=>Promise<Response>`, h3-free by rule (`packages/sdk/CLAUDE.md`); reference mount is a TanStack Start splat route with one `ANY` handler (`apps/wallow-web/src/app/routes/bff/$.ts`) | **HAS** — same framework family |
| 36 | Package distribution | n/a | GitHub Packages, `access: restricted` (`packages/sdk/package.json`); ticket #117 | **DIFF** — install needs a `read:packages` token in `~/.npmrc` (1254 §gaps) |
| 37 | Env contract | `OIDC_ISSUER, OIDC_CLIENT_ID, OIDC_CLIENT_SECRET, OIDC_REDIRECT_URI, SESSION_SECRET, VALKEY_URL, WALLOW_API_URL, OIDC_SERVICE_CLIENT_ID/SECRET` (`.env.example`) | `OIDC_ISSUER, OIDC_CLIENT_ID, OIDC_CLIENT_SECRET, OIDC_REDIRECT_URI, OIDC_POST_LOGOUT_REDIRECT_URI, BFF_API_BASE_URL, COOKIE_PASSWORD[S]`; optional `OIDC_SCOPES, COOKIE_NAME, OIDC_METADATA_URL, SESSION_TTL_SECONDS, COOKIE_SECURE, COOKIE_SAMESITE, REDIS_URL` (`config.ts`, README) | **DIFF** — rename `SESSION_SECRET`→`COOKIE_PASSWORD`, `WALLOW_API_URL`→`BFF_API_BASE_URL`, add `OIDC_POST_LOGOUT_REDIRECT_URI`, set `OIDC_SCOPES` explicitly (default `openid profile email offline_access` lacks `roles`, `inquiries.*`, `notifications.*`) |

## Special attention

### Client credentials (M2M) — SDK lacks it

- bcordes needs it for exactly one path: the anonymous contact form (`submitInquiry` when no
  session, `src/server-fns/inquiries.ts`) posting `/v1/inquiries` as `sa-bcordes-bff`.
- The SDK has no M2M surface: no `clientCredentialsGrant` import in `packages/sdk/src`, no
  token cache, no "bearer without a session" path. `createApiProxy` answers 401 with no
  session cookie (`proxy.ts`), and `createWallowSdk` is a browser/SSR client over the proxy.
- The API side is ready: OpenIddict is configured with `AllowClientCredentialsFlow()`
  (`api/src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs`)
  and `TokenController` handles `IsClientCredentialsGrantType()` →
  `HandleClientCredentialsAsync()`
  (`api/src/Modules/Identity/Wallow.Identity.Api/Controllers/TokenController.cs`); service
  accounts are managed by `OpenIddictServiceAccountService`. The seeded `sa-bcordes-bff`
  client was removed by plan `1203-remove-bcordes-client-seeding.md` — a deployment now
  provisions its service account through the admin UI/`ClientsController`.
- openid-client already ships the primitive: `clientCredentialsGrant(config, parameters?,
  options?)` (`panva/openid-client` `docs/functions/clientCredentialsGrant.md`). bcordes's
  helper is ~120 lines on top of it (Valkey cache `bcordes:service-token`, TTL
  `expires_in − 30`, lock + 100 ms poll, 401 → drop cache → refetch → replay once, 429 →
  `Retry-After`).
- Options for #121: (a) bcordes keeps `service-client.ts` as is — it depends only on
  openid-client and ioredis, both of which it already has; (b) the SDK grows a
  `createServiceClient({clientId, clientSecret, scope, store})` on the `./server` entry,
  reusing `discover`, `RedisLike`, and the retry/429/problem-details logic already in
  `proxy.ts`/`errors.ts`. (b) is small and would keep the resilience code in one place, but
  it is not required for the RP prototype (#127), which exercises only the interactive path.

### `/auth/*` vs `/bff/*` prefix

- bcordes routes: `/auth/login`, `/auth/callback`, `/auth/logout`, `/auth/me`, `/auth/error`;
  registered redirect URI `https://bcordes.dev/auth/callback` (`.env.example`).
- SDK: `WALLOW_BFF_MOUNT = "/bff"` and `WALLOW_API_MOUNT = "/api"` are exported constants;
  `createWallowBffServer().handleBff` dispatches only
  `/bff/{login,callback,user,logout,frontchannel-logout}` (`bff-server.ts` `bffSubPath`), and
  the `/api` proxy strips a hardcoded prefix (`proxy.ts`). Neither is configurable. The
  browser entry hardcodes `/bff/login` (`route-context.ts` `BFF_LOGIN_PATH`) and `/bff/logout`
  (`auth.ts`).
- The lower-level `createBffHandlers(config, store)` returns the five handlers individually,
  so a host CAN mount them at `/auth/*` — but then the browser helpers (`loginRedirect`,
  `logout`) and `createWallowSdk({baseUrl})` conventions stop matching. The callback path
  itself is just `config.redirectUri`, so any path validates.
- Recommendation: adopt `/bff/*` and `/api/*` in bcordes (pre-release, no users), re-register
  the redirect URI, keep `/auth/error` as a bcordes page if the loop-breaker UX is retained.
  Making the prefix configurable in the SDK buys nothing here.

### `end_session` handling

- Both build the RP-initiated logout URL from `end_session_endpoint` with the same fallback
  `${issuer}/connect/logout` (bcordes `getLogoutUrl`; SDK `buildLogoutUrl`). Wallow advertises
  the endpoint — `SetEndSessionEndpointUris(OpenIddictEndpointUris.EndSession)` with
  `EndSession = "connect/logout"`
  (`api/src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/OpenIddictEndpointUris.cs`,
  `IdentityInfrastructureExtensions.cs`) — so the fallback is never exercised against Wallow.
- Differences: the SDK reads `postLogoutRedirectUri` from config
  (`OIDC_POST_LOGOUT_REDIRECT_URI`, required) where bcordes derives `${origin}/` per request;
  the SDK pins the browser-facing URL to the public issuer under split-horizon
  (`pinToBrowserEndpoint`); the SDK requires a matching `x-csrf-token` on the POST (bcordes's
  `logout.ts` relies on its h3 middleware); the SDK answers `204` (no redirect) when there is
  no session to end.
- bcordes's `logout-csrf.test.ts` asserts POST-only + 302 to a URL containing
  `post_logout_redirect_uri=<origin>/`: the SDK satisfies both given
  `OIDC_POST_LOGOUT_REDIRECT_URI=https://bcordes.dev/`.

### The retry/problem-details client in `src/lib/wallow/`

- `client.ts` (user session) is fully subsumed by the `/api` proxy + generated operations:
  401/login-redirect → refresh-under-lock → replay once, 429 → `Retry-After` → replay once,
  30 s timeout, 503 `NETWORK_ERROR`/`NETWORK_TIMEOUT`, problem-details parsing. The SDK's
  `Retry-After` is bounded at 5 s and defaults to 0 ms (bcordes: unbounded, default 1 s) —
  the safer choice.
- Error-code placement: bcordes reads top-level `code`; the SDK reads `extensions.code` first
  and top-level `code` second (`server/errors.ts` `readCode`), so either wire shape parses.
  `WallowError.isValidation/isNotFound/...` and `validationErrors` map to SDK
  `status`/`fieldErrors`; `toJSON` omitting `traceId` is a host concern.
- `service-client.ts` is the M2M half and is NOT subsumed (§M2M).
- bcordes's server functions call the API server-to-server with the session token in hand;
  under the SDK a TanStack loader builds `createWallowSdk({baseUrl, cookieHeader,
  internalOrigin})` and goes back through its own `/api` proxy (README § server-rendered
  loaders). One extra hop, but it keeps the token out of the render.

### What the hardening tests assert that the SDK must also guarantee

| bcordes test | Assertion | SDK status |
| --- | --- | --- |
| `auth-hardening.test.ts` (1) | Refresh failure → session cleared, user `null` | **Partial.** Proxy answers 401 but leaves the store record and cookies in place; `/bff/user` keeps answering the stale claims until the record's TTL (`proxy.ts`, `handlers.ts`). The browser recovers because 401 → `getCurrentUser()` → `null` → login link, and the next callback overwrites the session. For literal parity the SDK should `store.destroy(ref)` + clear cookies on that path — small change, worth filing from #127 |
| `auth-hardening.test.ts` (2) | Successful refresh → new tokens persisted, `version` incremented | **HAS** (`refreshUnderLock`: `version + 1`, `store.write`) |
| `auth-hardening.test.ts` (3) | Missing `OIDC_CLIENT_ID`/`OIDC_REDIRECT_URI` → descriptive error | **HAS**, stronger: all problems aggregated into one boot-time error (`loadBffConfigFromEnv`) |
| `auth-hardening.test.ts` (4) | `Set-Cookie` carries `Max-Age=86400` | **HAS** (`sessionCookieOpts` `maxAge: sessionTtlSeconds`, default 86 400) |
| `oauth-state-timing.test.ts` | State compared with `timingSafeEqual`, not `===` | **DIFF.** `handlers.ts` uses `state !== tx.state` before openid-client re-checks `expectedState`. The tx cookie is sealed and the state is random, so an `!==` timing oracle reveals nothing exploitable; for literal parity swap in the timing-safe compare already in `server/csrf.ts`. (This bcordes test reads source off disk — the kind `.claude/rules/TESTING.md` bans here; do not port it) |
| `logout-csrf.test.ts` | No GET logout; POST clears session; 302 to end-session with `post_logout_redirect_uri=<origin>/` | **HAS** (405 on GET, `store.destroy`, redirect) plus a CSRF gate the bcordes route lacks |
| `service-client-401.test.ts` | Service token refetched on 401; exactly one retry with the new bearer | **N/A** — no SDK M2M path (§M2M) |
| `csrf-token.test.ts`, `security-headers.test.ts`, `inquiries-auth.test.ts` | Session CSRF token via server fn, header middleware, `requireAdmin` 403 | **N/A** — host-owned; keep in bcordes |
| `src/lib/auth/oidc.test.ts` | Discovery args; exact scope string; insecure only in non-prod; end-session fallback; cache reset on failure; refresh-token fallback | **HAS** with two nuances: a failed `discovery()` throws before the SDK caches it (equivalent to bcordes's reset); insecure is by URL scheme, not `NODE_ENV` (use an `http://` issuer/metadata URL in dev) |

## Contract differences the RP prototype (#127) must absorb

1. Route prefix `/auth/*` → `/bff/*` + `/api/*`; redirect URI re-registered.
2. Env renames (row 37) and an explicit `OIDC_SCOPES` including `roles`, `offline_access`,
   `inquiries.*`, `notifications.*`.
3. `user.id` → `user.sub`; tenant claims: keep reading `org_id`/`org_name` off the claim bag
   or have the IdP emit `tenant_id`/`tenant_name` (decision for #121/#127).
4. `expiresAt` seconds → milliseconds (only matters if bcordes code inspects the session).
5. `/auth/me` 200-`null` → `/bff/user` 401; use `getCurrentUser()`.
6. CSRF: drop the server-fn token fetch, let the double-submit cookie + interceptor carry it;
   keep a host gate for any bcordes-owned mutating route that bypasses `/api`.
7. Valkey key prefix `bcordes` → `keyPrefix` option (or accept `wallow`).
8. SSE: point `EventSource` at `/api/events?subscribe=Notifications,Inquiries` or keep the
   bcordes stream route; the manager/normalisation layer is host-owned either way.
9. Service account: keep `service-client.ts` (works unchanged against Wallow's
   `client_credentials`) until/unless the SDK grows an M2M helper.

## Recommendation

Adopt the SDK for the interactive path as-is; it is a superset of bcordes's hand-rolled auth
on every axis except the M2M service client. File two small SDK follow-ups from #127:
destroy-on-failed-refresh (hardening row 1) and, optionally, a `createServiceClient` M2M
helper on `./server` (row 29, owned by #121). Do not make the `/bff` prefix configurable;
change bcordes.
