# packages/sdk — @bc-solutions-coder/sdk Agent Guide

The TypeScript **BFF auth SDK**: the browser never holds a token; a Node-side tunnel owns
the OIDC session and proxies API calls with a bearer attached.

## Four entry points (exports map → `src/`; `dist/` only on publish)

| Entry                                                | Runs in | What it is                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| ---------------------------------------------------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `.` (`src/index.ts`)                                 | Browser | `createWallowSdk()` (`create-sdk.ts` — **per-request** client factory; no module-global singleton), `logout` (`auth.ts` — the ONE imperative navigation helper: `/bff/logout` is CSRF-gated, so it cannot be a link the way login is), `getCurrentUser`/consent+redirect arg builders (`auth-extras.ts`), OIDC URL builders (`auth-oidc.ts`), the CSRF helper, `requireAuth`/`loginRedirect` (`route-context.ts`), and the generated typed operations.                                                                    |
| `./server` (`src/server/`)                           | Node    | The BFF tunnel: `createWallowBffServer` (`bff-server.ts` — golden-path preset: loads config, picks a store, dispatches `handleBff`/`handleApi`/`handleHealth`), `loadBffConfigFromEnv` (`config.ts`), `createBffHandlers` (login/callback/logout/user), `createApiProxy` (`proxy.ts`), OIDC via **openid-client** (`oidc.ts`, auth code + PKCE, confidential client required), session sealing (`session.ts`, iron-webcrypto cookie **or** Valkey/Redis via `store/`), `WallowError`/`parseProblemDetails` (`errors.ts`). |
| `./server/passthrough` (`src/server/passthrough.ts`) | Node    | `createApiPassthrough` — the second golden-path topology: a pure reverse proxy owning no session, forwarding the upstream `Response` (and its `Set-Cookie`) verbatim. **Its own subpath so a passthrough-only app never pulls `openid-client` into its server bundle — nothing here may import the BFF handler/proxy graph.**                                                                                                                                                                                             |
| `./query` (`src/query/`)                             | Browser | TanStack Query layer (**optional** peer dep `@tanstack/react-query`): re-exports the GENERATED per-operation artifacts (`{op}Options`, `{op}QueryKey`, `{op}Mutation` — emitted into `src/generated/@tanstack/`) plus the one hand-written module, `invalidations.ts` (`queriesWithTag`, `queriesForOperation`), because generated keys are flat `[{ _id, baseUrl, tags, ...args }]` with no prefix to sweep by.                                                                                                          |

**The server entries are h3-free and must stay that way.** Every handler is a web-standard
`(request: Request) => Promise<Response>`; the SDK declares no host framework and imports
none. `src/server/web-standard-handlers.test.ts` pins it behaviourally — it CALLS each
handler with a real `Request` and asserts a real `Response` comes back.

## Deleted surface — do not bring it back

Banned outright, not deprecated: the module-global client; the hand-written query slices and
their `queryKeys` registry; the `unwrap`/`createAuthClient`/`createMfaClient` layer; the
per-app facade singletons; the imperative `login()`/`getUser()` browser helpers (use
`loginRedirect()` links and `getCurrentUser`/`currentUserQuery`; only the CSRF-gated
`logout()` stays imperative); the browser claim-bag readers (`claims.ts` — apps read the
typed `CurrentUser` through `@bc-solutions-coder/auth`'s `hasRole`/`hasPermission`/`isAdmin`;
id_token decoding is the server entry's own `server/claims.ts`); and the module-scope CSRF
token store (`setCsrfToken`/`getCsrfToken` — the interceptor and `logout()` read the
double-submit cookie directly via `readCsrfCookie`). Three guards hold this, and a change
here must keep all three true:

- `src/index.test.ts` (`DELETED_LEGACY_SYMBOLS`) and `src/query/index.test.ts`
  (`RETIRED_EXPORTS`) assert the barrels do NOT export those names.
- Root `.oxlintrc.json` bans them by name via `no-restricted-imports`, plus deep imports into
  `@bc-solutions-coder/sdk/dist/**` / `src/**` and the deleted app facade paths.
  `src/oxlint-guardrails.test.ts` pins that config AND runs the real binary over violating
  and compliant snippets, so the rule cannot rot into matching nothing.
- The seam specs (`apps/**/src/features/*/api.test.ts`) namespace-import the query entry to
  assert absence — the one place the lint rule is deliberately off.

## Session / CSRF model (server entry)

- Token set lives server-side in the session; `expiresAt` is **epoch milliseconds** (not
  Unix seconds); the proxy silently refreshes inside `EXPIRY_SKEW_MS`.
- CSRF: synchronizer token in `session.csrfToken` + non-HttpOnly double-submit cookie;
  browser echoes `x-csrf-token`; proxy validates on POST/PUT/PATCH/DELETE, GET/HEAD bypass.
  Browser-side the cookie is the ONE token source — the interceptor reads it live per request
  (`readCsrfCookie`), and `createWallowSdk({ csrf: false })` skips the interceptor for a
  passthrough topology (wallow-auth), which has no token of its own to stamp.
- RFC 7807: the API puts the machine code in `extensions.code` — parse from there with an
  `UNKNOWN` fallback, never a top-level `code`.

## Generated OpenAPI client

- `src/generated/` is emitted by **@hey-api/openapi-ts** from the committed snapshot
  `openapi/v1.json` — never hand-edit either.
- Regenerate (API running on 5001), then commit both:
  `WALLOW_OPENAPI_URL=http://localhost:5001/openapi/v1.json pnpm --filter @bc-solutions-coder/sdk exec tsx scripts/generate.ts`
- CI compares the snapshot against the document the API **emits at build time**
  (`.github/actions/openapi-document`), not a live server. `openapi-drift.yml` fails a PR
  whose snapshot is stale; `openapi-autoregen.yml` picks the same condition up on `main` and
  opens a PR with the regenerated snapshot and client. Its output is byte-identical to the
  manual refresh above.

## Build & publish

- `pnpm --filter @bc-solutions-coder/sdk build` = **Vite library mode** (ES, every
  non-relative import externalized) + `tsc -p tsconfig.build.json` for declarations.
  Dependent apps do **not** need this build — in-repo the `exports` map resolves to `src/`,
  and `publishConfig.exports` swaps in the `dist/` map at pack time. The build exists for
  publishing and for `pnpm check:exports` (publint + attw), which is why `pnpm build`
  precedes it in the `pnpm check` chain.
- This package is the **template all new workspace packages mirror** (exports map, build
  scripts, node-only subpath separation).
- Publishes to GitHub Packages on `sdk-v*` tags via `sdk-publish.yml`, independently of the
  platform release.

## Tests (vitest, node environment)

- Co-located `src/**/*.test.ts`; `vitest.config.ts` sets `mockReset: true` (Vitest 4 no
  longer resets `vi.fn` module-factory mocks itself).
- `openid-client` must be mocked in **both** `oidc.test.ts` and `handlers.test.ts`; the
  discovery cache in `oidc.ts` is keyed by metadata URL — use a unique issuer per test.
- One meta-spec guards the toolchain (`openapi-regen.test.ts`) — update it when changing
  build/CI wiring. The bundler wiring and the publish workflow are deliberately NOT spec'd;
  the published exports map and type resolution are checked by `pnpm check:exports` in CI.
