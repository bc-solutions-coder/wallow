# apps — Frontend Applications Agent Guide

Every app here is a **TanStack Start** frontend consuming the five `@bc-solutions-coder`
workspace packages (`sdk`, `styles`, `ui`, `web-shell`, `testing`) via `workspace:*`.

| App                     | Port | What it is                                                               |
| ----------------------- | ---- | ------------------------------------------------------------------------ |
| `wallow-web/`           | 3000 | Reference dashboard demonstrating the full same-origin BFF OIDC flow.    |
| `wallow-auth/`          | 3002 | Auth frontend — login / signup / MFA screens.                            |
| `examples/minimal-app/` | 3010 | Smallest app wiring all five shared packages into a TanStack Start host. |

**Build the SDK before touching an app** — apps typecheck against `packages/sdk/dist/`:
`pnpm --filter @bc-solutions-coder/sdk build`.

Per-app scripts (`pnpm --filter ./apps/<app> <script>`): `dev` (`vite dev`), `build`
(`vite build` → `.output/server/index.mjs` + `.output/public`), `start`
(`node .output/server/index.mjs` — what the Dockerfiles and E2E containers run),
`typecheck`, `test`.

- **Hosting is per-app and owned by Start.** Each app has one `vite.config.ts`
  (`tanstackStart` + `react` + `nitro` + `wallowStyles`) and no host files: `server.ts`,
  `dev-server.ts`, `vite.ssr.config.ts`, and the web-shell `./server` presets are all
  deleted. Backend-facing surface = **server routes** under `src/routes/**` delegating to
  an SDK preset (`createApiPassthrough` for wallow-auth/minimal-app, `createWallowBffServer`
  for wallow-web). `src/routeTree.gen.ts` regenerates as a side effect of `vite dev`/`vite
build` — never hand-edit it, and do not add a `routes:generate` script or `tsr.config.json`.
- Every app spells out `server.port` in its `vite.config.ts` (`vite dev` binds 3000 when
  `PORT` is unset) and pins `@tanstack/react-start`/`react-router`/`react-router-ssr-query`
  exactly, with no `^`.

- **Tests**: `test` is vitest with the two-project node/browser split from
  `@bc-solutions-coder/testing`; component specs run in real headless Chromium, never jsdom.
  See `.claude/rules/TESTING.md`.
- **E2E**: `test:e2e` (Playwright, per-app `e2e/`) and, for wallow-web only,
  `test:e2e:cross-app` (`e2e-cross-app/`, needs an externally supplied three-origin stack).
  Read `.claude/rules/E2E.md` before editing anything under `e2e/`.
- `wallow-web` and `wallow-auth` each ship a `Dockerfile` whose build context is the **repo
  root** — the whole workspace is needed to resolve `workspace:*`.
