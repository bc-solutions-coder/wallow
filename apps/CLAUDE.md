# apps — Frontend Applications Agent Guide

Every app here is a **TanStack Start** frontend consuming the seven `@bc-solutions-coder`
workspace packages (`sdk`, `styles`, `ui`, `forms`, `query`, `auth`, `testing`) via
`workspace:*`. `forms` and `auth` are the optional ones — `examples/minimal-app` renders no
form and has no signed-in user, so it omits both.

| App                     | Port | What it is                                                                    |
| ----------------------- | ---- | ----------------------------------------------------------------------------- |
| `wallow-web/`           | 3000 | Reference dashboard demonstrating the full same-origin BFF OIDC flow.         |
| `wallow-auth/`          | 3002 | Auth frontend — login / signup / MFA screens.                                 |
| `examples/minimal-app/` | 3010 | Smallest app wiring the five core shared packages into a TanStack Start host. |

**Build the SDK before touching an app** — apps typecheck against `packages/sdk/dist/`:
`pnpm --filter @bc-solutions-coder/sdk build`.

Per-app scripts (`pnpm --filter ./apps/<app> <script>`): `dev` (`vite dev`), `build`
(`vite build` → `.output/server/index.mjs` + `.output/public`), `start`
(`node .output/server/index.mjs` — what the Dockerfiles and E2E containers run),
`typecheck`, `test`.

- **Hosting is per-app and owned by Start.** Each app has one `vite.config.ts`
  (`tanstackStart` + `react` + `nitro` + `wallowStyles`) and no host files: `server.ts`,
  `dev-server.ts`, `vite.ssr.config.ts`, and the hand-rolled host-runtime `./server` presets
  the deleted shared frontend-runtime package used to ship are all gone. Backend-facing surface = **server routes** under `src/app/routes/**` delegating to
  an SDK preset (`createApiPassthrough` for wallow-auth/minimal-app, `createWallowBffServer`
  for wallow-web). `src/app/routeTree.gen.ts` regenerates as a side effect of `vite dev`/`vite
build` — never hand-edit it, and do not add a `routes:generate` script or `tsr.config.json`.
- **`wallow-web` and `wallow-auth` are zoned; `examples/minimal-app` is deliberately not.**
  In the two zoned apps `src/` is `app/` (routes, router, entries, server-only modules),
  `features/<name>/` (one directory per screen or vertical, reachable only through its
  `index.ts` barrel) and `shared/` (limited to `components`, `hooks`, `lib`, `stores`,
  `testing`, `types`). Cross-zone imports are spelled as aliases — `@app/*`,
  `@features/<name>`, `@shared/*` — declared **once**, in the app's `tsconfig.json` `paths`.
  Vite reads it natively (`resolve.tsconfigPaths: true`), vitest reads it inside each
  `test.projects` entry, and `src/zone-dag.test.ts` reads it to derive which prefixes it
  polices — so adding a zone is that one edit. Relative specifiers stay correct
  _within_ a zone. The DAG itself is enforced by a spec, not convention:
  `src/zone-dag.test.ts` resolves every specifier against its importer's real directory and
  judges the edge. Two consequences worth knowing before you move a file: server-only modules
  belong in `app/` (that is what keeps `node:crypto`/`openid-client` out of the client graph),
  and `srcDirectory: "src/app"` in `vite.config.ts` must be paired with
  `importProtection: { include: ["src/**"] }` or Start scopes its env-boundary check to
  `src/app` alone and silently stops checking `features/` and `shared/`. That pairing sets
  only the importer SCOPE. What actually gets denied is the **filename**: Start's default
  client rule blocks an imported file matching `**/*.server.*` and knows nothing about
  `redis` or `node:crypto`, so every server-only module is named `*.server.*`
  (`wallow-web/src/app/lib/bff.server.ts`, `wallow-auth/src/shared/lib/api-passthrough.server.ts`)
  and `src/server-only-naming.test.ts` — byte-identical in both apps — keeps it that way.
- Every app spells out `server.port` in its `vite.config.ts` (`vite dev` binds 3000 when
  `PORT` is unset). `@tanstack/react-start`/`react-router`/`react-router-ssr-query` are still
  pinned exactly, but the pin now lives in the **`start` catalog** in `pnpm-workspace.yaml`:
  app manifests say `"catalog:start"`, and the exact version is edited in one place. Ranged
  shared deps (`react`, `react-dom`, `@tanstack/react-form`/`react-query`, `zustand`) come from
  the sibling **`react` catalog**. Do not collapse the two — a library peering
  `@tanstack/react-router@^1.170.18` against an app pinning `1.170.18` exactly is correct
  practice, not drift, and stays a literal.

- **Tests**: `test` is vitest with the two-project node/browser split from
  `@bc-solutions-coder/testing`; component specs run in real headless Chromium, never jsdom.
  See `.claude/rules/TESTING.md`.
- **E2E**: `test:e2e` (Playwright, per-app `e2e/`) and, for wallow-web only,
  `test:e2e:cross-app` (`e2e-cross-app/`, needs an externally supplied three-origin stack).
  Read `.claude/rules/E2E.md` before editing anything under `e2e/`.
- `wallow-web` and `wallow-auth` each ship a `Dockerfile` whose build context is the **repo
  root** — the whole workspace is needed to resolve `workspace:*`.
