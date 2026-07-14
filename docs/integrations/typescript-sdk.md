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

## Publishing the SDK

The SDK is versioned and released **independently of the platform**. It does not
piggyback on the release-please `vX.Y.Z` releases (the platform version, e.g.
`v3.2.1`) — pushing a platform tag or cutting a platform release does **not**
publish the SDK.

Publish a new SDK version in one of two ways:

- **Push an `sdk-v<version>` tag** — the `sdk-publish` workflow strips the
  `sdk-v` prefix and publishes that version:

  ```bash
  git tag sdk-v0.1.0
  git push origin sdk-v0.1.0
  ```

- **Run the `sdk-publish` workflow manually** from the Actions tab (or via
  `gh workflow run sdk-publish.yml -f version=0.1.0`), providing the version
  (no leading `v`) as the required `version` input.

Either path installs, tests, and builds the SDK, syncs `package.json` to the
requested version, and publishes to GitHub Packages. The SDK version is chosen
independently and has no relationship to the platform `vX.Y.Z` release-please
versions.

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
| `OIDC_METADATA_URL` | No | Server-side discovery URL, for split-horizon DNS where the issuer is reachable under different hostnames from the browser and the server. The backchannel uses its `token_endpoint`; browser-facing redirects stay pinned to the public `OIDC_ISSUER` origin. Defaults to `${OIDC_ISSUER}/.well-known/openid-configuration` |
| `SESSION_TTL_SECONDS` | No | Lifetime of the session cookie, written as its `Max-Age`, so a stale browser cookie cannot outlive the session it references. Must be a positive whole number — a malformed value throws at startup rather than silently falling back. Defaults to `86400` (24 hours) |
| `COOKIE_SECURE` | No | Whether the session, transaction, and CSRF cookies carry the `Secure` flag. Fails secure: only the literal `false` clears it. Set `COOKIE_SECURE=false` for plain-HTTP local development. Defaults to `true` |

> **Confidential values:** `OIDC_CLIENT_SECRET` and `COOKIE_PASSWORD` must never
> be shipped to the browser or committed to source control. They belong in the
> server process environment (or a secrets manager) only.

---

## Session stores

Where the token set lives is pluggable. `createBffHandlers(config, store)` and
`createApiProxy(config, store)` both accept a `SessionStore` as an optional
second argument — pass the **same instance** to both. Omitting it defaults to a
cookie-only store built from `COOKIE_PASSWORD`, so single-argument callers keep
working.

| Store | Where the session lives | Use it when |
|-------|------------------------|-------------|
| `CookieSessionStore` | Sealed into the session cookie itself | Simple apps and local development — nothing extra to run |
| `ValkeySessionStore` | In a Redis-compatible server; the cookie holds only an opaque sealed session id | Production — small cookies, server-side revocation, and a refresh lock that serializes concurrent token refreshes for one session |

```ts
import {
  CookieSessionStore,
  loadBffConfigFromEnv,
  type BffConfig,
  type SessionStore,
} from "@bc-solutions-coder/sdk/server";

const config: BffConfig = loadBffConfigFromEnv();
const store: SessionStore = new CookieSessionStore({
  password: config.cookiePassword,
});
```

`ValkeySessionStore` takes any client satisfying the `RedisLike` interface
(`get`, `set` with optional `ex`/`nx` flags, `del`), so the SDK carries no
concrete Redis dependency — you adapt the client you already run. The `nx` flag
must reach the server as a real conditional set; that is what makes the refresh
lock a lock. The package README shows a complete `ioredis` adapter:
[`packages/typescript-sdk/README.md`](https://github.com/bc-solutions-coder/wallow/blob/main/packages/typescript-sdk/README.md).

---

## CSRF protection

The `/api` proxy **rejects every state-changing request that does not present a
valid CSRF token**, answering `403` with the code `CSRF_INVALID`. This is the
first thing to reach for when a `POST`, `PUT`, `PATCH`, or `DELETE` through the
tunnel comes back as `403`. Safe methods (`GET`, `HEAD`, `OPTIONS`, `TRACE`) are
not gated.

The SDK uses a synchronizer token with a double-submit companion cookie:

1. On successful login, `/bff/callback` mints a token, stores it inside the
   sealed session, and writes it to a cookie named `<COOKIE_NAME>-csrf`
   (default `wallow_bff-csrf`). That cookie is deliberately **not** `HttpOnly`,
   because browser JavaScript must read it. It carries no credential of its own
   — the session cookie stays `HttpOnly`, and the token is worthless without it.
2. `GET /bff/user` returns the same token as `csrfToken` in its JSON body.
3. The browser echoes it in the `x-csrf-token` header on every state-changing
   request. The proxy compares it against the session-bound token in constant
   time before refreshing anything or forwarding anything upstream.

```ts
import { client, configureBffClient } from "@bc-solutions-coder/sdk";

configureBffClient();

function csrfToken(): string {
  const match: RegExpMatchArray | null = document.cookie.match(
    /(?:^|;\s*)wallow_bff-csrf=([^;]*)/,
  );
  return match === null ? "" : decodeURIComponent(match[1]);
}

// Attach the token to every generated operation that mutates state.
client.interceptors.request.use((request: Request): Request => {
  if (request.method !== "GET" && request.method !== "HEAD") {
    request.headers.set("x-csrf-token", csrfToken());
  }
  return request;
});
```

Server-side the header name is exported as `CSRF_HEADER` and the rejection code
as `CSRF_INVALID_CODE`, so a BFF host can reuse them rather than hardcode
strings.

---

## Error handling and resilience

Proxy failures come back as RFC 7807 problem details
(`content-type: application/problem+json`), so every failure carries a
machine-readable `code` alongside its status. On the server, `WallowError`
(`status`, `code`, `title`, `detail`) is the SDK's error type and
`parseProblemDetails(response, bodyText)` converts an upstream body into one,
falling back to `UNKNOWN_ERROR_CODE` when the body is not problem details.
`redact(value)` masks secrets as `REDACTED` for safe logging.

Before forwarding, `ensureFreshSession` proactively refreshes an access token
already inside the expiry-skew window. Beyond that, the forward itself handles
the following, each retried at most once:

| Upstream response | What the proxy does |
|-------------------|---------------------|
| `401`, or a `3xx` redirecting to the API's login page | Forces a token refresh under the store's refresh lock and replays the request |
| `429` | Waits for `Retry-After`, bounded by `MAX_RETRY_AFTER_MS` (5s), then replays |
| No response within `FORWARD_TIMEOUT_MS` (30s) | Returns `503` with code `NETWORK_TIMEOUT` |
| Transport failure | Returns `503` with code `NETWORK_ERROR` |

---

## Browser API

Configure the shared client once at startup, then use the three auth helpers.

```ts
import { configureBffClient, getUser, login, logout } from "@bc-solutions-coder/sdk";

// Point the typed client at the same-origin BFF proxy and send the cookie.
configureBffClient(); // defaults baseUrl to "/api", credentials: "include"

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

`configureBffClient()` points the generated Hey API client at the `/api`
proxy with `credentials: "include"`, so every generated call rides the sealed
session cookie and is transparently authenticated by the BFF. Import the
generated operation functions from `@bc-solutions-coder/sdk` and call them directly — no
token handling in the browser:

```ts
import { configureBffClient } from "@bc-solutions-coder/sdk";
// import { getInquiries } from "@bc-solutions-coder/sdk"; // generated typed operation

configureBffClient();

// const { data } = await getInquiries();
// data is fully typed from the OpenAPI schema; the request went
// browser -> /api proxy -> Wallow API with a server-attached Bearer token.
```

If your app is not served from the same origin as the BFF, pass an explicit
base URL: `configureBffClient({ baseUrl: "https://app.example.com/api" })`.

> [!NOTE]
> `configureBffClient` was previously named `configureWallowClient`. The old
> name is still exported as a deprecated alias to the same function (and
> `WallowClientOptions` to `BffClientOptions`), so existing code keeps working.
> It will be removed in a future major release — prefer `configureBffClient`.

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
  and `configureBffClient()` sends it with `credentials: "include"`.
- **Confidential client.** Unlike a public SPA using PKCE alone, the BFF is a
  confidential client: it authenticates to the token endpoint with
  `OIDC_CLIENT_SECRET` in addition to PKCE, so a leaked authorization code
  cannot be redeemed without the server secret.
- **Silent refresh, server-side.** Token refresh happens inside the `/api`
  proxy using the stored refresh token; the browser is never involved and never
  sees rotated tokens.
- **CSRF-gated mutations.** Because the session rides a cookie, every
  state-changing request must present a session-bound token in `x-csrf-token`,
  compared in constant time before anything is forwarded. See
  [CSRF protection](#csrf-protection).
- **Bounded cookie lifetime.** The session cookie's `Max-Age` is pinned to
  `SESSION_TTL_SECONDS`, so a stale browser cookie cannot outlive the session it
  references, and cookies are `Secure` unless `COOKIE_SECURE=false` is set
  explicitly for local HTTP.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `Missing required environment variable: ...` on startup | A required env var is unset or empty | Set every required key in the [environment variables](#environment-variables) table |
| `getUser()` always returns `null` | Session cookie not being sent | Serve the app and BFF on the same origin; confirm `configureBffClient()` runs before any call |
| `invalid_client` on callback | `OIDC_CLIENT_ID`/`OIDC_CLIENT_SECRET` mismatch | Confirm they match the registered (or seeded) confidential client |
| `redirect_uri` mismatch | `OIDC_REDIRECT_URI` does not match the registered URI | Register `http://localhost:3000/bff/callback` (or your value) and keep them identical |
| `401` from `/api/**` after login | Session missing or refresh token unavailable | Ensure `offline_access` is in the requested scopes so a refresh token is issued |
| `403` with code `CSRF_INVALID` on POST/PUT/PATCH/DELETE | The `x-csrf-token` header is missing or stale | Echo the `wallow_bff-csrf` cookie (or `/bff/user`'s `csrfToken`) in the `x-csrf-token` header — see [CSRF protection](#csrf-protection) |
| Session cookie not set over plain HTTP locally | Cookies carry `Secure` by default | Set `COOKIE_SECURE=false` in local development only |
| `npm install` `401 Unauthorized` | GitHub Packages token missing or lacks `read:packages` | Set `GITHUB_TOKEN` and the `@bc-solutions-coder:registry` line in `.npmrc` |

---

## See also

- [BFF Pattern Integration Guide](bff-pattern.md) — the underlying protocol the
  SDK implements.
- [External Auth Setup](external-auth.md) — configuring Wallow as an identity
  provider.
- [DCR Integration](dcr-integration.md) — dynamic client registration.
