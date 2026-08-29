# packages/sdk — @bc-solutions-coder/sdk Agent Guide

The TypeScript **BFF auth SDK**: the browser never holds a token; a Node-side tunnel owns
the OIDC session and proxies API calls with a bearer attached.

## Entries (exports map → `src/`; `dist/` only on publish)

- `.` (browser) — `createWallowSdk()` is a **per-request** client factory, no module-global
  singleton. `logout` is the ONE imperative navigation helper: `/bff/logout` is CSRF-gated,
  so it cannot be a link the way login is.
- `./server` (Node) — the BFF tunnel: handlers, proxy, OIDC via openid-client, session sealing.
- `./server/passthrough` (Node) — `createApiPassthrough`, a pure reverse proxy owning no
  session. **Its own subpath so a passthrough-only app never pulls `openid-client` into its
  server bundle — nothing here may import the BFF handler/proxy graph.**
- `./query` (browser; `@tanstack/react-query` is an **optional** peer) — re-exports the
  generated per-operation artifacts plus the one hand-written module, `invalidations.ts`.

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
- RFC 7807: the machine code is in `extensions.code` — parse from there with an `UNKNOWN`
  fallback, never a top-level `code`.

## Generated OpenAPI client

`src/generated/` is emitted from the committed snapshot `openapi/v1.json` — never hand-edit
either. Regenerate (API on 5001), then commit both:
`WALLOW_OPENAPI_URL=http://localhost:5001/openapi/v1.json pnpm --filter @bc-solutions-coder/sdk exec tsx scripts/generate.ts`
CI compares the snapshot against the document the API emits **at build time**;
`openapi-autoregen.yml`'s PR output is byte-identical to the manual refresh.

## Tests (vitest, node environment)

- `vitest.config.ts` sets `mockReset: true` — Vitest 4 no longer resets module-factory mocks.
- `openid-client` must be mocked in **both** `oidc.test.ts` and `handlers.test.ts`; the
  discovery cache is keyed by metadata URL — use a unique issuer per test.

This package is the **template all new workspace packages mirror**. Publishes to GitHub
Packages on `sdk-v*` tags via `sdk-publish.yml`, independently of the platform release.
