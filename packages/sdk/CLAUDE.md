# packages/sdk — @bc-solutions-coder/sdk Agent Guide

The TypeScript **BFF auth SDK**: the browser never holds a token; a Node-side tunnel owns
the OIDC session and proxies API calls with a bearer attached.

## Four entry points (exports map → `dist/`)

| Entry                                                | Runs in | What it is                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| ---------------------------------------------------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `.` (`src/index.ts`)                                 | Browser | `createWallowSdk()` (`create-sdk.ts` — the **per-request** client factory; there is no module-global singleton), `login`/`logout`/`getUser` (`auth.ts`), `getCurrentUser`/consent+redirect arg builders (`auth-extras.ts`), OIDC URL builders (`auth-oidc.ts`), claim readers (`claims.ts`), the CSRF helper, `WallowRouterContext`/`requireAuth`/`loginRedirect` (`route-context.ts`), and the generated typed operations.                                                                                                           |
| `./server` (`src/server/`)                           | Node    | The BFF tunnel: `createWallowBffServer` (`bff-server.ts` — the golden-path preset that loads config, picks a store, and dispatches `handleBff`/`handleApi`/`handleHealth`), `loadBffConfigFromEnv` (`config.ts`), `createBffHandlers` (login/callback/logout/user), `createApiProxy` (`proxy.ts`), OIDC via **openid-client** (`oidc.ts`, auth code + PKCE, confidential client required), session sealing (`session.ts`, iron-webcrypto cookie **or** Valkey/Redis via `store/`), `WallowError`/`parseProblemDetails` (`errors.ts`). |
| `./server/passthrough` (`src/server/passthrough.ts`) | Node    | `createApiPassthrough` — the second golden-path topology: a pure reverse proxy owning no session, forwarding the upstream `Response` (and its `Set-Cookie`) verbatim. **Its own subpath so a passthrough-only app never pulls `openid-client` into its server bundle — nothing here may import the BFF handler/proxy graph.**                                                                                                                                                                                                         |
| `./query` (`src/query/`)                             | Browser | TanStack Query layer (**optional** peer dep `@tanstack/react-query`): re-exports the GENERATED per-operation artifacts (`{op}Options`, `{op}QueryKey`, `{op}Mutation` — emitted into `src/generated/@tanstack/`) plus one curated module, `invalidations.ts` (`queriesWithTag`, `queriesForOperation`), because generated keys are flat `[{ _id, baseUrl, tags, ...args }]` with no prefix to sweep by. The hand-written per-feature slices are **deleted** — `invalidations.ts` is the only hand-written file on this entry.         |

**The server entries are h3-free and must stay that way.** Every handler is a
web-standard `(request: Request) => Promise<Response>`; the SDK declares no host
framework and imports none. `src/server/h3-free.test.ts` pins this.

## Deleted surface — do not bring it back

The module-global client, the hand-written query slices and their `queryKeys` registry, the
`unwrap`/`createAuthClient`/`createMfaClient` layer, and the three per-app facade singletons
are **deleted outright**, not deprecated. Three things enforce that, and a change touching
this package should keep all three true:

- `src/index.test.ts` (`DELETED_LEGACY_SYMBOLS`) and `src/query/index.test.ts`
  (`RETIRED_EXPORTS`) assert the barrels still do NOT export those names.
- Root `.oxlintrc.json` carries `no-restricted-imports` entries banning them by name, plus
  deep imports into `@bc-solutions-coder/sdk/dist/**` / `src/**` and the deleted app facade
  paths. `src/oxlint-guardrails.test.ts` pins that config AND runs the real binary over
  violating and compliant snippets, so the rule cannot rot into matching nothing.
- The seam specs (`apps/**/src/features/*/api.test.ts`) namespace-import the query entry to
  assert absence, so they are the one place the lint rule is deliberately off.

Errors are the reason most of it went: every operation's failure path now surfaces a
`WallowError` through the response interceptor, so there is nothing left to unwrap.

## Session / CSRF model (server entry)

- Token set lives server-side in the session; `expiresAt` is **epoch milliseconds** (not
  Unix seconds); the proxy silently refreshes inside `EXPIRY_SKEW_MS`.
- CSRF: synchronizer token in `session.csrfToken` + non-HttpOnly double-submit cookie;
  browser echoes `x-csrf-token`; proxy validates on POST/PUT/PATCH/DELETE, GET/HEAD bypass.
- RFC 7807: the API puts the machine code in `extensions.code` — parse from there with an
  `UNKNOWN` fallback, never a top-level `code`.

## Generated OpenAPI client

- `src/generated/` is emitted by **@hey-api/openapi-ts** from the committed snapshot
  `openapi/v1.json` — never hand-edit either.
- Regenerate (API running on 5001), then commit both:
  `WALLOW_OPENAPI_URL=http://localhost:5001/openapi/v1.json pnpm --filter @bc-solutions-coder/sdk exec tsx scripts/generate.ts`
- CI compares the snapshot against the document the API **emits at build time**
  (`.github/actions/openapi-document`, shared by both workflows below), not against a
  live server. `openapi-drift.yml` fails a PR whose snapshot is stale;
  `openapi-autoregen.yml` picks the same condition up on `main` and opens a PR with the
  regenerated snapshot and client, so a backend merge never leaves the SDK behind.
  Its snapshot is byte-identical to what the manual refresh above produces.

## Build & publish

- `pnpm --filter @bc-solutions-coder/sdk build` = **Vite library mode** (ES, every
  non-relative import externalized) + `tsc -p tsconfig.build.json` for declarations.
  Apps typecheck against `dist/` — **build the SDK before dependent apps**. (No tsup;
  the README is stale on this.)
- This package is the **template all new workspace packages mirror** (exports map,
  build scripts, node-only subpath separation).
- Publishes to GitHub Packages on `sdk-v*` tags via `sdk-publish.yml`, independently of
  the platform release.

## Tests (vitest, node environment)

- Co-located `src/**/*.test.ts`; `vitest.config.ts` sets `mockReset: true` (Vitest 4 no
  longer resets `vi.fn` module-factory mocks itself).
- `openid-client` must be mocked in **both** `oidc.test.ts` and `handlers.test.ts`; the
  discovery cache in `oidc.ts` is keyed by metadata URL — use a unique issuer per test.
- Meta-specs guard the toolchain (`openapi-regen.test.ts`, `build-config.test.ts`) — update
  them when changing build/CI wiring. The publish workflow itself is NOT spec'd: `sdk-publish.yml`
  is validated by running, and the published package's exports map and type resolution are
  checked in CI by `publint` + `@arethetypeswrong/cli` (`pnpm check:exports`).
