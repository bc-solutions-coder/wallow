# TypeScript SDK Integration Guide

This guide explains how to consume Wallow from a TypeScript frontend using the
[`@bc-solutions-coder/sdk`](../operations/versioning.md) package. The SDK ships a **browser
client** for calling Wallow APIs from the page, and a **server (BFF) tunnel**
that runs the OAuth 2.0 Authorization Code flow entirely server-side so that no
token ever reaches the browser.

If you are building a bespoke BFF by hand — or targeting a non-TypeScript
runtime — read the [BFF Pattern guide](bff-pattern.md) first for the underlying
protocol. This guide is the batteries-included path: the SDK implements that
same pattern (PKCE, sealed session cookie, silent refresh, `/api` proxy) for
you.

## Overview

`@bc-solutions-coder/sdk` has two entrypoints:

| Import | Runs in | Purpose |
|--------|---------|---------|
| `@bc-solutions-coder/sdk` | Browser | `login()`, `logout()`, `getUser()`, and a typed API client configured to call the same-origin `/api` proxy |
| `@bc-solutions-coder/sdk/server` | Server (Node) | `createBffHandlers()`, `createApiProxy()`, `loadBffConfigFromEnv()` — the [h3](https://h3.unjs.io) route handlers that make up the BFF tunnel |

The browser never holds an access token. It holds only a sealed, `httpOnly`
session cookie. The BFF exchanges the authorization code, stores the token set
inside that sealed cookie, and attaches the `Authorization: Bearer` header when
it proxies calls to the Wallow API.

```mermaid
sequenceDiagram
    participant Browser
    participant BFF as Your BFF<br/>(@bc-solutions-coder/sdk/server)
    participant Auth as Wallow Auth
    participant API as Wallow API

    Browser->>BFF: GET /bff/login
    BFF->>Browser: 302 -> Auth /connect/authorize (PKCE)
    Browser->>Auth: Authenticate + consent
    Auth->>Browser: 302 -> /bff/callback?code=...
    Browser->>BFF: GET /bff/callback?code=...
    BFF->>API: POST /connect/token (code + verifier + secret)
    API->>BFF: { access_token, refresh_token, id_token }
    BFF->>Browser: Set-Cookie: wallow_bff=<sealed>; 302 -> /
    Browser->>BFF: GET /api/v1/... (Cookie: wallow_bff)
    BFF->>BFF: Silent refresh if near expiry
    BFF->>API: GET /v1/... (Authorization: Bearer <token>)
    API->>BFF: 200 OK
    BFF->>Browser: 200 OK
```

---

## Installation

`@bc-solutions-coder/sdk` is published to **GitHub Packages** under the repository owner's
scope. Because it is not on the public npm registry, configure npm to resolve
the `@bc-solutions-coder` scope from GitHub Packages and authenticate with a token that has
the `read:packages` permission.

Create a `.npmrc` at your project root:

```ini
@bc-solutions-coder:registry=https://npm.pkg.github.com
//npm.pkg.github.com/:_authToken=${GITHUB_TOKEN}
```

> **Scope note:** GitHub Packages resolves scoped packages against the
> publishing organization. Point the `@bc-solutions-coder` scope at
> `https://npm.pkg.github.com` and export a `GITHUB_TOKEN` (a personal access
> token or CI token with `read:packages`). Never commit the token — reference it
> via an environment variable as shown above.

Then install:

```bash
npm install @bc-solutions-coder/sdk
```

The SDK depends on `h3` on the server side; install it alongside the SDK if your
host framework does not already provide it:

```bash
npm install h3
```

---

## Server setup: mounting the BFF

The BFF is a set of h3 event handlers. In TanStack Start (or any h3-compatible
server), mount them so that:

- `/bff/login`, `/bff/callback`, `/bff/user`, `/bff/logout` map to the four
  handlers from `createBffHandlers(config)`.
- `/api/**` maps to the proxy from `createApiProxy(config)`.

```ts
// server routes: mount the BFF tunnel
import {
  createApiProxy,
  createBffHandlers,
  loadBffConfigFromEnv,
  type BffConfig,
} from "@bc-solutions-coder/sdk/server";

const config: BffConfig = loadBffConfigFromEnv();
const bff = createBffHandlers(config);
const apiProxy = createApiProxy(config);

// h3 handlers — wire them to your framework's server routes:
//   ALL  /bff/login    -> bff.login
//   ALL  /bff/callback -> bff.callback
//   GET  /bff/user     -> bff.user
//   ALL  /bff/logout   -> bff.logout
//   ALL  /api/**       -> apiProxy
```

In **TanStack Start**, create a catch-all server route per prefix and export the
handler. Each of `bff.login`, `bff.callback`, `bff.user`, `bff.logout`, and
`apiProxy` is a standard `defineEventHandler` object, so it drops directly into
any server route that accepts an h3 `EventHandler`.

A minimal, framework-agnostic reference host lives in the repository at
`packages/typescript-sdk/examples/tanstack-min/` — it mounts exactly these
routes on a plain h3 app and serves a static browser bundle.

### The `/api` proxy and silent refresh

`createApiProxy(config)` reads the sealed session cookie on each request. Before
forwarding, it checks whether the access token is within a short skew window of
expiry and, if so, **silently refreshes** it using the stored refresh token and
re-seals the cookie — the browser sees only a normal API response. It then
strips the `/api` prefix and forwards the request to `apiBaseUrl` with the
`Authorization: Bearer <access_token>` header attached.

Requests that arrive without a valid session receive a `401`, which the browser
`getUser()` helper interprets as "unauthenticated".

---

## Environment variables

`loadBffConfigFromEnv()` reads the following variables (it throws on startup if
any required key is missing or empty):

| Variable | Required | Description |
|----------|----------|-------------|
| `OIDC_ISSUER` | Yes | OIDC issuer base URL, e.g. `https://auth.example.com` |
| `OIDC_CLIENT_ID` | Yes | Confidential client identifier registered with Wallow |
| `OIDC_CLIENT_SECRET` | Yes | Confidential client secret — server-side only, never exposed |
| `OIDC_REDIRECT_URI` | Yes | Absolute callback URL, e.g. `http://localhost:3000/bff/callback` |
| `OIDC_POST_LOGOUT_REDIRECT_URI` | Yes | Absolute URL to land on after logout, e.g. `http://localhost:3000/` |
| `BFF_API_BASE_URL` | Yes | Base URL of the downstream Wallow API the proxy forwards to |
| `COOKIE_PASSWORD` | Yes | Secret (32+ chars) used to seal/unseal the session and transaction cookies |
| `OIDC_SCOPES` | No | Space-separated scopes. Defaults to `openid profile email offline_access` |
| `COOKIE_NAME` | No | Session cookie name. Defaults to `wallow_bff` |

> **Confidential values:** `OIDC_CLIENT_SECRET` and `COOKIE_PASSWORD` must never
> be shipped to the browser or committed to source control. They belong in the
> server process environment (or a secrets manager) only.

---

## Browser API

Configure the shared client once at startup, then use the three auth helpers.

```ts
import { configureWallowClient, getUser, login, logout } from "@bc-solutions-coder/sdk";

// Point the typed client at the same-origin BFF proxy and send the cookie.
configureWallowClient(); // defaults baseUrl to "/api", credentials: "include"

// Render current auth state.
const user = await getUser(); // WallowUser | null (null when unauthenticated)
if (user === null) {
  login("/dashboard"); // navigates to /bff/login?returnTo=/dashboard
} else {
  console.log(user.sub, user.email);
}

// Sign out — navigates to /bff/logout, clears the session, returns to the
// post-logout redirect URI.
logout();
```

- `login(returnTo = "/")` — navigates the browser to `/bff/login`, preserving
  where to land after a successful sign-in.
- `logout()` — navigates the browser to `/bff/logout`.
- `getUser()` — `GET /bff/user`; resolves to a `WallowUser` on `200`, `null` on
  `401` (unauthenticated), and throws on any other error. `WallowUser` always
  carries `sub` and optionally `email`/`name` plus any additional claims.

### Calling module endpoints with the typed client

`configureWallowClient()` points the generated Hey API client at the `/api`
proxy with `credentials: "include"`, so every generated call rides the sealed
session cookie and is transparently authenticated by the BFF. Import the
generated operation functions from `@bc-solutions-coder/sdk` and call them directly — no
token handling in the browser:

```ts
import { configureWallowClient } from "@bc-solutions-coder/sdk";
// import { getInquiries } from "@bc-solutions-coder/sdk"; // generated typed operation

configureWallowClient();

// const { data } = await getInquiries();
// data is fully typed from the OpenAPI schema; the request went
// browser -> /api proxy -> Wallow API with a server-attached Bearer token.
```

If your app is not served from the same origin as the BFF, pass an explicit
base URL: `configureWallowClient({ baseUrl: "https://app.example.com/api" })`.

---

## Local development: the seeded `bcordes-bff` client

The repository's `seed.json` ships a ready-to-use confidential client for local
BFF development so you do not have to register one by hand. After running the
[seeder](../getting-started/developer-guide.md), the following client exists in
the `Dev` tenant:

| Setting | Value |
|---------|-------|
| `clientId` | `bcordes-bff` |
| `clientSecret` | `bcordes-bff-secret` |
| Redirect URI | `http://localhost:3000/bff/callback` |
| Post-logout redirect URI | `http://localhost:3000/` |
| Scopes | `openid email profile roles offline_access inquiries.read inquiries.write notifications.read notifications.write` |

Point your BFF at it with a local `.env` (adjust the API/issuer origins to your
running stack):

```ini
OIDC_ISSUER=http://localhost:5002
OIDC_CLIENT_ID=bcordes-bff
OIDC_CLIENT_SECRET=bcordes-bff-secret
OIDC_REDIRECT_URI=http://localhost:3000/bff/callback
OIDC_POST_LOGOUT_REDIRECT_URI=http://localhost:3000/
BFF_API_BASE_URL=http://localhost:5001
COOKIE_PASSWORD=dev-only-change-me-to-a-long-random-string
```

The redirect and post-logout URIs must match the seeded client exactly, so keep
your BFF on port `3000` locally (or update `seed.json` and re-seed).

> **Development secret:** `bcordes-bff-secret` and the sample `COOKIE_PASSWORD`
> are for local development only. Provision distinct, high-entropy values for
> every deployed environment.

---

## Security model

- **Tokens never reach the browser.** The access token, refresh token, and
  `id_token` live only inside the sealed session cookie, which is `httpOnly` and
  unreadable by JavaScript. The browser holds an opaque, encrypted blob.
- **Same-origin by design.** Serve the browser app and the BFF (`/bff/*` and
  `/api/**`) from the same origin. The session cookie is scoped to that origin,
  and `configureWallowClient()` sends it with `credentials: "include"`.
- **Confidential client.** Unlike a public SPA using PKCE alone, the BFF is a
  confidential client: it authenticates to the token endpoint with
  `OIDC_CLIENT_SECRET` in addition to PKCE, so a leaked authorization code
  cannot be redeemed without the server secret.
- **Silent refresh, server-side.** Token refresh happens inside the `/api`
  proxy using the stored refresh token; the browser is never involved and never
  sees rotated tokens.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `Missing required environment variable: ...` on startup | A required env var is unset or empty | Set every required key in the [environment variables](#environment-variables) table |
| `getUser()` always returns `null` | Session cookie not being sent | Serve the app and BFF on the same origin; confirm `configureWallowClient()` runs before any call |
| `invalid_client` on callback | `OIDC_CLIENT_ID`/`OIDC_CLIENT_SECRET` mismatch | Confirm they match the registered (or seeded) confidential client |
| `redirect_uri` mismatch | `OIDC_REDIRECT_URI` does not match the registered URI | Register `http://localhost:3000/bff/callback` (or your value) and keep them identical |
| `401` from `/api/**` after login | Session missing or refresh token unavailable | Ensure `offline_access` is in the requested scopes so a refresh token is issued |
| `npm install` `401 Unauthorized` | GitHub Packages token missing or lacks `read:packages` | Set `GITHUB_TOKEN` and the `@bc-solutions-coder:registry` line in `.npmrc` |

---

## See also

- [BFF Pattern Integration Guide](bff-pattern.md) — the underlying protocol the
  SDK implements.
- [External Auth Setup](external-auth.md) — configuring Wallow as an identity
  provider.
- [DCR Integration](dcr-integration.md) — dynamic client registration.
