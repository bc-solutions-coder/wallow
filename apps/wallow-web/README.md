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

> This app is not published. It is exercised by the .NET E2E harness
> (`api/tests/Wallow.E2E.Tests/Flows/BffFlowTests.cs`) via
> `docker/docker-compose.test.yml`, and serves as reference wiring for forks.

## Commands

Run from the repo root (pnpm workspace) or with `--filter @bc-solutions-coder/wallow-web`.
Build the SDK first — the app typechecks against its `dist/`.

```bash
pnpm --filter @bc-solutions-coder/sdk build   # build the SDK the app depends on

pnpm --filter @bc-solutions-coder/wallow-web dev        # SSR dev server + BFF (tsx dev-server.ts)
pnpm --filter @bc-solutions-coder/wallow-web build      # vite build -> public/ bundle
pnpm --filter @bc-solutions-coder/wallow-web start      # standalone h3 BFF host (tsx server.ts)
pnpm --filter @bc-solutions-coder/wallow-web typecheck  # tsc --noEmit
pnpm --filter @bc-solutions-coder/wallow-web test       # vitest run  (test:watch for watch mode)
```

- **`dev`** boots a Vite (`middlewareMode`) + Node HTTP server (`dev-server.ts`)
  that server-renders the matched route and answers `/health`, `/bff/*`, and
  `/api/**` from the BFF bridge in one process. Plain `pnpm dev` serves SSR even
  without BFF env — only the BFF/api/health prefixes need the OIDC env below.
- **`start`** runs the standalone h3 BFF host (`server.ts`) that serves the
  built `public/` bundle. This is the entry the `Dockerfile` and the E2E stack
  use.

## Layout

| Path                   | Role                                                                                                                                                         |
| ---------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `dev-server.ts`        | Dev entry (`pnpm dev`): TanStack Start SSR via Vite middleware, with `/health` + `/bff/*` + `/api/**` dispatched to the BFF bridge. See BFF wiring below.    |
| `server.ts`            | Prod/E2E entry (`pnpm start`): standalone h3 host mounting the same BFF handlers and serving the static `public/` bundle. Containerized by the `Dockerfile`. |
| `src/ssr.tsx`          | SSR render entry — turns a request into the server-rendered HTML shell for the matched route.                                                                |
| `src/router.tsx`       | Manual TanStack route tree (routes are wired explicitly here, not file-based codegen).                                                                       |
| `src/routes/`          | Route components: public `index`, the `dashboard` layout + feature routes, and the `bff-demo` BFF smoke route.                                               |
| `src/features/<name>/` | Feature verticals (`organizations`, `apps`, `settings`, `mfa`, `inquiries`): each has `api.ts` (query/mutation layer), `types.ts`, and `components/`.        |
| `src/lib/`             | Shared plumbing: `wallow-sdk.ts` (typed SDK facade), `bff-server.ts` (BFF bridge), `csrf.ts` (request interceptor), `query-client.ts`.                       |
| `src/components/`      | Cross-feature UI (`DashboardLayout`, `DashboardNav`).                                                                                                        |
| `public/`              | Built browser bundle and static assets served by `server.ts`.                                                                                                |
| `Dockerfile`           | Containerizes the app for the E2E stack; its build context is the **repo root** (needs the whole workspace to resolve `workspace:*`).                        |

### Feature folder shape

Each vertical under `src/features/<name>/` follows the same template:

```
src/features/organizations/
  api.ts                       # TanStack Query queries + mutations over the SDK facade
  types.ts                     # feature-local view types
  components/
    OrganizationList.tsx       # dashboard list page body
    OrganizationDetail.tsx     # detail + member management
    CreateOrganizationForm.tsx # TanStack Form create flow
```

Routes in `src/routes/dashboard/<name>/` render these components; new routes must
be registered in `src/router.tsx` (the route tree is manual, not generated).

## BFF wiring

The installed TanStack Start release exposes no file-based server-route creator,
so the SDK's h3 BFF/proxy handlers are mounted at the **server layer** instead of
in a `src/routes/**` server route. `src/lib/bff-server.ts` builds the handlers
once and exposes `handleBffRequest(request)`, a web `Request` → `Response` bridge
that mounts:

| Route           | Handler                                                                                                                                                  |
| --------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GET /health`   | Liveness for the E2E `DockerComposeFixture`.                                                                                                             |
| `/bff/login`    | Start OIDC login (PKCE), redirect to Wallow.Auth.                                                                                                        |
| `/bff/callback` | OIDC redirect URI; exchanges the code and seals the session.                                                                                             |
| `GET /bff/user` | Current user (`200` + claims, or `401` when anonymous).                                                                                                  |
| `/bff/logout`   | Clear the session and redirect through end-session.                                                                                                      |
| `/api/**`       | Reverse proxy to the Wallow API with a `Bearer` token + silent refresh. State-changing methods must carry the `x-csrf-token` header (see [CSRF](#csrf)). |

`dev-server.ts` dispatches those prefixes to the bridge and lets everything else
fall through to the router SSR. `server.ts` mounts the same handlers on a plain
h3 app for the container/E2E path. If a future TanStack release adds a real
server-route creator, a `$.ts` server route can delegate to `handleBffRequest`
unchanged.

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

`src/lib/csrf.ts` wires a request interceptor onto the shared SDK client that
echoes the cached token on every unsafe request, so each generated operation
carries it without any per-call code:

```ts
client.interceptors.request.use((request: Request): Request => {
  if (csrfToken !== null && !safeMethods.has(request.method.toUpperCase())) {
    request.headers.set("x-csrf-token", csrfToken);
  }
  return request;
});
```

## Typed API calls

The typed SDK facade lives in `src/lib/wallow-sdk.ts`. After the client is
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
| `OIDC_CLIENT_ID`                | yes      | —                                                 | Confidential client id — `bcordes-bff` for the seeded dev client.                                                                                                                                                                                                                                                                                          |
| `OIDC_CLIENT_SECRET`            | yes      | —                                                 | Confidential client secret — `bcordes-bff-secret` in dev.                                                                                                                                                                                                                                                                                                  |
| `OIDC_REDIRECT_URI`             | yes      | —                                                 | Callback URL — `http://localhost:3000/bff/callback`.                                                                                                                                                                                                                                                                                                       |
| `OIDC_POST_LOGOUT_REDIRECT_URI` | yes      | —                                                 | Post-logout URL — `http://localhost:3000/`.                                                                                                                                                                                                                                                                                                                |
| `BFF_API_BASE_URL`              | yes      | —                                                 | Downstream API base URL — `http://localhost:5001`.                                                                                                                                                                                                                                                                                                         |
| `COOKIE_PASSWORD`               | yes      | —                                                 | Seal/unseal password for the session cookie (>= 32 chars).                                                                                                                                                                                                                                                                                                 |
| `OIDC_SCOPES`                   | no       | `openid profile email offline_access`             | Space-separated scopes.                                                                                                                                                                                                                                                                                                                                    |
| `COOKIE_NAME`                   | no       | `wallow_bff`                                      | Sealed session cookie name. The readable CSRF companion cookie is `${COOKIE_NAME}-csrf`.                                                                                                                                                                                                                                                                   |
| `SESSION_TTL_SECONDS`           | no       | `86400`                                           | Session lifetime. Bounds the session cookie's `Max-Age` (and the Valkey record's TTL), so a stale browser cookie cannot outlive its session.                                                                                                                                                                                                               |
| `COOKIE_SECURE`                 | no       | `true`                                            | Sets `Secure` on the cookies the BFF writes. Set to `false` **only** for plain-HTTP local development on a non-`localhost` hostname — `localhost` already counts as a secure context, so the default works there.                                                                                                                                          |
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
export OIDC_CLIENT_ID=bcordes-bff
export OIDC_CLIENT_SECRET=bcordes-bff-secret
export OIDC_REDIRECT_URI=http://localhost:3000/bff/callback
export OIDC_POST_LOGOUT_REDIRECT_URI=http://localhost:3000/
export BFF_API_BASE_URL=http://localhost:5001
export COOKIE_PASSWORD=dev-cookie-password-change-me-32chars
pnpm dev   # SSR + BFF on http://localhost:3000
```

## E2E

Run the full flow through the containerized stack:

```bash
./scripts/run-e2e.sh
```
