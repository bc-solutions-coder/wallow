# tanstack-min — Wallow BFF reference example

A minimal, drop-in example that wires the `@bc-solutions-coder/sdk` **Backend-for-Frontend
(BFF)** tunnel into a TanStack Start-style server. It shows the full
same-origin OIDC flow: browser login → authenticated `/api` call with silent
token refresh → logout, with tokens held server-side in an httpOnly sealed
cookie (never exposed to JavaScript).

> This example is not published. It is exercised by the .NET E2E harness
> (`tests/Wallow.E2E.Tests/Flows/BffFlowTests.cs`) via
> `docker/docker-compose.test.yml`, and serves as copy-paste reference wiring.

## Layout

| Path | Role |
| --- | --- |
| `server.ts` | Host that mounts `createBffHandlers` at `/bff/*`, `createApiProxy` at `/api/**` (both sharing one `SessionStore`), serves `public/`, and listens on `PORT` (default `3000`). |
| `src/app.ts` | Browser entry: `configureBffClient()`, `login()`/`logout()`/`getUser()`, the CSRF request interceptor, and generated typed operations. |
| `public/index.html` | The DOM the E2E test drives (`data-testid` selectors). |
| `Dockerfile` | Containerizes the example for the E2E stack. |

## Endpoints

| Route | Handler |
| --- | --- |
| `GET /health` | Liveness for the E2E `DockerComposeFixture`. |
| `/bff/login` | Start OIDC login (PKCE), redirect to Wallow.Auth. |
| `/bff/callback` | OIDC redirect URI; exchanges the code and seals the session. |
| `GET /bff/user` | Current user (`200` + claims, or `401` when anonymous). |
| `/bff/logout` | Clear the session and redirect through end-session. |
| `/api/**` | Reverse proxy to the Wallow API with a `Bearer` token + silent refresh. State-changing methods must carry the `x-csrf-token` header (see [CSRF](#csrf)). |

## Session storage

`createBffHandlers(config, store)` and `createApiProxy(config, store)` both take a
`SessionStore`. **Pass the same instance to both** — the proxy has to resolve the
sessions the login callback wrote.

This example injects a `CookieSessionStore`, which keeps the whole sealed session
inside the cookie: no Redis, no server-side state, scales statelessly. Swap it for
`ValkeySessionStore` when you need server-side revocation or a cross-instance
refresh lock; the session cookie then carries only an opaque reference.

```ts
import {
  CookieSessionStore,
  ValkeySessionStore,
  type RedisLike,
  type SessionStore,
} from "@bc-solutions-coder/sdk/server";

const store: SessionStore = new CookieSessionStore({
  password: config.cookiePassword,
});

// ...or, backed by Valkey/Redis (`client` is any RedisLike — ioredis, node-redis):
// const store: SessionStore = new ValkeySessionStore({
//   client,
//   password: config.cookiePassword,
//   ttlSeconds: config.sessionTtlSeconds,
// });

const bff = createBffHandlers(config, store);
const apiProxy = createApiProxy(config, store);
```

Both arguments are optional — omitting `store` defaults to a `CookieSessionStore`
built from `config.cookiePassword` — but wiring it explicitly is what makes the
production swap a one-line change.

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

`src/app.ts` caches the token from `getUser()` and echoes it with a request
interceptor on the shared client, so every generated operation carries it without
any per-call code:

```ts
client.interceptors.request.use((request: Request): Request => {
  if (csrfToken !== null && !safeMethods.has(request.method.toUpperCase())) {
    request.headers.set("x-csrf-token", csrfToken);
  }
  return request;
});
```

The **Create org** button exercises this end to end: it `POST`s through `/api`,
which an ordinary signed-in user is allowed to do, so the `201` it returns means
the request carried the token *and* cleared the gate. Drop the interceptor and the
same click comes back `403 CSRF_INVALID` without ever reaching the API.

## Typed API calls

After `configureBffClient()` the **generated typed operations** are pointed at the
same-origin `/api` proxy and send the session cookie — use them instead of raw
`fetch`. They resolve to `{ data, error, response }` and never throw on a non-2xx,
and the BFF and API both report failures as RFC 7807 problem+json, so `error` is a
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

| Variable | Required | Default | Purpose |
| --- | --- | --- | --- |
| `OIDC_ISSUER` | yes | — | OIDC issuer base URL (e.g. `http://localhost:5001`). |
| `OIDC_CLIENT_ID` | yes | — | Confidential client id — `bcordes-bff` for the seeded dev client. |
| `OIDC_CLIENT_SECRET` | yes | — | Confidential client secret — `bcordes-bff-secret` in dev. |
| `OIDC_REDIRECT_URI` | yes | — | Callback URL — `http://localhost:3000/bff/callback`. |
| `OIDC_POST_LOGOUT_REDIRECT_URI` | yes | — | Post-logout URL — `http://localhost:3000/`. |
| `BFF_API_BASE_URL` | yes | — | Downstream API base URL — `http://localhost:5001`. |
| `COOKIE_PASSWORD` | yes | — | Seal/unseal password for the session cookie (>= 32 chars). |
| `OIDC_SCOPES` | no | `openid profile email offline_access` | Space-separated scopes. |
| `COOKIE_NAME` | no | `wallow_bff` | Sealed session cookie name. The readable CSRF companion cookie is `${COOKIE_NAME}-csrf`. |
| `SESSION_TTL_SECONDS` | no | `86400` | Session lifetime. Bounds the session cookie's `Max-Age` (and the Valkey record's TTL), so a stale browser cookie cannot outlive its session. |
| `COOKIE_SECURE` | no | `true` | Sets `Secure` on the cookies the BFF writes. Set to `false` **only** for plain-HTTP local development on a non-`localhost` hostname — `localhost` already counts as a secure context, so the default works there. |
| `OIDC_METADATA_URL` | no | `${OIDC_ISSUER}/.well-known/openid-configuration` | Server-reachable discovery URL. Set this when the browser and server reach the OP under different hostnames (reverse proxy, container network, split-horizon DNS). The server fetches discovery here and uses its `token_endpoint` for the backchannel, while the browser-facing authorize/end-session URLs are pinned to the public `OIDC_ISSUER` origin. |
| `PORT` | no | `3000` | Listen port. |

> **Split-horizon note (container networks).** In the E2E stack the browser
> reaches the OP at `http://localhost:5050` while the example container reaches
> it at `http://host.docker.internal:5050`. `OIDC_ISSUER` stays on the
> browser-facing origin and `OIDC_METADATA_URL` points at the container-reachable
> one — the same pattern `Wallow.Web` uses with `Authority` + `MetadataAddress`.

> Note: an earlier design draft referenced `BFF_ISSUER`, `BFF_CLIENT_ID`,
> `BFF_REDIRECT_URI`, `BFF_POST_LOGOUT_REDIRECT_URI`, and `BFF_COOKIE_PASSWORD`.
> The shipped SDK uses the `OIDC_*` / `COOKIE_*` names above — those are
> authoritative.

## Run locally

```bash
# from packages/typescript-sdk
npm install && npm run build   # build @bc-solutions-coder/sdk first

# from packages/typescript-sdk/examples/tanstack-min
npm install
export OIDC_ISSUER=http://localhost:5001
export OIDC_CLIENT_ID=bcordes-bff
export OIDC_CLIENT_SECRET=bcordes-bff-secret
export OIDC_REDIRECT_URI=http://localhost:3000/bff/callback
export OIDC_POST_LOGOUT_REDIRECT_URI=http://localhost:3000/
export BFF_API_BASE_URL=http://localhost:5001
export COOKIE_PASSWORD=dev-cookie-password-change-me-32chars
npm run build && npm start   # http://localhost:3000
```

## E2E

Run the full flow through the containerized stack:

```bash
./scripts/run-e2e.sh
```
