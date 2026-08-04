# Integration Cookbook: New Fork, New App

A start-to-finish recipe for standing up a new TanStack Start frontend against Wallow —
from `npm install` to a working feature reading and writing module data through the BFF.

This page is the connective tissue between guides that each own one piece:

| For…                                                  | Read                                                                                                          |
| ----------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| The full Vite/Vitest/Tailwind wiring of a new app     | [Frontend Setup](../development/frontend-setup.md)                                                            |
| The SDK's entry points, env vars, and session model   | [TypeScript SDK](typescript-sdk.md)                                                                           |
| The query/Zustand boundary and how to add a query     | [Frontend State](../development/frontend-state.md)                                                            |
| The wire protocol, if you are writing a BFF elsewhere | [BFF Pattern](bff-pattern.md)                                                                                 |
| Why the platform mandates a BFF at all                | [Fork Guide → Frontend auth policy](../getting-started/fork-guide.md#frontend-authentication-policy-bff-only) |

Six steps. Each one names the reference file in this repository that already does it, so
you can read a working version rather than trusting a snippet.

---

## 1. Install

**Inside a fork of this repository**, a new app under `apps/` depends on the core workspace
packages as `workspace:*` runtime dependencies — see
[Frontend Setup → Depend on the core packages](../development/frontend-setup.md#1-depend-on-the-core-packages).

**Outside the workspace**, packages come from GitHub Packages under the repository owner's scope,
so point the scope at that registry in a project `.npmrc`:

```ini
@bc-solutions-coder:registry=https://npm.pkg.github.com
```

The credential goes in your **user-level** config, not that file — pnpm will not expand
`${GITHUB_TOKEN}` out of a committed project `.npmrc`, because such a file could be edited to
redirect the registry:

```bash
npm config set "//npm.pkg.github.com/:_authToken" "$GITHUB_TOKEN"   # or: pnpm config set …
npm install @bc-solutions-coder/sdk
```

> [!IMPORTANT]
> **Only `@bc-solutions-coder/sdk` is published today.** `sdk-publish.yml` is scoped to
> `packages/sdk` and fires on an `sdk-v*` tag; no workflow publishes
> `@bc-solutions-coder/styles`, and its `package.json` still reads `"version": "0.0.0"`, so
> `npm install @bc-solutions-coder/styles` returns a 404. Nothing about the package prevents
> publication — unlike the other workspace packages it is not marked `"private": true` — it simply
> has no release pipeline yet. Until it gets one, an out-of-workspace app supplies its own Tailwind
> setup and copies the theme tokens it needs from `packages/styles/branding.json`.

The token needs only `read:packages`. The SDK carries no host-framework dependency: its server handlers are
web-standard `(Request) => Promise<Response>` functions, so they mount on TanStack Start,
Nitro, Hono, or a bare Fetch handler alike.

## 2. Add `tanstackStart()` to `vite.config.ts`

One config serves dev and builds production; there is no `server.ts`, no `dev-server.ts`, and
no second SSR config. Compose the plugins in this order and set the port explicitly, because
`vite dev` binds 3000 whenever `PORT` is unset:

```ts
import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { tanstackStart } from "@tanstack/react-start/plugin/vite";
import react from "@vitejs/plugin-react";
import { nitro } from "nitro/vite";
import { defineConfig } from "vite";

export default defineConfig({
  server: { port: Number(process.env.PORT ?? 3010) },
  plugins: [
    tanstackStart({
      // Specs are co-located, so a *.test.tsx under src/routes/ would otherwise
      // be codegen'd in as a route.
      router: { routeFileIgnorePattern: String.raw`\.(test|spec)\.(ts|tsx)$` },
    }),
    react(),
    nitro(),
    ...wallowStyles(),
  ],
});
```

The `routeFileIgnorePattern` is the part not to drop: without it, every co-located spec file
under `src/routes/` is compiled in as a route. `wallowStyles()` returns an array of plugins and
is **spread**, not nested — Vite flattens either form, but every app in this repository spreads it.

`src/routeTree.gen.ts` regenerates as a side effect of `vite dev` and `vite build` — never
hand-edit it, and do not add a `routes:generate` script. The complete config, including the
`copyPublicDir` workaround Nitro needs, is in
[Frontend Setup → Vite config](../development/frontend-setup.md#3-vite-config-viteconfigts).

This config is hand-rolled on purpose. Apps *inside* the workspace spread
`wallowAppConfig({ defaultPort })` from `@bc-solutions-coder/config/vite/app` instead and get all
of this for free — but that package is `"private": true` and is never published, so it cannot
reach the out-of-workspace audience this page is written for. The same applies to the
`wallowStyles()` plugin above: it is available in a fork of this repository and, per the note in
step 1, not yet installable outside one. **Working inside a fork? Use the preset** and read
[Frontend Setup](../development/frontend-setup.md#3-vite-config-viteconfigts) rather than this
block.

## 3. Mount the splat server routes

Pick the topology first, because it decides which SDK preset sits behind your routes:

| Topology             | Preset                                                                  | Use it when                                                                                                                                                |
| -------------------- | ----------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **BFF tunnel**       | `createWallowBffServer()` (`@bc-solutions-coder/sdk/server`)            | The app signs users in and calls the API on their behalf. Owns an OIDC session; mounts `/bff/**` and `/api/**`                                             |
| **Pure passthrough** | `createApiPassthrough()` (`@bc-solutions-coder/sdk/server/passthrough`) | The app only needs the API on its own origin with no session of its own — an auth frontend, for example. Mounts `/v1/**`, `/connect/**`, `/.well-known/**` |

Either way, each prefix is one splat route with a **single `ANY` handler**. A method map
would swallow the preset's own method policy (a bare `GET /bff/logout` answers `405` with
`Allow: POST`) as a local 404:

```ts
// src/routes/bff/$.ts
import { createFileRoute } from "@tanstack/react-router";

export const Route = createFileRoute("/bff/$")({
  server: {
    handlers: {
      // Dynamic import: this preset pulls node:crypto + openid-client, and every
      // route module is a member of the tree the CLIENT graph also imports.
      ANY: async ({ request }) => (await import("../../lib/bff.server")).handleBffRequest(request),
    },
  },
});
```

Build the preset **lazily and memoise it at module scope** so importing a route module does
not construct it, and so a config throw cannot take down SSR at bundle-evaluation time.
`apps/wallow-web/src/app/lib/bff.server.ts` (BFF) and `apps/wallow-auth/src/shared/lib/api-passthrough.server.ts`
(passthrough) are the reference implementations; the environment variables each preset reads
are tabulated in [TypeScript SDK → Environment variables](typescript-sdk.md#environment-variables).

## 4. Mint one SDK per request in `src/start.ts`

Server-rendering an authenticated route needs two things a browser tab gets for free: an
absolute origin, because Node's `fetch` cannot resolve a relative `/api`, and the inbound
session cookie, because Node has no cookie jar. Both are request-scoped, and both are
constructor arguments to `createWallowSdk()`:

```ts
// src/start.ts
import { createWallowSdk, type WallowSdk } from "@bc-solutions-coder/sdk";
import { createMiddleware, createStart } from "@tanstack/react-start";

const sdkMiddleware = createMiddleware().server(({ next, request }) => {
  const origin: string = new URL(request.url).origin;

  const sdk: WallowSdk = createWallowSdk({
    baseUrl: `${origin}/api`,
    cookieHeader: request.headers.get("cookie") ?? undefined,
    internalOrigin: process.env.WALLOW_WEB_INTERNAL_URL,
  });

  return next({ context: { sdk } });
});

export const startInstance = createStart(() => ({ requestMiddleware: [sdkMiddleware] }));
```

`getRouter()` then lifts that instance into the router context, falling back to a same-origin
browser instance when there is no request in scope:

```ts
// src/router.tsx
const sdk: WallowSdk = readRequestSdk() ?? createWallowSdk({ baseUrl: "/api" });
const router = createTanStackRouter({ routeTree, context: { queryClient, sdk } });
```

Per **request**, never per module. A module-global client is safe in a browser — one document,
one session — and wrong on a server, where concurrent renders share the module graph: the last
request to configure it wins, and its cookie leaks into another user's render. That is why the
old `configureBffClient()` / `configureSsrClient()` / `client` exports are deleted outright
rather than deprecated, and why reaching for one is now a lint error (see
[step 6](#6-the-patterns-that-are-gone)).

`src/start.ts` lands in **both** module graphs — Start aliases it as the client entry too — so
keep `@bc-solutions-coder/sdk/server` and every other Node-only import out of it. Read
`process.env` inside the server callback, which the browser never runs.
`apps/wallow-web/src/app/start.ts` is the reference, including the `internalOrigin` resolution a
containerised deployment needs.

## 5. The first feature: `src/features/<name>/api.ts`

Backend-facing code lives in a **feature folder**, and each one owns exactly one seam file:

```
src/features/inquiries/
├── api.ts                    # the ONLY file importing @bc-solutions-coder/sdk/query
└── components/
    ├── InquiryList.tsx
    └── CreateInquiryForm.tsx
```

`api.ts` is a **thin re-export seam** — no wrappers, no hand-written `queryFn`, no local
types. It names the generated artifacts this feature uses and re-exports them by identity:

```ts
// src/features/inquiries/api.ts
export {
  inquiriesGetAllOptions,
  inquiriesGetAllQueryKey,
  inquiriesSubmitMutation,
  queriesForOperation,
  queriesWithTag,
} from "@bc-solutions-coder/sdk/query";
```

The seam earns its keep by being a **list**, not a layer. It makes one feature's backend
surface reviewable in a single file, gives every component in the folder one import path
(`./api`) that survives an operation being renamed upstream, and keeps `@bc-solutions-coder/sdk/query`
out of component files, where a wildcard import would quietly widen that surface again. What
it must never do is re-declare: a seam that wraps rather than re-exports doubles the surface
and silently forks the cache. `apps/wallow-web/src/features/*/api.ts` are all this shape, and
each has a co-located spec asserting re-export identity.

There is **no `types.ts`** in a feature folder. Request and response types are generated from
the OpenAPI document — import them from the SDK rather than restating them, or a hand-written
copy will drift from the schema without anything failing.

Components then read the request-scoped client off the router context and call the generated
factories:

```tsx
// src/features/inquiries/components/InquiryList.tsx
import { useMutation, useQuery, useQueryClient } from "@bc-solutions-coder/query";
import { useRouteContext } from "@tanstack/react-router";

import {
  inquiriesGetAllOptions,
  inquiriesGetAllQueryKey,
  inquiriesSubmitMutation,
  queriesForOperation,
} from "../api";

export function InquiryList(): React.ReactElement {
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();

  const { data } = useQuery(inquiriesGetAllOptions({ client: sdk.client }));

  const submit = useMutation({
    ...inquiriesSubmitMutation({ client: sdk.client }),
    onSuccess: () => {
      void queryClient.invalidateQueries(
        queriesForOperation(inquiriesGetAllQueryKey({ client: sdk.client })),
      );
    },
  });

  return (
    <ul>
      {data?.map((inquiry) => (
        <li key={inquiry.id}>{inquiry.name}</li>
      ))}
    </ul>
  );
}
```

Three properties of this code are load-bearing, and all three are covered in full by
[Frontend State](../development/frontend-state.md):

- **`data` is the response body.** Operations are generated with `responseStyle: "data"` and
  `throwOnError: true`, so there is no `{ data, error }` envelope to unwrap and every failure
  arrives as a thrown `WallowError`.
- **Keys are flat and generated.** A key is `[{ _id, baseUrl, tags, ...args }]` — a single
  object, with no prefix that sweeps a subtree. Never write a `queryKey` literal.
- **Invalidation lives at the call site.** Sweep with `queriesForOperation(key)` for one
  operation, or `queriesWithTag("Inquiries")` for everything one backend controller serves.

Adding an endpoint is a backend change plus a regen — see
[TypeScript SDK → Generated OpenAPI client](typescript-sdk.md#calling-module-endpoints-the-tanstack-query-layer).
There is no step for hand-writing a factory, because there is no hand-written factory left.

### Testing the feature

`@bc-solutions-coder/testing` ships the seam so specs never mock the SDK:
`createSdkHarness()` (from `./sdk-harness`) builds a real `createWallowSdk()` instance over a
fetch double and records every call; `renderWithWallow()` (from `./render-with-wallow`) mounts
a component with that instance and a fresh `QueryClient` in router context. Component specs run
in real headless Chromium, never jsdom — see [Testing](../development/testing.md).

## 6. The patterns that are gone

The collapse of the hand-written SDK surface deleted these outright — no deprecation window,
no re-export stub. Root `.oxlintrc.json` carries `no-restricted-imports` entries so each one
fails `pnpm lint` with a message naming its replacement, rather than resurfacing in a fork:

| Don't                                                                                  | Do instead                                                                         |
| -------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| `configureBffClient()`, `configureWallowClient()`, importing `client`                  | `createWallowSdk({ baseUrl })` per request; pass `{ client: sdk.client }`          |
| `configureSsrClient()`, `setSsrRequestContextResolver()`, `wireSsrCookieInterceptor()` | `createWallowSdk({ baseUrl, cookieHeader, internalOrigin })` in request middleware |
| `createAuthClient()`, `createMfaClient()`, `unwrap()`                                  | The generated operations; failures already arrive as `WallowError`                 |
| `queryKeys`, `organizationsQueries`, `mfaQueries`, and the other slices                | The generated `{op}Options()` / `{op}Mutation()` / `{op}QueryKey()`                |
| `registerQueryBootstrap()`, `ensureQueryBootstrapped()`                                | Nothing — pass `{ client }` explicitly; there is no module state to bootstrap      |
| A `src/lib/wallow-sdk.ts` facade singleton                                             | `useRouteContext({ from: "__root__" }).sdk`                                        |
| Deep-importing `@bc-solutions-coder/sdk/dist/**` or `/src/**`                          | The published entries: `.`, `/server`, `/server/passthrough`, `/query`             |

The last row is the one worth internalising even in a greenfield fork: `dist/` and `src/`
layouts are internals, not contract, and an import that reaches past the exports map breaks on
an SDK release that reorganises them.

## See also

- [Frontend Setup](../development/frontend-setup.md) — the complete app bootstrap.
- [TypeScript SDK](typescript-sdk.md) — entry points, env vars, CSRF, and the session model.
- [Frontend State](../development/frontend-state.md) — generated keys, invalidation, Zustand.
- [Fork Guide](../getting-started/fork-guide.md) — the fork workflow and the BFF-only policy.
