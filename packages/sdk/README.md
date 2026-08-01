# @bc-solutions-coder/sdk

TypeScript SDK for Wallow. It ships four entry points:

| Import                                       | Runs in                                             | Contains                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| -------------------------------------------- | --------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `@bc-solutions-coder/sdk`                    | Browser (also safe to import from a Node SSR entry) | `createWallowSdk()` (the per-request factory), `login()`, `logout()`, `getUser()`, the generated typed API operations, the CSRF module (`isSafeMethod`, `setCsrfToken`, `wireCsrfInterceptor`), the OIDC URL builders (`buildConnectAuthorizeUrl`, `buildConnectLogoutUrl`, `buildConsentSubmitUrl`, `buildExchangeTicketUrl`, `isSafeReturnUrl`), the role helpers (`getRoles`, `hasRole`, `isAdmin`, `requireAuth`), and `WallowError` / `isWallowError` |
| `@bc-solutions-coder/sdk/server`             | Node                                                | `createWallowBffServer()` (the host preset), `createBffHandlers()`, `createApiProxy()`, `loadBffConfigFromEnv()`, the session stores, and `WallowError`                                                                                                                                                                                                                                                                                                    |
| `@bc-solutions-coder/sdk/server/passthrough` | Node                                                | `createApiPassthrough()` — a pure reverse proxy owning no session, forwarding the upstream response (`Set-Cookie` included) verbatim. Kept on its own subpath so a passthrough-only app never pulls `openid-client` into its server bundle                                                                                                                                                                                                                 |
| `@bc-solutions-coder/sdk/query`              | Browser                                             | The TanStack Query layer (peer dep `@tanstack/react-query`): a generated `{op}Options()` / `{op}QueryKey()` / `{op}Mutation()` trio per OpenAPI operation, plus the curated invalidation predicates `queriesForOperation()` and `queriesWithTag()` — the only hand-written module left on this entry                                                                                                                                                       |

Every server handler is a web-standard `(request: Request) => Promise<Response>`.
The SDK declares no host framework, so the handlers mount on TanStack Start server
routes, Nitro, Hono, or a bare Fetch handler alike.

The browser never holds a token. Your server runs the OIDC Authorization Code
flow with PKCE, keeps the token set in a session (sealed cookie or Valkey), and
attaches the `Authorization: Bearer` header when it proxies `/api/**` calls to
the Wallow API.

For the full narrative guide — protocol diagrams, the seeded local `bcordes-bff`
client, publishing, troubleshooting — see
[`docs/integrations/typescript-sdk.md`](../../docs/integrations/typescript-sdk.md).
A runnable host lives in [`apps/wallow-web/`](../../apps/wallow-web).

---

## Install

The package is published to GitHub Packages, so point the `@bc-solutions-coder`
scope at that registry in your project's `.npmrc`:

```ini
@bc-solutions-coder:registry=https://npm.pkg.github.com
```

Then authenticate with a token that has `read:packages`. It goes in your
**user-level** config — pnpm will not expand `${GITHUB_TOKEN}` out of a
committed project `.npmrc`, since such a file could be edited to redirect the
registry and leak the token:

```bash
pnpm config set "//npm.pkg.github.com/:_authToken" "$GITHUB_TOKEN"
pnpm add @bc-solutions-coder/sdk
```

That is the whole install — there is no companion host-runtime package to add.

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

`createWallowBffServer()` is the golden path: it does steps 1 and 2 for you —
loads the config, selects a store, builds the tunnel handlers and the `/api`
proxy over that one shared instance — and returns three functions to mount:

```ts
import {
  createWallowBffServer,
  WALLOW_API_MOUNT, // "/api"
  WALLOW_BFF_MOUNT, // "/bff"
  type WallowBffServer,
} from "@bc-solutions-coder/sdk/server";

const server: WallowBffServer = createWallowBffServer();

// server.handleBff(request)    -> /bff/login | /bff/callback | /bff/user | /bff/logout
// server.handleApi(request)    -> /api/**  (the proxy strips the prefix itself)
// server.handleHealth()        -> 200 liveness JSON
```

Build it on **first use and memoise**, not at module load — a config throw at
import time takes the whole server bundle down with it, and a failed build that
gets cached turns a transient store outage into a permanently dead BFF. When
`REDIS_URL` is set, pass a connected client as `redisClient`: the SDK never
imports `redis`, and the preset throws rather than silently serving stateless
cookie sessions to a deployment that asked for server-side ones.

In TanStack Start, mount each prefix as a splat server route with a single `ANY`
handler — method policy belongs to the handlers (a bare `GET /bff/logout` answers
`405` + `Allow: POST`), and a method-filtered route would swallow that as a local 404. Elsewhere, any router that dispatches a `Request` works: these are plain
`(request: Request) => Promise<Response>` functions.

To assemble the pieces yourself instead, `createBffHandlers(config, store)` returns
the four tunnel handlers and `createApiProxy(config, store)` the reverse proxy.
Pass the **same store instance** to both — the proxy has to resolve the sessions
the callback wrote. Both default the store to a `CookieSessionStore` built from
`config.cookiePassword` when you omit it.

### 3b. Or: the pure passthrough

An app that only needs Wallow's API and OIDC endpoints on its own origin — no
session, no bearer — uses the sibling preset instead:

```ts
import { createApiPassthrough } from "@bc-solutions-coder/sdk/server/passthrough";

// defaults: /v1/**, /connect/**, /.well-known/**  ->  WALLOW_API_INTERNAL_URL
const passthrough = createApiPassthrough();

const response: Response = await passthrough.handle(request);
```

It forwards the inbound method, path, query, body, and `Cookie` header upstream and
returns the response unchanged, so every `Set-Cookie` reaches the browser verbatim.
Keep `/.well-known/**` in the prefix list: an OIDC client pointed at this origin
resolves discovery there and fetches signing keys from the `jwks_uri` that document
advertises, so dropping it 404s discovery and breaks login with no useful error.
Stamp the peer address onto the `x-wallow-client-ip` header before calling
`handle()` and the passthrough appends it to any inbound `X-Forwarded-For` chain,
then strips the seam header before the upstream hop.

### 4. Build an SDK instance per request

```ts
import { createWallowSdk, getUser, login } from "@bc-solutions-coder/sdk";

const sdk = createWallowSdk({ baseUrl: "/api" });

const user = await getUser(); // WallowUser | null (null when unauthenticated)
if (user === null) {
  login("/dashboard"); // -> /bff/login?returnTo=/dashboard
}
```

`baseUrl` is REQUIRED and has no default, because the right value differs by
caller: the browser wants the same-origin relative BFF path (`/api`), while an
SSR render wants an absolute origin Node's `fetch` can parse. Pass the full
origin (`https://app.example.com/api`) when the app is not served from the BFF's
origin.

Bind a generated operation to the instance through the standard `{ client }`
call option — `usersGetCurrentUser({ client: sdk.client })` — and lift the
instance into your router context so components read it rather than importing
one.

**There is no module-global client and no configure step.** A singleton is safe
in a browser (one document, one session) and wrong on a server, where concurrent
renders share the module graph: the last request to configure wins, its cookie
leaks into another user's render, and interceptors pile up on every
re-configure. `createWallowSdk()` builds a fresh generated client per call with
its own `baseUrl`, cookie, and interceptor list. The old
`configureBffClient()` / `configureWallowClient()` / `client` exports are gone,
not deprecated — reaching for one is a build error rather than a silently
unconfigured shared client.

Two options exist for the server case: `cookieHeader` forwards the inbound
session cookie (Node's `fetch` has no cookie jar, so an SSR render must carry it
explicitly), and `internalOrigin` rewrites the outgoing request's origin when
the host reaches itself on a different address than the browser does. The latter
applies inside the instance's `fetch` only, leaving the configured `baseUrl`
alone so a server instance and a browser instance stay hydration-compatible.

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

The SDK's `csrf` module owns the client side of this exchange, so you never
hand-roll a request interceptor or read the companion cookie yourself.
`createWallowSdk()` already wires the interceptor onto every instance it builds,
which leaves you one job — telling it the token:

```ts
import { createWallowSdk, getUser, setCsrfToken } from "@bc-solutions-coder/sdk";

const sdk = createWallowSdk({ baseUrl: "/api" }); // CSRF interceptor already wired

const user = await getUser();
setCsrfToken(user === null ? null : typeof user.csrfToken === "string" ? user.csrfToken : null);
```

- `wireCsrfInterceptor(client)` registers a request interceptor exactly once:
  it stamps the in-memory token into `x-csrf-token` on every request whose
  method is not CSRF-exempt, and leaves safe methods and the token-less state
  untouched. It accepts anything shaping up like the generated client
  (`CsrfInterceptorClient`), so it also wires onto a client you build yourself —
  but you only need to call it directly for a client the factory did not build.
- `setCsrfToken(token)` updates the in-memory token the interceptor reads
  live — call it once you have read `csrfToken` off the `/bff/user` response
  (or `null` it out on logout).
- `isSafeMethod(method)` is the RFC 9110 safe-method check
  (`GET`/`HEAD`/`OPTIONS`) the interceptor uses internally; it is exported in
  case a host needs the same rule elsewhere.

The header name is exported server-side as `CSRF_HEADER`, and the rejection code
as `CSRF_INVALID_CODE`. `GET`, `HEAD`, `OPTIONS`, and `TRACE` are not gated.

---

## Server-rendered loaders (SSR)

A same-origin BFF app that server-renders authenticated routes (e.g. a TanStack
Start `loader`) needs two things a browser tab gets for free: an ABSOLUTE origin
(Node's `fetch` cannot resolve a relative `/api` URL) and the incoming request's
session cookie (Node has no cookie jar, so `credentials: "include"` sends an
anonymous request). Both are per-request, and both are constructor arguments:

```ts
// global request middleware — runs once per request
import { createWallowSdk, type WallowSdk } from "@bc-solutions-coder/sdk";
import { createMiddleware } from "@tanstack/react-start";

const sdkMiddleware = createMiddleware().server(({ next, request }) => {
  const origin: string = new URL(request.url).origin;

  const sdk: WallowSdk = createWallowSdk({
    baseUrl: `${origin}/api`,
    cookieHeader: request.headers.get("cookie") ?? undefined,
    internalOrigin: process.env.WALLOW_WEB_INTERNAL_URL,
  });

  return next({ context: { sdk } });
});
```

Lift that instance into the router context and every loader and component reads
it back out (`useRouteContext({ from: "__root__" }).sdk`) instead of importing a
client. In the browser the same router builds one with the relative `baseUrl`
`/api` and no cookie header, so the two halves of a hydrating render agree.

`internalOrigin` covers the case where the host reaches ITSELF on a different
address than the browser uses — a container published as `127.0.0.1:5053:3000`
cannot self-fetch the browser's origin, and every SSR'd page would fall back to
an error boundary. It rewrites the outgoing request's origin inside the
instance's `fetch` only, leaving the configured `baseUrl` (and therefore the
request identity an SSR-primed cache shares with the browser) untouched.

Nothing here reads module scope, so there is no `AsyncLocalStorage` to own and
no resolver to register. The old request-context seam — `configureSsrClient`,
`getSsrRequestContext`, `setSsrRequestContextResolver`,
`wireSsrCookieInterceptor` — existed only to feed per-request values to a
module-global client, and is deleted along with it.
[`apps/wallow-web/src/start.ts`](../../apps/wallow-web/src/start.ts) is the
reference host.

---

## The query layer

`@bc-solutions-coder/sdk/query` is generated from the same OpenAPI document as
the operations, giving every operation a `{op}Options()` for reads, a
`{op}Mutation()` for writes, and a `{op}QueryKey()` for both. Each takes the
request-scoped client as a call option:

```tsx
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouteContext } from "@tanstack/react-router";
import {
  inquiriesGetAllOptions,
  inquiriesGetAllQueryKey,
  inquiriesSubmitMutation,
  queriesForOperation,
} from "@bc-solutions-coder/sdk/query";

function Inquiries() {
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();

  const list = useQuery(inquiriesGetAllOptions({ client: sdk.client }));

  const submit = useMutation({
    ...inquiriesSubmitMutation({ client: sdk.client }),
    onSuccess: () => {
      void queryClient.invalidateQueries(
        queriesForOperation(inquiriesGetAllQueryKey({ client: sdk.client })),
      );
    },
  });

  return list.data === undefined ? null : (
    <InquiryTable rows={list.data} onSubmit={submit.mutate} />
  );
}
```

Operations are generated with `responseStyle: "data"` and `throwOnError: true`,
so a hook's `data` is the response BODY (no `{ data, error }` envelope to
unwrap) and every failure arrives as a thrown `WallowError` on `error`.

**Generated keys are FLAT, not hierarchical.** A key is a single-element array
holding one object — `[{ _id, baseUrl, tags, ...args }]` — so there is no
prefix to invalidate a subtree with, and a key is not knowable without the
client, because it embeds that client's `baseUrl`. Never write a key literal;
always call the factory. The two curated predicates bridge the gap:

- `queriesForOperation(key)` — matches every cached query for the operation
  that key belongs to, whatever arguments it was called with.
- `queriesWithTag(tag)` — matches every query carrying an OpenAPI tag, for the
  broader sweep after a write that touches a whole domain.

The hand-written layer this replaced — the `queryKeys` registry, the per-domain
`userQueries` / `authQueries` / `mfaQueries` / `organizationsQueries` /
`appsQueries` / `inquiriesQueries` / `settingsQueries` namespaces, and
`registerQueryBootstrap` / `ensureQueryBootstrapped` — is deleted rather than
deprecated: every one of them closed over the module-global client that no
longer exists.

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
pnpm build       # vite build (library mode) + tsc -p tsconfig.build.json -> dist/
pnpm generate    # regenerate src/generated from openapi/v1.json
```

`pnpm build` is a two-stage pipeline: **Vite 8 in library mode** (`vite build`,
ESM output) emits the JavaScript bundle for both the browser (`.`) and Node
(`./server`) entry points, then **`tsc -p tsconfig.build.json`** does a
declaration-only pass to emit the `.d.ts` files alongside it. There is no
separate bundler config — the build is driven entirely by `vite.config.ts` plus
that declaration-only tsconfig.

The generated client is wired to the BFF at construction time through
`runtimeConfigPath` in `openapi-ts.config.ts`, which points at
`src/runtime-config.ts` — that is why generated operations already target `/api`
with `credentials: "include"`, and why they reject with a `WallowError` rather
than resolving an `{ data, error }` envelope.
