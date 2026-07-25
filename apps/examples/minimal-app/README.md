# example-minimal-app

The smallest reference app on Wallow's frontend golden path: a TanStack Start app
whose entire job is to wire the five shared `@bc-solutions-coder` packages
together through the web-shell host factory. It owns one route (`/`) that renders
a hello card; everything cross-cutting — styling, components, the auth client, the
test harness, and the SSR/dev/standalone host runtime — comes from the shared
packages, not from this app.

Use it as the copy-from skeleton when bootstrapping a new app. The step-by-step
rationale for each file lives in
[`docs/development/frontend-setup.md` → "New App Bootstrap"](../../docs/development/frontend-setup.md);
this README is the boot recipe.

## The five packages it wires

| Package                         | Published          | What this app pulls from it                                                                                                                                           |
| ------------------------------- | ------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `@bc-solutions-coder/styles`    | yes                | Tailwind v4 pipeline (`wallowStyles()`, via the web-shell Vite preset), brand theme tokens + assets (`src/styles.css`, `__root.tsx`)                                  |
| `@bc-solutions-coder/ui`        | **no (`private`)** | Shared components (`Card`, `MutedText`, `CenteredCardLayout`, `ForkAttribution`, `DocumentStyles`, `FocusOnNavigate`, `ReadyIndicator`) + its Tailwind `@source` scan |
| `@bc-solutions-coder/sdk`       | yes                | Same-origin BFF client + typed API operations (`src/lib/sdk.ts`)                                                                                                      |
| `@bc-solutions-coder/testing`   | **no (`private`)** | The `createVitestProjects` node+browser preset (`vitest.config.ts`) and the browser-mode `render` helper (`*.test.tsx`)                                               |
| `@bc-solutions-coder/web-shell` | **no (`private`)** | `createQueryClient`, and (`./server`) the host/dev-server/Vite-preset factories (`server.ts`, `dev-server.ts`, `vite.config.ts`, `vite.ssr.config.ts`)                |

> **Copy-outside-the-monorepo caveat:** only `@bc-solutions-coder/sdk` and
> `@bc-solutions-coder/styles` are published to GitHub Packages. `ui`, `testing`,
> and `web-shell` are `private` workspace packages — the `workspace:*` deps below
> resolve in-repo but would NOT resolve if this directory were lifted out of the
> monorepo. A fork extends the repo in place rather than copying this folder out.

## Boot it

All commands run from the repo root. Node 24 (`.nvmrc`), pnpm 10.20.0.

```bash
pnpm install                                                   # resolves the workspace:* deps
pnpm --filter @bc-solutions-coder/sdk build                    # apps typecheck/build against the SDK's dist/
pnpm --filter @bc-solutions-coder/example-minimal-app routes:generate   # writes src/routeTree.gen.ts (also emitted by the build)
```

### Dev (`pnpm dev`)

```bash
pnpm --filter @bc-solutions-coder/example-minimal-app dev      # http://localhost:3010
```

Boots a Vite SSR dev server. It needs no backend — the reverse proxy is loaded
lazily and only actual `/v1`/`/connect` requests reach the API. Override the port
with `PORT`; point the API surface elsewhere with `WALLOW_API_INTERNAL_URL`
(default `http://localhost:5001`).

### Production (`pnpm build` + `pnpm start`)

```bash
pnpm --filter @bc-solutions-coder/example-minimal-app build    # dist/client + dist/server
pnpm --filter @bc-solutions-coder/example-minimal-app start    # standalone SSR host on :3010
```

`GET /health` returns `ready`; `/` server-renders the hello card and hydrates
(the `data-testid="app-ready"` element flips to `true` once hydrated).

## Verify it

```bash
pnpm --filter @bc-solutions-coder/example-minimal-app typecheck
pnpm --filter @bc-solutions-coder/example-minimal-app test      # node + headless-Chromium vitest projects
```

## What this app owns vs. inherits

- **Owns:** the router and routes (`src/routes/`), the proxy topology
  (`src/lib/proxy-paths.ts` + `src/lib/proxy-server.ts` — a same-origin reverse
  proxy, the simpler of the two golden-path topologies), and the SDK facade
  (`src/lib/sdk.ts`).
- **Inherits (no source of its own):** branding/theme tokens, the component
  library, the test harness, and the host/dev/Vite runtime — all from the shared
  packages. Rebranding needs no source change here; it flows from
  `api/branding.json` through `@bc-solutions-coder/styles`.
