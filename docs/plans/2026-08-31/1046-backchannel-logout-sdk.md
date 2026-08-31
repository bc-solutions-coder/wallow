**status: active**

# Back-channel logout: SDK side (#147)

The BFF SDK receives OIDC back-channel logout with zero consumer code: a POST-only
`/bff/backchannel-logout` route under the existing BFF mount verifies the logout token and
revokes the matching server-side session(s). Counterpart of the OP side shipped for #146
(`docs/plans/2026-08-31/0909-backchannel-logout-op.md`); #151 (e2e proof) is downstream.

## Shape

### 1. Store capabilities — `revokeBySid` / `revokeBySubject`

`SessionStore` gains two **optional** methods; each returns the sessions it destroyed so the
handler can revoke their refresh tokens upstream:

```ts
revokeBySid?: (sid: string) => Promise<BffSession[]>;
revokeBySubject?: (sub: string) => Promise<BffSession[]>;
```

- `RedisLike` grows **required** set/expiry operations — `sadd`, `srem`, `smembers`,
  `expire` — implemented by `createMemoryRedis`, `createRedisAdapter` (node-redis
  `sAdd`/`sRem`/`sMembers`/`expire`), and the lazy `createRedisFromUrl` wrapper. Pre-release:
  the correct port beats a compatible one; the repo has no external implementers.
- `ValkeySessionStore` indexes at `write`: `<prefix>:sid:<sid>` → session id (string key,
  session TTL) when the session carries a `sid`; `<prefix>:sub:<sub>` set gains the session id
  (set re-`expire`d to the session TTL on each write). `destroy` reads the record first and
  clears both index entries. `revokeBySid` resolves the index, deletes record + indexes,
  returns the session; `revokeBySubject` walks the subject set, pruning stale members.
- `CookieSessionStore` leaves both **undefined** — nothing server-side exists to revoke.

### 2. Discovery additions

`DiscoveryDoc` gains `jwks_uri?: string` and `backchannel_logout_supported?: boolean`,
populated by `discover()` from openid-client's `serverMetadata()`. `jwks_uri` is a
server-reachable backchannel URL — used as advertised, never rebased to the public issuer.

### 3. The handler — `src/server/backchannel-logout.ts`

New module exporting `createBackchannelLogoutHandler(config, store)`; `createBffHandlers`
composes it in as `handlers.backchannelLogout`, and the preset routes
`/backchannel-logout` under `WALLOW_BFF_MOUNT`. No cookie is read, no CSRF gate — the caller
is the OP, not a browser.

- `POST` only (405 + `Allow: POST` otherwise); body `application/x-www-form-urlencoded`
  with `logout_token=<jwt>`; every response carries `cache-control: no-store`.
- Verification: `jose` (**new direct dependency**; already in the tree transitively) —
  `jwtVerify` against a cached `createRemoteJWKSet(jwks_uri)` with `issuer` (both with and
  without trailing slash — the OP mints `Uri.AbsoluteUri`), `audience` = client id, an
  explicit asymmetric `algorithms` allowlist, `clockTolerance` 30 s, and
  `requiredClaims: ["iat", "exp", "jti", "events"]`.
- Hand checks per OIDC Back-Channel Logout 1.0 §2.6: `events` contains
  `http://schemas.openid.net/event/backchannel-logout` with an object value; `nonce` is
  ABSENT (its presence is what rejects a replayed id token); `sid` or `sub` present; a
  `typ` header, when present, must be `logout+jwt` (or `application/logout+jwt`).
- Invalid → `400` JSON `{"error": "invalid_request"}` — no detail leaks to the caller.
- Valid → revoke: prefer `store.revokeBySid(sid)` when the token carries `sid` and the store
  can; else `store.revokeBySubject(sub)`; a store that can do neither is a no-op (the boot
  warning below covered it). Unknown sid/sub → still `200`: already-gone is success.
- Best-effort RFC 7009: each revoked session holding a `refreshToken` is revoked upstream
  via openid-client's `tokenRevocation(configuration, token, { token_type_hint:
  "refresh_token" })`; failures are swallowed — the local session is already dead.

### 4. Boot warning

`createWallowBffServer` fire-and-forgets a discovery probe at build time: when the resolved
metadata advertises `backchannel_logout_supported: true` and the selected store defines
neither revoke method, it calls `options.onWarning` (default `console.warn`) once with a
message naming the cookie store's limitation. Discovery failure at boot is swallowed — the
OP may not be up yet, and the first real request retries through the same cache.

### 5. Docs

`docs/integrations/typescript-sdk.md` documents the endpoint: the URL to register on the
client (`https://<app-host>/bff/backchannel-logout`), that it must be **server-reachable
from the OP** (ingress requirement — the OP's SSRF gate refuses private hosts unless
`Identity:BackchannelLogout:AllowPrivateNetworkHosts` is on), and that server-side revocation
needs the Valkey store. `docs/integrations/bff-pattern.md` back-channel section links to it.

## Seams under test (pre-agreed)

1. `ValkeySessionStore.revokeBySid`/`revokeBySubject` + index write/destroy semantics —
   `store/valkey.test.ts` over a fake `RedisLike`; cookie-store absence in `store/cookie.test.ts`.
2. `createRedisAdapter` set-op translation — `store/redis-adapter.test.ts`.
3. The handler as a web-standard `Request → Response` seam — new
   `server/backchannel-logout.test.ts`: REAL jose crypto (per-test generated key pair, JWKS
   served through a stubbed `fetch`), openid-client mocked per repo convention. Covers the
   AC matrix: valid token → session gone → 200; bad `aud`/`iss`/missing `events`/present
   `nonce`/expired → 400; unknown sid → 200; GET → 405; `no-store` everywhere; RFC 7009 call.
4. Boot warning matrix — `server/bff-server.test.ts`: advertised+cookie-store warns;
   advertised+valkey does not; unadvertised never; discovery failure stays silent.

## Out of scope

- E2E proof across the containerised stack (#151).
- Front-channel logout changes; the existing handler stays as is.
