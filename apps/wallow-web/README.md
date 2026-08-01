# wallow-web — Wallow TanStack Start frontend

`@bc-solutions-coder/wallow-web` is the runnable reference frontend for the
Wallow platform. It is a **TanStack Start** (React 19) single-page + SSR app that
consumes the `@bc-solutions-coder/sdk` **Backend-for-Frontend (BFF)** tunnel, so
the browser runs the full same-origin OIDC flow — login → authenticated `/api`
calls with silent token refresh → logout — while the OIDC token set stays
server-side in an httpOnly sealed cookie (or Valkey/Redis), never exposed to
JavaScript.

It doubles as the copy-paste template teams fork: a dashboard with feature
verticals (organizations, apps, settings, MFA, inquiries), each following the
same feature-folder shape, plus a live BFF smoke route.

> This app is not published. It is exercised by its own Playwright suite
> (`e2e/`, see [E2E](#e2e)) via `docker/docker-compose.test.yml`, and serves as
> reference wiring for forks.

## Commands

Run from the repo root (pnpm workspace) or with `--filter @bc-solutions-coder/wallow-web`.
Build the SDK first — the app typechecks against its `dist/`.

```bash
pnpm --filter @bc-solutions-coder/sdk build   # build the SDK the app depends on

pnpm --filter @bc-solutions-coder/wallow-web dev        # vite dev -- TanStack Start SSR + BFF
pnpm --filter @bc-solutions-coder/wallow-web build      # vite build -> .output/server + .output/public
pnpm --filter @bc-solutions-coder/wallow-web start      # node .output/server/index.mjs
pnpm --filter @bc-solutions-coder/wallow-web typecheck  # tsc --noEmit
pnpm --filter @bc-solutions-coder/wallow-web test       # vitest run  (test:watch for watch mode)
```

- **`dev`** runs the TanStack Start dev server (Vite). It server-renders the
  matched route and answers `/health`, `/bff/**`, and `/api/**` through the
  file-based server routes under `src/app/routes/` (see BFF wiring below). Plain
  `pnpm dev` serves SSR even without BFF env — only the BFF/api/health prefixes
  need the OIDC env below.
- **`build`** runs a Nitro-backed `vite build` that emits both environments
  into `.output/`: `.output/server/index.mjs` is the server entry and
  `.output/public` holds the client bundle and static assets.
- **`start`** runs the built server (`node .output/server/index.mjs`). This is
  the entry the `Dockerfile` and the E2E stack use.

## Layout

`src/` is split into three **zones**, and the split is enforced by the
`wallow/zone-dag` lint rule rather than by convention:

- **`app/`** — the host. Routes, router, entries, and anything server-only. Nothing
  outside `app/` may import from it (a spec mounting a real route is the one
  exemption), which is what keeps `node:crypto`/`openid-client` out of the client
  graph.
- **`features/<name>/`** — one directory per vertical. A feature reaches its own
  files relatively and `shared/` by alias; it never reaches a sibling feature, and
  is itself reachable only through its `index.ts` barrel.
- **`shared/`** — what more than one feature genuinely needs. It may reach nothing
  but itself.

Cross-zone imports are spelled as aliases — `@app/*`, `@features/<name>`,
`@shared/*` — so a boundary crossing is visible in the import block. The alias map
lives in `tsconfig.json` `paths` and nowhere else: Vite and vitest both read it
through `resolve.tsconfigPaths`, and `wallow/zone-dag` reads it to know which
zones exist.

| Path                                                                             | Role                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| -------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/app/start.ts`                                                               | The `createStart()` instance: a global request middleware mints a per-request SDK and installs the D13b `setSsrRequestContextResolver` bridge (temporary, until routes read their SDK off the router context).                                                                                                                                                                                                                                                                                |
| `src/app/router.tsx`                                                             | `createRouter` + `setupRouterSsrQueryIntegration`, wiring the request's SDK (or a fresh browser one) and a per-request `QueryClient` into the router context.                                                                                                                                                                                                                                                                                                                                 |
| `src/app/routes/`                                                                | File-based routes: public `index`, the `dashboard` layout + feature routes, the `bff-demo` BFF smoke route, and the server routes below. New routes are added here — there is no manual route tree to register them in.                                                                                                                                                                                                                                                                       |
| `src/app/routes/bff/$.ts`, `src/app/routes/api/$.ts`, `src/app/routes/health.ts` | File-based server routes (single `ANY`/`GET` handler each) delegating to `src/app/lib/bff.server.ts`. See BFF wiring below.                                                                                                                                                                                                                                                                                                                                                                   |
| `src/app/lib/`                                                                   | Server-only plumbing: `bff.server.ts`, the lazily-built `createWallowBffServer()` host exposing `handleBffRequest`/`handleApiRequest`/`handleHealthRequest`. It lives in `app/` and not `shared/` precisely because nothing else may import it, and the `.server.` suffix is what makes Start's import protection enforce that — see BFF wiring below.                                                                                                                                        |
| `src/app/routeTree.gen.ts`                                                       | Generated by the Start Vite plugin as a side effect of `vite dev`/`vite build` — never hand-edited, no separate `routes:generate` step. It follows the routes because `vite.config.ts` sets `srcDirectory: "src/app"`.                                                                                                                                                                                                                                                                        |
| `src/app/styles.css`                                                             | The app's single Tailwind entry, imported for side effects by `app/routes/__root.tsx`.                                                                                                                                                                                                                                                                                                                                                                                                        |
| `src/features/<name>/`                                                           | Feature verticals (`organizations`, `apps`, `settings`, `mfa`, `inquiries`): each has `index.ts` (the barrel — the feature's public contract), `api.ts` (query/mutation seam), `types.ts`, and `components/`.                                                                                                                                                                                                                                                                                 |
| `src/shared/`                                                                    | `components/` (`DashboardLayout` and its `dashboard-destinations` manifest, `SignOut`, `PublicLayout`, `ready-indicator`), `lib/` (`log`, `site-links`, `fork-links`). What used to sit beside them is now shared: the dashboard shell is `@bc-solutions-coder/navigation`, the `QueryClient` factory `@bc-solutions-coder/query`, the current-user query `@bc-solutions-coder/auth`, `errorText` `@bc-solutions-coder/forms`, and `PageContainer` + `SimpleSelect` `@bc-solutions-coder/ui`. |
| `Dockerfile`                                                                     | Containerizes the app for the E2E stack; its build context is the **repo root** (needs the whole workspace to resolve `workspace:*`). Runs `node .output/server/index.mjs` (Nitro's build output — there is no checked-in `public/` dir).                                                                                                                                                                                                                                                     |

### Feature folder shape

Each vertical under `src/features/<name>/` follows the same template:

```
src/features/organizations/
  index.ts                     # the barrel: everything a route may import from here
  api.ts                       # thin re-export seam over @bc-solutions-coder/sdk/query
  types.ts                     # feature-local view types
  components/
    OrganizationList.tsx       # dashboard list page body
    OrganizationDetail.tsx     # detail + member management
    CreateOrganizationForm.tsx # TanStack Form create flow
```

`api.ts` re-exports the vertical's `queryOptions`/mutation factories from
`@bc-solutions-coder/sdk/query` (the canonical query/mutation definitions and their keys live in
the SDK, not the app) so routes and components keep importing data access from `./api` — see
[Frontend State: TanStack Query vs. Zustand](../../docs/development/frontend-state.md) for
the query/Zustand boundary and how to add a new query.

Routes in `src/app/routes/dashboard/<name>/` render these components through the
feature's barrel (`@features/<name>`), never by deep path; new routes are
file-based under `src/app/routes/**` — the Start Vite plugin regenerates
`src/app/routeTree.gen.ts` as a side effect of `vite dev`/`vite build`.

## BFF wiring

TanStack Start's file-based routing supports server routes: a route module can
export `server.handlers` alongside (or instead of) a UI component. `src/app/lib/bff.server.ts`
lazily builds the SDK's `createWallowBffServer()` host once (memoised, connecting
Redis first when `REDIS_URL` is set) and exposes three async functions —
`handleBffRequest`, `handleApiRequest`, `handleHealthRequest` — each a web
`Request` → `Response` bridge. Three server routes delegate to them with a single
`ANY` (or `GET`) handler apiece, reaching `src/app/lib/bff.server.ts` through a dynamic
`import()` rather than a top-level one, because it pulls in
`@bc-solutions-coder/sdk/server` (`node:crypto`, `openid-client`) and every route
module is a member of the tree the **client** graph also imports:

| Route           | File                       | Handler                                                                                                                                                               |
| --------------- | -------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GET /health`   | `src/app/routes/health.ts` | Liveness for the E2E stack — building the BFF server here is what validates OIDC config, so a misconfigured host fails its healthcheck instead of crashing at import. |
| `/bff/login`    | `src/app/routes/bff/$.ts`  | Start OIDC login (PKCE), redirect to Wallow.Auth.                                                                                                                     |
| `/bff/callback` | `src/app/routes/bff/$.ts`  | OIDC redirect URI; exchanges the code and seals the session.                                                                                                          |
| `GET /bff/user` | `src/app/routes/bff/$.ts`  | Current user (`200` + claims, or `401` when anonymous).                                                                                                               |
| `/bff/logout`   | `src/app/routes/bff/$.ts`  | Clear the session and redirect through end-session.                                                                                                                   |
| `/api/**`       | `src/app/routes/api/$.ts`  | Reverse proxy to the Wallow API with a `Bearer` token + silent refresh. State-changing methods must carry the `x-csrf-token` header (see [CSRF](#csrf)).              |

`/bff/$` and `/api/$` use a single `ANY` handler rather than a method map: method
policy belongs to the SDK's handlers (a bare `GET /bff/logout` answers `405` +
`Allow: POST`), and a method-filtered route would swallow that as a local 404.

### Session storage

`createBffHandlers(config, store)` and `createApiProxy(config, store)` both take a
`SessionStore`, and **both are given the same instance** — the proxy has to
resolve the sessions the login callback wrote. When `REDIS_URL` is set the app
uses a `ValkeySessionStore` (opaque cookie reference, server-side revocation and
a cross-instance refresh lock); otherwise it falls back to a
`CookieSessionStore`, which seals the whole session into the cookie and needs no
external store. Swapping stores is the one production knob.

## CSRF

The BFF **rejects any state-changing request** (`POST`/`PUT`/`PATCH`/`DELETE`)
through `/api/**` that does not echo the session's CSRF token, with a `403`
problem+json carrying `code: "CSRF_INVALID"`. Safe methods (`GET`, `HEAD`) pass
through untouched. This is what stops a cross-site form post from riding on the
session cookie, which the browser would otherwise attach automatically.

The token is minted at login, sealed inside the session, and handed to the browser
two ways: in the `/bff/user` response body, and in a readable (non-`HttpOnly`)
companion cookie named `${COOKIE_NAME}-csrf`. The session cookie itself stays
`HttpOnly` — the companion cookie is not a credential on its own.

The SDK's own `csrf.ts` wires a request interceptor onto the shared client that
echoes the cached token on every unsafe request, so each generated operation
carries it without any per-call code — the app holds no copy of this:

```ts
client.interceptors.request.use((request: Request): Request => {
  if (csrfToken !== null && !safeMethods.has(request.method.toUpperCase())) {
    request.headers.set("x-csrf-token", csrfToken);
  }
  return request;
});
```

## Typed API calls

The typed SDK facade is the SDK's own `createWallowSdk()`, minted per request by
`src/app/start.ts` and read off the router context. After the client is
configured, the **generated typed operations** are pointed at the same-origin
`/api` proxy and send the session cookie — use them instead of raw `fetch`. They
resolve to `{ data, error, response }` and never throw on a non-2xx, and the BFF
and API both report failures as RFC 7807 problem+json, so `error` is a
`ProblemDetails`:

```ts
const { data, error, response } = await getV1IdentityUsersMe();
if (error !== undefined) {
  const problem = error as ProblemDetails;
  console.error(response.status, problem.title, problem.detail);
}
```

## Environment variables

The SDK reads config via `loadBffConfigFromEnv()`. These are the **actual keys**
consumed by `@bc-solutions-coder/sdk/server` (`src/server/config.ts`):

| Variable                        | Required | Default                                           | Purpose                                                                                                                                                                                                                                                                                                                                                    |
| ------------------------------- | -------- | ------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `OIDC_ISSUER`                   | yes      | —                                                 | OIDC issuer base URL (e.g. `http://localhost:5001`).                                                                                                                                                                                                                                                                                                       |
| `OIDC_CLIENT_ID`                | yes      | —                                                 | Confidential client id — `wallow-web-client` for the seeded dev client.                                                                                                                                                                                                                                                                                    |
| `OIDC_CLIENT_SECRET`            | yes      | —                                                 | Confidential client secret — `wallow-web-secret` in dev.                                                                                                                                                                                                                                                                                                   |
| `OIDC_REDIRECT_URI`             | yes      | —                                                 | Callback URL — `http://localhost:3000/bff/callback`.                                                                                                                                                                                                                                                                                                       |
| `OIDC_POST_LOGOUT_REDIRECT_URI` | yes      | —                                                 | Post-logout URL — `http://localhost:3000/`.                                                                                                                                                                                                                                                                                                                |
| `BFF_API_BASE_URL`              | yes      | —                                                 | Downstream API base URL — `http://localhost:5001`.                                                                                                                                                                                                                                                                                                         |
| `COOKIE_PASSWORD`               | yes      | —                                                 | Seal/unseal password for the session cookie (>= 32 chars).                                                                                                                                                                                                                                                                                                 |
| `OIDC_SCOPES`                   | no       | `openid profile email offline_access`             | Space-separated scopes.                                                                                                                                                                                                                                                                                                                                    |
| `COOKIE_NAME`                   | no       | `wallow_bff`                                      | Sealed session cookie name. The readable CSRF companion cookie is `${COOKIE_NAME}-csrf`.                                                                                                                                                                                                                                                                   |
| `SESSION_TTL_SECONDS`           | no       | `86400`                                           | Session lifetime. Bounds the session cookie's `Max-Age` (and the Valkey record's TTL), so a stale browser cookie cannot outlive its session.                                                                                                                                                                                                               |
| `COOKIE_SECURE`                 | no       | `true`                                            | Sets `Secure` on the cookies the BFF writes. Set to `false` for any plain-HTTP deployment, including `localhost`: Chrome and Firefox accept `Secure` cookies over `http://localhost`, but Safari/WebKit drops them, which silently breaks the login callback (400).                                                                                        |
| `OIDC_METADATA_URL`             | no       | `${OIDC_ISSUER}/.well-known/openid-configuration` | Server-reachable discovery URL. Set this when the browser and server reach the OP under different hostnames (reverse proxy, container network, split-horizon DNS). The server fetches discovery here and uses its `token_endpoint` for the backchannel, while the browser-facing authorize/end-session URLs are pinned to the public `OIDC_ISSUER` origin. |
| `REDIS_URL`                     | no       | —                                                 | When set, sessions persist in Valkey/Redis (`ValkeySessionStore`); otherwise the app seals the session into the cookie (`CookieSessionStore`).                                                                                                                                                                                                             |
| `PORT`                          | no       | `3000`                                            | Listen port.                                                                                                                                                                                                                                                                                                                                               |

> **Split-horizon note (container networks).** In the E2E stack the browser
> reaches the OP at `http://localhost:5050` while the app container reaches
> it at `http://host.docker.internal:5050`. `OIDC_ISSUER` stays on the
> browser-facing origin and `OIDC_METADATA_URL` points at the container-reachable
> one — the same pattern `Wallow.Web` uses with `Authority` + `MetadataAddress`.

## Run locally

```bash
# build the SDK first, from the repo root
pnpm --filter @bc-solutions-coder/sdk build

# from apps/wallow-web
export OIDC_ISSUER=http://localhost:5001
export OIDC_CLIENT_ID=wallow-web-client
export OIDC_CLIENT_SECRET=wallow-web-secret
export OIDC_REDIRECT_URI=http://localhost:3000/bff/callback
export OIDC_POST_LOGOUT_REDIRECT_URI=http://localhost:3000/
export BFF_API_BASE_URL=http://localhost:5001
export COOKIE_PASSWORD=dev-cookie-password-change-me-32chars
pnpm dev   # SSR + BFF on http://localhost:3000
```

## E2E

Run this app's Playwright suite (it boots the dev server itself on port 3000):

```bash
pnpm --filter ./apps/wallow-web test:e2e
```

The cross-app login journey (`e2e-cross-app/`) boots no server of its own — it needs a full stack
cross-wiring wallow-web, the API's OIDC issuer, and wallow-auth:

```bash
E2E_BASE_URL=http://localhost:5053 pnpm --filter ./apps/wallow-web test:e2e:cross-app
```

For the full backend-dependent flow, `./scripts/e2e.sh` brings up the containerized stack
(infra + API + seeder + this app on `:5053`), runs all three suites against it — wallow-auth,
this app's reachability gate, and the cross-app journey — and tears down.
