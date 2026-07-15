# @bc-solutions-coder/sdk

TypeScript SDK for Wallow. It ships two entry points:

| Import                           | Runs in | Contains                                                                                                   |
| -------------------------------- | ------- | ---------------------------------------------------------------------------------------------------------- |
| `@bc-solutions-coder/sdk`        | Browser | `configureBffClient()`, `login()`, `logout()`, `getUser()`, and the generated typed API operations         |
| `@bc-solutions-coder/sdk/server` | Node    | `createBffHandlers()`, `createApiProxy()`, `loadBffConfigFromEnv()`, the session stores, and `WallowError` |

The browser never holds a token. Your server runs the OIDC Authorization Code
flow with PKCE, keeps the token set in a session (sealed cookie or Valkey), and
attaches the `Authorization: Bearer` header when it proxies `/api/**` calls to
the Wallow API.

For the full narrative guide — protocol diagrams, the seeded local `bcordes-bff`
client, publishing, troubleshooting — see
[`docs/integrations/typescript-sdk.md`](../../docs/integrations/typescript-sdk.md).
A runnable host lives in [`apps/tanstack-min/`](../../apps/tanstack-min).

---

## Install

The package is published to GitHub Packages, so point the `@bc-solutions-coder`
scope at that registry in your project's `.npmrc` and authenticate with a token
that has `read:packages`:

```ini
@bc-solutions-coder:registry=https://npm.pkg.github.com
//npm.pkg.github.com/:_authToken=${GITHUB_TOKEN}
```

```bash
pnpm add @bc-solutions-coder/sdk h3
```

`h3` is the server-side handler runtime; install it alongside the SDK unless
your host framework already provides it.

---

## Onboarding

### 1. Configure the environment

`loadBffConfigFromEnv()` builds a `BffConfig` from `process.env` and throws on
startup if a required key is missing or empty.

| Variable                        | Required | Default                                           | Description                                                                                                                                                         |
| ------------------------------- | -------- | ------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `OIDC_ISSUER`                   | Yes      | —                                                 | Issuer base URL, e.g. `https://auth.example.com`                                                                                                                    |
| `OIDC_CLIENT_ID`                | Yes      | —                                                 | Confidential client id                                                                                                                                              |
| `OIDC_CLIENT_SECRET`            | Yes      | —                                                 | Confidential client secret (server-side only)                                                                                                                       |
| `OIDC_REDIRECT_URI`             | Yes      | —                                                 | Absolute callback URL, e.g. `http://localhost:3000/bff/callback`                                                                                                    |
| `OIDC_POST_LOGOUT_REDIRECT_URI` | Yes      | —                                                 | Absolute URL to land on after logout                                                                                                                                |
| `BFF_API_BASE_URL`              | Yes      | —                                                 | Downstream API the `/api` proxy forwards to                                                                                                                         |
| `COOKIE_PASSWORD`               | Yes      | —                                                 | Secret (32+ chars) used to seal the session, transaction, and store-reference cookies                                                                               |
| `OIDC_SCOPES`                   | No       | `openid profile email offline_access`             | Space-separated scopes                                                                                                                                              |
| `COOKIE_NAME`                   | No       | `wallow_bff`                                      | Session cookie name                                                                                                                                                 |
| `OIDC_METADATA_URL`             | No       | `${OIDC_ISSUER}/.well-known/openid-configuration` | Server-side discovery URL for split-horizon DNS — the backchannel uses its `token_endpoint`, while browser-facing redirects stay pinned to the public issuer origin |
| `SESSION_TTL_SECONDS`           | No       | `86400`                                           | Session cookie `Max-Age`. Must be a positive whole number; a malformed value throws rather than falling back                                                        |
| `COOKIE_SECURE`                 | No       | `true`                                            | `Secure` flag on the session, transaction, and CSRF cookies. Fails secure: only the literal `false` clears it — set it for plain-HTTP local development             |

`OIDC_CLIENT_SECRET` and `COOKIE_PASSWORD` are confidential. They belong in the
server process environment or a secrets manager, never in the browser bundle or
source control.

### 2. Choose a session store

`SessionStore` decides where the token set lives. Both stores implement the same
interface, so swapping one for the other is a one-line change.

- **`CookieSessionStore`** — seals the whole session into the session cookie. No
  infrastructure to run. This is the default when you omit the `store` argument,
  so a single-argument `createBffHandlers(config)` still works.
- **`ValkeySessionStore`** — keeps the session in a Redis-compatible server and
  puts only an opaque sealed session id in the cookie. Use this in production:
  the cookie stays small, sessions can be revoked server-side, and concurrent
  token refreshes for one session are serialized by a refresh lock.

```ts
import {
  CookieSessionStore,
  ValkeySessionStore,
  loadBffConfigFromEnv,
  type BffConfig,
  type SessionStore,
} from "@bc-solutions-coder/sdk/server";

const config: BffConfig = loadBffConfigFromEnv();

// Simple apps: everything in the sealed cookie.
const store: SessionStore = new CookieSessionStore({
  password: config.cookiePassword,
});
```

`ValkeySessionStore` takes any client that satisfies the `RedisLike` interface —
`get`, `set` (with optional `ex` / `nx` flags), and `del` — so no concrete Redis
dependency is baked into the SDK. Wrap the client you already use. With
[`ioredis`](https://github.com/redis/ioredis):

```ts
import Redis from "ioredis";
import {
  ValkeySessionStore,
  type RedisLike,
  type SessionStore,
} from "@bc-solutions-coder/sdk/server";

const redis: Redis = new Redis(process.env.VALKEY_URL ?? "redis://localhost:6379");

const adapter: RedisLike = {
  get: (key: string): Promise<string | null> => redis.get(key),
  set: (key: string, value: string, opts?: { ex?: number; nx?: boolean }): Promise<"OK" | null> => {
    if (opts?.ex !== undefined && opts.nx === true) {
      return redis.set(key, value, "EX", opts.ex, "NX");
    }
    if (opts?.ex !== undefined) {
      return redis.set(key, value, "EX", opts.ex);
    }
    if (opts?.nx === true) {
      return redis.set(key, value, "NX");
    }
    return redis.set(key, value);
  },
  del: (key: string): Promise<number> => redis.del(key),
};

const store: SessionStore = new ValkeySessionStore({
  client: adapter,
  password: config.cookiePassword,
  ttlSeconds: config.sessionTtlSeconds, // record TTL; defaults to 86400
  lockTtlSeconds: 10, // refresh-lock TTL; defaults to 10
  keyPrefix: "wallow", // keys are <prefix>:session:<id> and <prefix>:refreshlock:<id>
});
```

The `nx` flag must reach the server as a real conditional set — that is what
makes the refresh lock a lock. An adapter that drops it will let concurrent
refreshes race.

### 3. Mount the handlers

`createBffHandlers(config, store)` returns four h3 `defineEventHandler` objects;
`createApiProxy(config, store)` returns the reverse proxy. Pass the **same store
instance** to both. Both default the store to a `CookieSessionStore` built from
`config.cookiePassword` when you omit it.

```ts
import { createApp, createRouter, toNodeListener, type App, type Router } from "h3";
import { createServer } from "node:http";
import {
  createApiProxy,
  createBffHandlers,
  type BffHandlers,
} from "@bc-solutions-coder/sdk/server";

const bff: BffHandlers = createBffHandlers(config, store);
const apiProxy = createApiProxy(config, store);

const router: Router = createRouter();
router.use("/bff/login", bff.login);
router.use("/bff/callback", bff.callback);
router.get("/bff/user", bff.user);
router.use("/bff/logout", bff.logout);
router.use("/api/**", apiProxy); // proxy strips the /api prefix itself

const app: App = createApp();
app.use(router);
createServer(toNodeListener(app)).listen(3000);
```

In TanStack Start (or any h3-compatible framework) these same handler objects
drop directly into a catch-all server route per prefix.

### 4. Configure the browser client

```ts
import { configureBffClient, getUser, login, logout } from "@bc-solutions-coder/sdk";

configureBffClient(); // baseUrl "/api", credentials: "include"

const user = await getUser(); // WallowUser | null (null when unauthenticated)
if (user === null) {
  login("/dashboard"); // -> /bff/login?returnTo=/dashboard
}
```

Call it once at startup, before any generated operation. Pass
`configureBffClient({ baseUrl: "https://app.example.com/api" })` when the app is
not served from the BFF's origin.

`configureWallowClient` is a deprecated alias for the same function (as is the
`WallowClientOptions` type for `BffClientOptions`); it will be removed in a
future major release.

---

## CSRF: read this before your first POST

The proxy **rejects every state-changing request that does not carry a CSRF
token** with `403` and the code `CSRF_INVALID`. If your `POST`/`PUT`/`PATCH`/
`DELETE` calls through `/api/**` come back as 403, this is why.

How the token is delivered:

- On successful login the callback mints a synchronizer token, stores it inside
  the sealed session, and writes it to a companion cookie named
  `<COOKIE_NAME>-csrf` (default: `wallow_bff-csrf`). That cookie is deliberately
  **not** `HttpOnly` — browser JavaScript is meant to read it. It carries no
  credential of its own; the session cookie remains `HttpOnly`.
- `GET /bff/user` also returns the token as `csrfToken` in its JSON body.

Echo it back in the `x-csrf-token` header on every state-changing request:

```ts
function csrfToken(): string {
  const match: RegExpMatchArray | null = document.cookie.match(/(?:^|;\s*)wallow_bff-csrf=([^;]*)/);
  return match === null ? "" : decodeURIComponent(match[1]);
}

await fetch("/api/v1/inquiries", {
  method: "POST",
  credentials: "include",
  headers: {
    "content-type": "application/json",
    "x-csrf-token": csrfToken(),
  },
  body: JSON.stringify({ subject: "Hello" }),
});
```

To attach it to every generated typed operation instead of hand-rolling each
call, register a request interceptor on the shared client:

```ts
import { client, configureBffClient } from "@bc-solutions-coder/sdk";

configureBffClient();

client.interceptors.request.use((request: Request): Request => {
  if (request.method !== "GET" && request.method !== "HEAD") {
    request.headers.set("x-csrf-token", csrfToken());
  }
  return request;
});
```

The header name is exported server-side as `CSRF_HEADER`, and the rejection code
as `CSRF_INVALID_CODE`. `GET`, `HEAD`, `OPTIONS`, and `TRACE` are not gated.

---

## Errors and resilience

The proxy answers failures with RFC 7807 problem details
(`content-type: application/problem+json`), so a failed call carries a machine
readable `code` alongside the status.

Server-side, `WallowError` is the SDK's error type (`status`, `code`, `title`,
`detail`) and `parseProblemDetails(response, bodyText)` turns an upstream body
into one, falling back to `UNKNOWN_ERROR_CODE` when the body is not problem
details. `redact(value)` replaces secrets with `REDACTED` for safe logging.

What the proxy does for you on the way through, each retried at most once:

| Upstream                                            | Behavior                                                                    |
| --------------------------------------------------- | --------------------------------------------------------------------------- |
| `401` (or a `3xx` redirect to the API's login page) | Force a token refresh under the store's refresh lock and replay the request |
| `429`                                               | Wait for `Retry-After`, bounded by `MAX_RETRY_AFTER_MS` (5s), and replay    |
| No response within `FORWARD_TIMEOUT_MS` (30s)       | `503` with code `NETWORK_TIMEOUT`                                           |
| Transport failure                                   | `503` with code `NETWORK_ERROR`                                             |

Ahead of the forward, `ensureFreshSession` proactively refreshes an access token
that is inside the expiry skew window, so most requests never see a 401 at all.

---

## Development

```bash
pnpm install
pnpm test        # vitest
pnpm typecheck   # tsc --noEmit
pnpm build       # tsup -> dist/
pnpm generate    # regenerate src/generated from openapi/v1.json
```

The generated client is wired to the BFF at construction time through
`runtimeConfigPath` in `openapi-ts.config.ts`, which points at
`src/runtime-config.ts` — that is why generated operations already target `/api`
with `credentials: "include"` even before `configureBffClient()` runs.
