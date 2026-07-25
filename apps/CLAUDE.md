# apps — Frontend Applications Agent Guide

Every app here is a **TanStack Start** frontend consuming the five `@bc-solutions-coder`
workspace packages (`sdk`, `styles`, `ui`, `web-shell`, `testing`) via `workspace:*`.

| App                     | Port | What it is                                                                       |
| ----------------------- | ---- | -------------------------------------------------------------------------------- |
| `wallow-web/`           | 3000 | Reference dashboard demonstrating the full same-origin BFF OIDC flow.            |
| `wallow-auth/`          | 3002 | Auth frontend — login / signup / MFA screens.                                    |
| `examples/minimal-app/` | —    | Smallest app wiring all five shared packages through the web-shell host factory. |

**Build the SDK before touching an app** — apps typecheck against `packages/sdk/dist/`:
`pnpm --filter @bc-solutions-coder/sdk build`.

Per-app scripts (`pnpm --filter ./apps/<app> <script>`): `dev` (tsx dev-server), `build`
(client + SSR Vite passes), `start` (standalone h3 host used by the E2E containers),
`routes:generate` (`tsr generate` — rerun after adding a route file), `typecheck`, `test`.

- **Tests**: `test` is vitest with the two-project node/browser split from
  `@bc-solutions-coder/testing`; component specs run in real headless Chromium, never jsdom.
  See `.claude/rules/TESTING.md`.
- **E2E**: `test:e2e` (Playwright, per-app `e2e/`) and, for wallow-web only,
  `test:e2e:cross-app` (`e2e-cross-app/`, needs an externally supplied three-origin stack).
  Read `.claude/rules/E2E.md` before editing anything under `e2e/`.
- `wallow-web` and `wallow-auth` each ship a `Dockerfile` whose build context is the **repo
  root** — the whole workspace is needed to resolve `workspace:*`.
