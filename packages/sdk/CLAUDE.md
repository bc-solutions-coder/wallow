# packages/sdk — @bc-solutions-coder/sdk Agent Guide

The TypeScript **BFF auth SDK**: the browser never holds a token; a Node-side tunnel owns
the OIDC session and proxies API calls with a bearer attached.

## Entries (exports map → `src/`; `dist/` only on publish)

- `.` (browser) — `createWallowSdk()` is a **per-request** client factory, no module-global
  singleton. `logout` is the ONE imperative navigation helper: `/bff/logout` is CSRF-gated,
  so it cannot be a link the way login is.
- `./server` (Node) — the BFF tunnel: handlers, proxy, OIDC via openid-client, session sealing.
  `jose` is a direct dependency, used only by the back-channel logout handler to verify logout
  tokens against the issuer's JWKS (openid-client exposes no standalone JWT verifier).
- `./server/passthrough` (Node) — `createApiPassthrough`, a pure reverse proxy owning no
  session. **Its own subpath so a passthrough-only app never pulls `openid-client` into its
  server bundle — nothing here may import the BFF handler/proxy graph.**
- `./server/forwarded` (isomorphic-safe, zero dependencies, no module-scope env reads) — the
  trusted-proxy seam: `resolveClientAddress`/`parseTrustedProxies`/`resolveTrustedProxies`,
  `createClientAddressResolver`, `createRequestOriginResolver`, `PeerRequest`. Both proxying
  presets read the peer from `request.ip` and stamp the resolved caller onto the upstream
  `X-Forwarded-For` (the API pops the rightmost entry); the trust list is the `trustedProxies`
  option else `WALLOW_TRUSTED_PROXIES`. The old `x-wallow-client-ip` host↔SDK header is
  retired — still stripped inbound so a caller cannot smuggle it upstream, never read.
- `./server/service` (Node) — `createServiceClient()`, the client-credentials service-account
  client: same typed client shape, token cached under a `SET NX EX` lock (in-memory `RedisLike`
  when no store), one replay on 401. **Own subpath for the same reason as passthrough — must
  never import the handler/proxy graph**; `service.test.ts` throws if it does.
- `./query` (browser; `@tanstack/react-query` is an **optional** peer) — re-exports the
  generated per-operation artifacts plus the one hand-written module, `invalidations.ts`.

`redis` (node-redis) is an **optional** peer: `createRedisFromUrl` loads it with a dynamic
`import()` on first store use, so cookie-only hosts never install it. `NodeRedisClient` is
deliberately wide (`unknown` replies) so a raw `createClient()` result is assignable.

**The server entries are h3-free and must stay that way** — every handler is a web-standard
`(request: Request) => Promise<Response>`; `src/server/web-standard-handlers.test.ts` pins it.

## Deleted surface — do not bring it back

Banned outright, not deprecated: the module-global client; the hand-written query slices and
their `queryKeys` registry; the `unwrap`/`createAuthClient`/`createMfaClient` layer; the
per-app facade singletons; the imperative `login()`/`getUser()` browser helpers (use
`loginRedirect()` links and `getCurrentUser`/`currentUserQuery`; only the CSRF-gated
`logout()` stays imperative); the browser claim-bag readers (apps read the typed
`CurrentUser` through `@bc-solutions-coder/auth`); and the module-scope CSRF token store
(readers use the double-submit cookie via `readCsrfCookie`). Three guards hold this, and a
change here must keep all three true:

- `src/index.test.ts` (`DELETED_LEGACY_SYMBOLS`) and `src/query/index.test.ts`
  (`RETIRED_EXPORTS`) assert the barrels do NOT export those names.
- Root `.oxlintrc.json` bans them by name; `src/oxlint-guardrails.test.ts` pins that config
  AND runs the real binary over violating and compliant snippets, so the rule cannot rot.
- The seam specs (`apps/*/src/features/*/api.test.ts`) namespace-import the query entry to
  assert absence — the one place the lint rule is deliberately off.

## Session / CSRF model (server entry)

- `expiresAt` is **epoch milliseconds** (not Unix seconds); the proxy silently refreshes
  inside `EXPIRY_SKEW_MS` (30_000).
- Browser-side the double-submit cookie is the ONE token source — read live per request;
  `createWallowSdk({ csrf: false })` skips the interceptor for a passthrough topology
  (wallow-auth), which has no token of its own to stamp.
- RFC 7807: the machine code is a **top-level `code`** on the problem body (never
  `extensions.code`), and `@bc-solutions-coder/api-errors` is the ONLY parser — the browser
  interceptor (`runtime-config.ts`) and the proxy both call its `failureFromResponse`, and every
  failure the SDK raises is its `ApiFailure`. The SDK has no error type of its own;
  `WallowError` / `isWallowError` / `UNKNOWN_ERROR_CODE` / `parseProblemDetails` are deleted and
  pinned deleted by `src/index.test.ts`. A body without a code parses as
  `Client.UnrecognizedResponse`, so a spec that fakes a problem body must give it a `code`.
- **Relayed vs originated.** An upstream failure is relayed byte for byte. Every failure the
  `/api` proxy, the passthrough, `/bff/user`, and the logout CSRF gate answer THEMSELVES goes
  through the ONE writer, `src/server/problem.ts`
  (`problemResponse(status, code, { requestId, detail?, headers? })`): `about:blank`, fixed
  title/detail per code (the server twin of `api-errors`' shipped messages), `requestId` on
  body and header, never `traceId`, never a transport message (that goes to the redacted log).
  Passthrough imports it too, so it must never grow a handler/proxy import. No bodiless
  responses on those paths and no SDK-private code strings — codes come from
  `ErrorCode`/`ClientErrorCode` (the remaining bare `/bff/*` 404/405/400s are a separate issue):
  404 `Http.NotFound` (path outside `/api` / the allowlist / the API base); 401
  `Bff.SessionMissing` (no or unreadable session); 401 `Bff.SessionRefreshFailed` (terminal
  refresh → teardown; a faulting freshness check → no teardown); 403 `Bff.CsrfInvalid`; 401
  `Auth.Unauthenticated` (login redirect survived the replay); 503 `Transport.NetworkError`;
  504 `Transport.Timeout` (forward timeout, proxy only — passthrough adds no timeout).
- `POST /bff/backchannel-logout` (the sixth route) is the OP-to-BFF endpoint: no cookie, no
  CSRF — the signed logout token is the whole security of the request. `RedisLike` requires
  `sadd`/`srem`/`smembers`/`expire` alongside `get`/`set`/`del`; they back the Valkey store's
  sid and subject indexes behind the optional `SessionStore.revokeBySid`/`revokeBySubject`
  (cookie store defines neither, and the server preset warns at boot when the issuer
  advertises back-channel logout over a store that cannot revoke).

## Generated OpenAPI client

`src/generated/` is emitted from the committed snapshot `openapi/v1.json` — never hand-edit
either. Regenerate (API on 5001), then commit both:
`WALLOW_OPENAPI_URL=http://localhost:5001/openapi/v1.json pnpm --filter @bc-solutions-coder/sdk exec tsx scripts/generate.ts`
CI compares the snapshot against the document the API emits **at build time**;
`openapi-autoregen.yml`'s PR output is byte-identical to the manual refresh.

## Tests (vitest, node environment)

- `vitest.config.ts` sets `mockReset: true` — Vitest 4 no longer resets module-factory mocks.
- `openid-client` must be mocked in every spec that loads it (`oidc`, `handlers`, `bff-server`,
  `service`); the discovery cache is keyed by metadata URL — use a unique issuer per test.
  `redis` is mocked the same way (`vi.mock("redis", …)` over a hoisted `createClient` fake) in
  specs that exercise `REDIS_URL` self-connect.

This package is the **template all new workspace packages mirror**. Publishes to GitHub
Packages on `sdk-v*` tags via `package-publish.yml` (shared with `packages/api-errors`,
which generates its `ErrorCode` catalogue from this package's snapshot; the SDK depends on it
as `workspace:^`, so it must be published first), independently of the platform release.
