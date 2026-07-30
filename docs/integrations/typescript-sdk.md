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

`@bc-solutions-coder/sdk` has four entrypoints:

| Import                                       | Runs in                                             | Purpose                                                                                                                                                                                                                                                                                              |
| -------------------------------------------- | --------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `@bc-solutions-coder/sdk`                    | Browser (also safe to import from a Node SSR entry) | `createWallowSdk()` — the [per-request client factory](#browser-api) targeting the same-origin `/api` proxy — plus `login()`, `logout()`, `getUser()`, the generated typed operations, the [CSRF module](#csrf-protection), and the [SSR wiring](#per-request-instances-for-server-rendered-loaders) |
| `@bc-solutions-coder/sdk/server`             | Server (Node)                                       | The BFF tunnel: `createWallowBffServer()`, `createBffHandlers()`, `createApiProxy()`, `loadBffConfigFromEnv()`, and the session stores. Every handler is a plain `(Request) => Promise<Response>` function                                                                                           |
| `@bc-solutions-coder/sdk/server/passthrough` | Server (Node)                                       | `createApiPassthrough()` — a pure reverse proxy that owns no session and forwards the upstream response verbatim. Its own subpath so a passthrough-only app never pulls `openid-client` into its server bundle                                                                                       |
| `@bc-solutions-coder/sdk/query`              | Browser                                             | The TanStack Query layer — a generated `{op}Options()` / `{op}QueryKey()` / `{op}Mutation()` trio per OpenAPI operation, plus the curated invalidation predicates `queriesForOperation()` and `queriesWithTag()`                                                                                     |

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

That is the whole install. The server entry has no host-framework dependency —
its handlers are web-standard `Request` → `Response` functions, so they mount on
anything that speaks the Fetch API (TanStack Start server routes, Nitro, Hono,
a bare `Bun.serve`). The SDK previously required `h3`; it no longer does, and
nothing in the package imports it.

---

## Keeping the SDK in step with the API

`packages/sdk/openapi/v1.json` and `packages/sdk/src/generated/**` are build
artefacts of the backend contract, and CI keeps them honest without anyone having
to remember to regenerate them.

Both halves read the contract the same way — from the document
`Wallow.Api` emits at build time, via the shared
`.github/actions/openapi-document` composite action, so they can never disagree
about what changed:

- **On a pull request**, `openapi-drift.yml` fails if the committed snapshot no
  longer matches the contract, and prints the commands to refresh it.
- **On `main`**, `openapi-autoregen.yml` regenerates the snapshot and the typed
  client and opens a pull request titled
  `feat(sdk): regenerate OpenAPI snapshot and typed client`. Merging that PR
  feeds release-please, which bumps `@bc-solutions-coder/sdk` and lets you cut an
  `sdk-v*` tag as below.

The automated PR is byte-identical to what a manual refresh against a running API
produces, so accepting it never fights with a developer regenerating locally. Check
its commit type before merging: it is `feat(sdk):` by default, and a contract change
that removed or renamed an operation should be squashed as `feat(sdk)!:` instead so
the SDK takes a major bump.

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

`createWallowBffServer()` is the golden path: it loads the config, picks a session
store, builds the OIDC tunnel handlers and the `/api` proxy over that **one shared
store**, and dispatches by path. What comes back is three web-standard entry points —
`handleBff`, `handleApi`, and `handleHealth` — plus the resolved `config` and `store`:

```ts
// src/lib/bff.ts — build the host once, lazily
import { createWallowBffServer, type WallowBffServer } from "@bc-solutions-coder/sdk/server";

let server: WallowBffServer | undefined;

async function getServer(): Promise<WallowBffServer> {
  server ??= createWallowBffServer();
  return server;
}

export async function handleBffRequest(request: Request): Promise<Response> {
  return (await getServer()).handleBff(request);
}

export async function handleApiRequest(request: Request): Promise<Response> {
  return (await getServer()).handleApi(request);
}
```

The mount points are exported as `WALLOW_BFF_MOUNT` (`/bff`) and `WALLOW_API_MOUNT`
(`/api`) so the host and the SDK agree on the prefixes by import rather than by
repeating string literals that drift.

Build the server on **first use, not at module load**: a server-route module is
evaluated as part of the server bundle, where a config throw would take down SSR
and every other route with it. Two things stay the host's job. The SDK never
imports `redis`, so when `REDIS_URL` is set the host constructs and connects the
client and passes it as `redisClient` — `createWallowBffServer` throws rather than
silently serving stateless cookie sessions to a deployment that asked for
server-side ones. And a failed build must not be memoised, so a transient store
outage at boot does not permanently disable the BFF.

In **TanStack Start**, mount each prefix as a splat server route with a single `ANY`
handler:

```ts
// src/routes/bff/$.ts
import { createFileRoute } from "@tanstack/react-router";

export const Route = createFileRoute("/bff/$")({
  server: {
    handlers: {
      ANY: async ({ request }): Promise<Response> => {
        // dynamic import: this module pulls node:crypto + openid-client, and every
        // route module is a member of the tree the CLIENT graph also imports
        const { handleBffRequest } = await import("../../lib/bff");
        return handleBffRequest(request);
      },
    },
  },
});
```

One `ANY` handler, not a method map: method policy belongs to the SDK's handlers
(a bare `GET /bff/logout` answers `405` + `Allow: POST`), and a method-filtered
route would swallow that as a local 404.

If you would rather wire the pieces yourself, `createBffHandlers(config, store)`
and `createApiProxy(config, store)` are still exported — but pass **the same store
instance** to both, since the proxy has to resolve the sessions the login callback
wrote. Each handler is a plain `(request: Request) => Promise<Response>`; there is
no framework-specific handler object to unwrap.

A runnable reference host lives in the repository at `apps/wallow-web/` — a
TanStack Start app that mounts exactly these routes (`src/routes/bff/$.ts`,
`src/routes/api/$.ts`, `src/routes/health.ts`, over `src/lib/bff.ts`) and consumes
the proxy from its dashboard.

### The pure passthrough: `createApiPassthrough()`

An app that only needs the API to appear on its own origin — no session, no token —
uses the other preset instead. `createApiPassthrough()` forwards the inbound method,
path, query, body, and `Cookie` header to the internal API and returns the upstream
`Response` unchanged, so every `Set-Cookie` reaches the browser verbatim:

```ts
import { createApiPassthrough } from "@bc-solutions-coder/sdk/server/passthrough";

const passthrough = createApiPassthrough(); // prefixes default to /v1/**, /connect/**, /.well-known/**
```

`/.well-known/**` is required, not optional: an OIDC client whose authority points at
this origin resolves discovery at `${origin}/.well-known/openid-configuration` and
then fetches signing keys from the `jwks_uri` that document advertises — which is this
origin too. Omitting the prefix 404s discovery and breaks login with no useful error.

The upstream comes from the `internalApiUrl` option, else `WALLOW_API_INTERNAL_URL`,
else `http://localhost:5001`. To get real client IPs into the API's rate limiter, the
host stamps the peer address onto the `x-wallow-client-ip` header before calling
`handle()`; the passthrough appends it to any inbound `X-Forwarded-For` chain and
strips the seam header before the upstream hop. `apps/wallow-auth/` is the reference
consumer (`src/lib/api-passthrough.ts` plus three splat routes).

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

| Variable                        | Required | Description                                                                                                                                                                                                                                                                                                                 |
| ------------------------------- | -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `OIDC_ISSUER`                   | Yes      | OIDC issuer base URL, e.g. `https://auth.example.com`                                                                                                                                                                                                                                                                       |
| `OIDC_CLIENT_ID`                | Yes      | Confidential client identifier registered with Wallow                                                                                                                                                                                                                                                                       |
| `OIDC_CLIENT_SECRET`            | Yes      | Confidential client secret — server-side only, never exposed                                                                                                                                                                                                                                                                |
| `OIDC_REDIRECT_URI`             | Yes      | Absolute callback URL, e.g. `http://localhost:3000/bff/callback`                                                                                                                                                                                                                                                            |
| `OIDC_POST_LOGOUT_REDIRECT_URI` | Yes      | Absolute URL to land on after logout, e.g. `http://localhost:3000/`                                                                                                                                                                                                                                                         |
| `BFF_API_BASE_URL`              | Yes      | Base URL of the downstream Wallow API the proxy forwards to                                                                                                                                                                                                                                                                 |
| `COOKIE_PASSWORD`               | Yes\*    | Secret (32+ chars) used to seal/unseal the session and transaction cookies. Not required when `COOKIE_PASSWORDS` is set                                                                                                                                                                                                     |
| `COOKIE_PASSWORDS`              | No       | Keyed form of `COOKIE_PASSWORD` for rotation without logging everyone out: a JSON object of key ID to secret, e.g. `{"v2":"...","default":"..."}`. The first key seals, every key unseals, and it takes precedence over `COOKIE_PASSWORD`. See [Rotating the Cookie Password](bff-pattern.md#rotating-the-cookie-password)  |
| `OIDC_SCOPES`                   | No       | Space-separated scopes. Defaults to `openid profile email offline_access`                                                                                                                                                                                                                                                   |
| `COOKIE_NAME`                   | No       | Session cookie name. Defaults to `wallow_bff`                                                                                                                                                                                                                                                                               |
| `OIDC_METADATA_URL`             | No       | Server-side discovery URL, for split-horizon DNS where the issuer is reachable under different hostnames from the browser and the server. The backchannel uses its `token_endpoint`; browser-facing redirects stay pinned to the public `OIDC_ISSUER` origin. Defaults to `${OIDC_ISSUER}/.well-known/openid-configuration` |
| `SESSION_TTL_SECONDS`           | No       | Lifetime of the session cookie, written as its `Max-Age`, so a stale browser cookie cannot outlive the session it references. Must be a positive whole number — a malformed value throws at startup rather than silently falling back. Defaults to `86400` (24 hours)                                                       |
| `COOKIE_SECURE`                 | No       | Whether the session, transaction, and CSRF cookies carry the `Secure` flag. Fails secure: only the literal `false` clears it. Set `COOKIE_SECURE=false` for plain-HTTP local development. Defaults to `true`                                                                                                                |

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

| Store                | Where the session lives                                                         | Use it when                                                                                                                       |
| -------------------- | ------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| `CookieSessionStore` | Sealed into the session cookie itself                                           | Simple apps and local development — nothing extra to run                                                                          |
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
[`packages/sdk/README.md`](https://github.com/bc-solutions-coder/wallow/blob/main/packages/sdk/README.md).

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

The SDK's `csrf` module owns this exchange on the client side, so app code
never hand-rolls a request interceptor or reads the companion cookie itself.
`createWallowSdk()` already wires the interceptor onto every instance it builds,
which leaves app code one job — telling it the token:

```ts
import { createWallowSdk, getUser, setCsrfToken } from "@bc-solutions-coder/sdk";

const sdk = createWallowSdk({ baseUrl: "/api" }); // CSRF interceptor already wired

const user = await getUser();
setCsrfToken(user === null ? null : typeof user.csrfToken === "string" ? user.csrfToken : null);
```

- `wireCsrfInterceptor(client)` registers a request interceptor exactly once:
  it stamps the current in-memory token into `x-csrf-token` on every request
  whose method is not CSRF-exempt, and leaves safe methods (and the
  token-less, pre-login state) untouched. It takes any object shaped like the
  generated client (`CsrfInterceptorClient`), so it also wires onto a client you
  built yourself — but you only need to call it directly for a client the
  factory did not build.
- `setCsrfToken(token)` sets (or, with `null`, clears) the in-memory token the
  interceptor reads live — call it once `csrfToken` comes back on the
  `/bff/user` response, and again with `null` on logout.
- `isSafeMethod(method)` is the RFC 9110 safe-method check
  (`GET`/`HEAD`/`OPTIONS`) the interceptor uses internally; it is exported for
  hosts that need the same rule outside the interceptor.

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

| Upstream response                                     | What the proxy does                                                           |
| ----------------------------------------------------- | ----------------------------------------------------------------------------- |
| `401`, or a `3xx` redirecting to the API's login page | Forces a token refresh under the store's refresh lock and replays the request |
| `429`                                                 | Waits for `Retry-After`, bounded by `MAX_RETRY_AFTER_MS` (5s), then replays   |
| No response within `FORWARD_TIMEOUT_MS` (30s)         | Returns `503` with code `NETWORK_TIMEOUT`                                     |
| Transport failure                                     | Returns `503` with code `NETWORK_ERROR`                                       |

---

## Browser API

Build one SDK instance per request, then use the three auth helpers.

```ts
import { createWallowSdk, getUser, login, logout } from "@bc-solutions-coder/sdk";

// Point a fresh typed client at the same-origin BFF proxy.
const sdk = createWallowSdk({ baseUrl: "/api" });

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

- `createWallowSdk({ baseUrl })` — builds a request-scoped instance owning its
  own generated client, cookie, and interceptor list. `baseUrl` is REQUIRED and
  has no default: the browser passes the same-origin relative BFF path (`/api`),
  while an SSR render passes an absolute origin Node's `fetch` can parse. Pass
  the full origin (`https://app.example.com/api`) when the app is not served
  from the BFF's origin. Bind a generated operation to the instance with the
  standard `{ client }` call option.
- `login(returnTo = "/")` — navigates the browser to `/bff/login`, preserving
  where to land after a successful sign-in.
- `logout()` — navigates the browser to `/bff/logout`.
- `getUser()` — `GET /bff/user`; resolves to a `WallowUser` on `200`, `null` on
  `401` (unauthenticated), and throws on any other error. `WallowUser` always
  carries `sub` and optionally `email`/`name` plus any additional claims. It
  fetches `/bff/user` directly and so needs no SDK instance.

> [!IMPORTANT]
> **There is no module-global client and no configure step.** A singleton is
> safe in a browser (one document, one session) and wrong on a server, where
> concurrent renders share the module graph: the last request to configure wins,
> its cookie leaks into another user's render, and interceptors accumulate on
> every re-configure. The former `configureBffClient()` /
> `configureWallowClient()` / `client` exports are **deleted, not deprecated** —
> reaching for one is a build error rather than a silently unconfigured shared
> client.

### Calling module endpoints: the TanStack Query layer

`@bc-solutions-coder/sdk/query` is the golden path for reading and writing module data. It is
GENERATED from the same OpenAPI document as the operations, so every operation gets a
`{op}Options()` for reads, a `{op}Mutation()` for writes, and a `{op}QueryKey()` for both.
Each takes the request-scoped client as a call option — components read that client off the
router context rather than importing one:

```tsx
import { useMutation, useQuery, useQueryClient } from "@bc-solutions-coder/query";
import {
  inquiriesGetAllOptions,
  inquiriesGetAllQueryKey,
  inquiriesSubmitMutation,
  queriesForOperation,
} from "@bc-solutions-coder/sdk/query";
import { useRouteContext } from "@tanstack/react-router";

function InquiriesList(): React.ReactElement {
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

  // data is fully typed from the OpenAPI schema; the request went
  // browser -> /api proxy -> Wallow API with a server-attached Bearer token.
  return (
    <ul>
      {data?.map((inquiry) => (
        <li key={inquiry.id}>{inquiry.name}</li>
      ))}
    </ul>
  );
}
```

Operations are generated with `responseStyle: "data"` and `throwOnError: true`, so a hook's
`data` is the response BODY — there is no `{ data, error }` envelope to unwrap — and every
failure arrives as a thrown `WallowError` on `error`.

**Generated keys are FLAT, not hierarchical.** A key is a single-element array holding one
object — `[{ _id, baseUrl, tags, ...args }]` — so there is no prefix that sweeps a subtree,
and a key is not knowable without the client, because it embeds that client's `baseUrl`.
Never write a `queryKey` literal; always call the factory. Two curated predicates bridge the
gap for invalidation:

- `queriesForOperation(key)` — matches every cached query for the operation that key belongs
  to, whatever arguments it was called with.
- `queriesWithTag(tag)` — matches every query carrying an OpenAPI tag, for the broader sweep
  after a write that touches a whole domain.

Invalidation lives at the CALL SITE, in the component's own `onSuccess`, rather than baked
into a shared factory — which is what lets one screen sweep narrowly by operation and another
broadly by tag off the same mutation. See
[Frontend State: TanStack Query vs. Zustand](../development/frontend-state.md) for the full
rules (including the Zustand boundary) and a worked example of adding a new query.

The hand-written layer this replaced — the `queryKeys` registry, the per-domain
`userQueries` / `authQueries` / `mfaQueries` / `organizationsQueries` / `appsQueries` /
`inquiriesQueries` / `settingsQueries` namespaces, and
`registerQueryBootstrap` / `ensureQueryBootstrapped` — is deleted rather than deprecated:
every one of them closed over the module-global client that no longer exists.

#### Escape hatch: calling a generated operation directly

Outside a component render — a one-off script, a non-React host, or code that genuinely has no
use for caching — call the generated typed operation directly instead of going through the query
layer, passing the same instance:

```ts
import { createWallowSdk, inquiriesGetSubmitted } from "@bc-solutions-coder/sdk";

const sdk = createWallowSdk({ baseUrl: "/api" });

const data = await inquiriesGetSubmitted({ client: sdk.client });
// data is fully typed from the OpenAPI schema; the request went
// browser -> /api proxy -> Wallow API with a server-attached Bearer token.
```

Prefer the query layer for anything rendered in a component — a bare call like this has no cache
entry, so two screens calling it independently can disagree about the same data.

---

## Per-request instances for server-rendered loaders

A same-origin BFF app that server-renders authenticated routes (a TanStack
Start `loader`, for example) needs two things a browser tab gets for free: an
ABSOLUTE origin — Node's `fetch` cannot resolve a relative `/api` URL — and the
incoming request's session cookie — Node has no cookie jar, so
`credentials: "include"` alone sends an anonymous request. Both are
request-scoped, and both are constructor arguments:

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

Lift that instance into the router context and every loader and component reads it back out
(`useRouteContext({ from: "__root__" }).sdk`) instead of importing a client. In the browser the
same router builds one with the relative `baseUrl` `/api` and no cookie header, so the two
halves of a hydrating render agree.

- `cookieHeader` — the inbound session cookie, forwarded on every outgoing request from this
  instance. Captured per instance, never read from module scope.
- `internalOrigin` — the origin the host can reach ITSELF on, when it differs from the
  browser-facing one. A container published as `127.0.0.1:5053:3000` cannot self-fetch the
  browser's origin, so without this every SSR'd page falls back to an error boundary. It
  rewrites the outgoing request's origin inside the instance's `fetch` ONLY, leaving the
  configured `baseUrl` — and therefore the request identity an SSR-primed cache shares with
  the browser — untouched.

Nothing here reads module scope, so there is no `AsyncLocalStorage` to own and no resolver to
register. The old request-context seam — `configureSsrClient`, `getSsrRequestContext`,
`setSsrRequestContextResolver`, `wireSsrCookieInterceptor` — existed only to feed per-request
values to a module-global client, and is deleted along with it.
`apps/wallow-web/src/start.ts` is the reference host: it mints the instance in a global request
middleware and `getRouter()` lifts it into the router context.

---

## Local development: the seeded `bcordes-bff` client

The repository's `seed.json` ships a ready-to-use confidential client for local
BFF development so you do not have to register one by hand. After running the
[seeder](../getting-started/developer-guide.md), the following client exists in
the `Wallow` tenant:

| Setting                  | Value                                                                                                             |
| ------------------------ | ----------------------------------------------------------------------------------------------------------------- |
| `clientId`               | `bcordes-bff`                                                                                                     |
| `clientSecret`           | `bcordes-bff-secret`                                                                                              |
| Redirect URI             | `http://localhost:3000/bff/callback`                                                                              |
| Post-logout redirect URI | `http://localhost:3000/`                                                                                          |
| Scopes                   | `openid email profile roles offline_access inquiries.read inquiries.write notifications.read notifications.write` |

Point your BFF at it with a local `.env` (adjust the API/issuer origins to your
running stack):

```ini
OIDC_ISSUER=http://localhost:5001
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
  and every instance `createWallowSdk()` builds sends it with
  `credentials: "include"`.
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

| Symptom                                                 | Likely cause                                           | Fix                                                                                                                                     |
| ------------------------------------------------------- | ------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------- |
| `Missing required environment variable: ...` on startup | A required env var is unset or empty                   | Set every required key in the [environment variables](#environment-variables) table                                                     |
| `getUser()` always returns `null`                       | Session cookie not being sent                          | Serve the app and BFF on the same origin; on the server, confirm the request's `cookieHeader` reaches `createWallowSdk()`               |
| `invalid_client` on callback                            | `OIDC_CLIENT_ID`/`OIDC_CLIENT_SECRET` mismatch         | Confirm they match the registered (or seeded) confidential client                                                                       |
| `redirect_uri` mismatch                                 | `OIDC_REDIRECT_URI` does not match the registered URI  | Register `http://localhost:3000/bff/callback` (or your value) and keep them identical                                                   |
| `401` from `/api/**` after login                        | Session missing or refresh token unavailable           | Ensure `offline_access` is in the requested scopes so a refresh token is issued                                                         |
| `403` with code `CSRF_INVALID` on POST/PUT/PATCH/DELETE | The `x-csrf-token` header is missing or stale          | Echo the `wallow_bff-csrf` cookie (or `/bff/user`'s `csrfToken`) in the `x-csrf-token` header — see [CSRF protection](#csrf-protection) |
| Session cookie not set over plain HTTP locally          | Cookies carry `Secure` by default                      | Set `COOKIE_SECURE=false` in local development only                                                                                     |
| `npm install` `401 Unauthorized`                        | GitHub Packages token missing or lacks `read:packages` | Set `GITHUB_TOKEN` and the `@bc-solutions-coder:registry` line in `.npmrc`                                                              |

---

## See also

- [Integration Cookbook](integration-cookbook.md) — the start-to-finish "new fork,
  new app" recipe: install, mount the splat routes, mint the per-request instance,
  and ship a first feature.
- [BFF Pattern Integration Guide](bff-pattern.md) — the underlying protocol the
  SDK implements.
- [External Auth Setup](external-auth.md) — configuring Wallow as an identity
  provider.
