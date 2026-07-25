# packages/sdk — @bc-solutions-coder/sdk Agent Guide

The TypeScript **BFF auth SDK**: the browser never holds a token; a Node-side tunnel owns
the OIDC session and proxies API calls with a bearer attached.

## Three entry points (exports map → `dist/`)

| Entry                      | Runs in   | What it is                                                                                                                                                                                                                                                                                                                                                                |
| -------------------------- | --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `.` (`src/index.ts`)       | Browser   | `configureBffClient()` (`client.ts` — module-level `createClient()` singleton, `credentials:'include'`), `login`/`logout`/`getUser` (`auth.ts`), CSRF helper, MFA client, SSR helpers (`ssr.ts`), and the generated typed operations.                                                                                                                                     |
| `./server` (`src/server/`) | Node (h3) | The BFF tunnel: `loadBffConfigFromEnv` (`config.ts`), `createBffHandlers` (login/callback/logout/user), `createApiProxy` (`proxy.ts`), OIDC via **openid-client** (`oidc.ts`, auth code + PKCE, confidential client required), session sealing (`session.ts`, iron-webcrypto cookie **or** Valkey/Redis via `store/`), `WallowError`/`parseProblemDetails` (`errors.ts`). |
| `./query` (`src/query/`)   | Browser   | TanStack Query layer (peer dep `@tanstack/react-query`): per-feature `queryOptions`/`mutationOptions` slices (`user`, `auth`, `mfa`, `organizations`, `apps`, `keys`, `inquiries`, `settings`, `bootstrap`) sharing one key registry.                                                                                                                                     |

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
- CI `openapi-drift.yml` fails if the snapshot drifts from a live API.

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
- Meta-specs guard the toolchain (`openapi-regen.test.ts`, `sdk-publish-workflow.test.ts`,
  `build-config.test.ts`) — update them when changing build/CI wiring.
