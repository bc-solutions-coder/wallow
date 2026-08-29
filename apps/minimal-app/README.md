# example-minimal-app

The smallest reference app on Wallow's frontend golden path: a **TanStack Start**
app whose entire job is to wire the six shared `@bc-solutions-coder` packages
together. It owns one page route (`/`) that renders a hello card, plus the server
routes that passthrough-proxy the API; everything cross-cutting — styling,
components, the auth client, and the test harness — comes from the shared
packages, not from this app.

Use it as the copy-from skeleton when bootstrapping a new app. The step-by-step
rationale for each file lives in
[`docs/development/frontend-setup.md` → "New App Bootstrap"](../../docs/development/frontend-setup.md);
this README is the boot recipe.

## The six packages it wires

| Package                       | Published          | What this app pulls from it                                                                                                                                                                     |
| ----------------------------- | ------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `@bc-solutions-coder/styles`  | yes                | Tailwind v4 pipeline (`wallowStyles()` in `vite.config.ts`), brand theme tokens + assets (`src/styles.css`, `__root.tsx`)                                                                       |
| `@bc-solutions-coder/ui`      | **no (`private`)** | Shared components (`Card`, `MutedText`, `CenteredCardLayout`, `ForkAttribution`, `DocumentStyles`, `FocusOnNavigate`, `ReadyIndicator`) + its Tailwind `@source` scan                           |
| `@bc-solutions-coder/sdk`     | yes                | `createWallowSdk` and `createRequestOriginResolver` (`src/start.ts`), the `createApiPassthrough` server preset (`src/lib/api-passthrough.ts`), and the generated `./query` TanStack Query layer |
| `@bc-solutions-coder/testing` | **no (`private`)** | The `createVitestProjects` node+browser preset (`vitest.config.ts`) and the browser-mode `render` helper (`*.test.tsx`)                                                                         |
| `@bc-solutions-coder/query`   | **no (`private`)** | `createQueryClient` — the router's `QueryClient` factory — and every react-query symbol the app uses, re-exported from the one facade                                                           |
| `@bc-solutions-coder/env`     | **no (`private`)** | `resolveInternalOrigin` (`src/start.ts`) — the deployment-derived addressing every Start host needs                                                                                             |

> **Copy-outside-the-monorepo caveat:** only `@bc-solutions-coder/sdk` and
> `@bc-solutions-coder/styles` are published to GitHub Packages. `ui`, `testing`,
> `query` and `env` are `private` workspace packages — the `workspace:*` deps below
> resolve in-repo but would NOT resolve if this directory were lifted out of the
> monorepo. A fork extends the repo in place rather than copying this folder out.

## Boot it

All commands run from the repo root. Node 24 (`.nvmrc`), pnpm 11.24.0.

```bash
pnpm install                                                   # resolves the workspace:* deps
```

That is the whole setup — no package build first. In-repo every `@bc-solutions-coder/*` exports
map resolves to that package's `src/`, so this app runs, typechecks and builds straight from the
sources. `dist/` is a publish artifact, needed only by `pnpm check:exports`.

There is no `routes:generate` step: the `tanstackStart()` Vite plugin regenerates
`src/routeTree.gen.ts` as a side effect of both `vite dev` and `vite build`.

### Dev (`pnpm dev`)

```bash
pnpm --filter @bc-solutions-coder/example-minimal-app dev      # http://localhost:3010
```

Boots the Start dev server. It needs no backend — the API passthrough is
constructed lazily, so only actual `/v1`, `/connect`, and `/.well-known` requests
reach the API. Override the port with `PORT`; point the API surface elsewhere
with `WALLOW_API_INTERNAL_URL` (default `http://localhost:5001`).

### Production (`pnpm build` + `pnpm start`)

```bash
pnpm --filter @bc-solutions-coder/example-minimal-app build    # .output/server + .output/public (Nitro)
pnpm --filter @bc-solutions-coder/example-minimal-app start    # node .output/server/index.mjs on :3010
```

`GET /health` returns `ready`; `/` server-renders the hello card and hydrates
(`document.body` gains `data-app-ready="true"` once hydrated).

## Verify it

```bash
pnpm --filter @bc-solutions-coder/example-minimal-app typecheck
pnpm --filter @bc-solutions-coder/example-minimal-app test      # node + headless-Chromium vitest projects
```

## What this app owns vs. inherits

- **Owns:** the route tree (`src/routes/`) — the page route, the `/health` probe,
  and the three passthrough server routes (`v1/$.ts`, `connect/$.ts`,
  `[.]well-known/$.ts`, all delegating to `src/lib/api-passthrough.ts`); the
  per-request SDK middleware (`src/start.ts`); the router factory
  (`src/router.tsx`); and the little UI it does not inherit —
  `src/features/hello/HelloCard.tsx` and `src/components/ready-indicator.tsx`, the
  app's wrapper over the catalog's `ReadyIndicator`. Note that a `features/`
  directory is not what "zoned" means: this app is un-zoned because its
  `tsconfig.json` declares no `paths` alias map, so `wallow/zone-dag` has no DAG to
  judge — not because it has no feature folders.
- **Inherits (no source of its own):** branding/theme tokens, the component
  library, the test harness, and the whole host runtime — the SSR server, the dev
  server, and the production Node server are Start + Nitro output, not app code.
  Rebranding needs no source change here; it flows from `packages/styles/branding.json`
  through `@bc-solutions-coder/styles`.

### Two config lines worth copying verbatim

`vite.config.ts` carries two non-obvious settings that every Start app in this
repo needs, both explained in comments there:

- an explicit `server.port` — `vite dev` binds 3000 when `PORT` is unset;
- `environments.client.build.copyPublicDir: true` — `nitro/vite` forces that flag
  off, which silently drops the shared brand assets and 404s `/piggy-icon.svg` in
  the built output.

Also note `src/routes/__root.tsx` imports `../styles.css` for its **side effect**
rather than as a `?url` + `head()` link: Start builds two Vite environments, and a
`?url` import resolved in the SSR graph names a hash the client build never emits.

## Adding your first query

This app renders **no live data on purpose** — `HelloCard` is static, so there is
no demo fetch to read past. What it does ship is the wiring your first query
needs, already in place:

- `src/start.ts` mints ONE SDK instance per request with `createWallowSdk`,
  giving it the request's own origin, cookie header, and CSRF interceptor. There
  is no module-global client to configure and nothing to bootstrap before first
  use.
- `src/router.tsx` lifts that instance into the router context (falling back to a
  browser-side instance when there is no request), so every route and component
  reads it from context rather than importing a facade.
- `createQueryClient` (from `@bc-solutions-coder/query`) already supplies the
  router's `QueryClient`.

So a fork adds a read by calling the generated options factory for the operation
and binding it to the context SDK:

```tsx
import { useQuery } from "@bc-solutions-coder/query";
import { usersGetCurrentUserOptions } from "@bc-solutions-coder/sdk/query";
import { useRouteContext } from "@tanstack/react-router";

const { sdk } = useRouteContext({ from: "__root__" });
const { data } = useQuery(usersGetCurrentUserOptions({ client: sdk.client }));
```

Note where each half of that snippet comes from. The hooks (`useQuery`,
`useMutation`, `QueryClient`, …) come from `@bc-solutions-coder/query` — the
workspace's single TanStack Query facade. An app never depends on
`@tanstack/react-query` itself: only the facade does, so the whole graph shares one
copy of the library and therefore one `QueryClientProvider` context.

Reads come from `@bc-solutions-coder/sdk`'s **`./query`** entry, and every
artifact on it is **generated** from the OpenAPI document — an `{op}Options()`,
`{op}QueryKey()` and `{op}Mutation()` per operation. Never hand-write a `queryKey`
literal or a query factory in an app; if an operation lacks one, regenerate. Keys
are flat (`[{ _id, baseUrl, tags, ...args }]`) with no prefix to sweep by, so
invalidate through the curated `queriesWithTag` / `queriesForOperation`
predicates. Keep UI-only state (open/closed, active tab, wizard step) in a Zustand
store rather than the query cache. The full boundary — what belongs in TanStack
Query, what belongs in Zustand, and what belongs in neither — is
[`docs/development/frontend-state.md`](../../docs/development/frontend-state.md).
