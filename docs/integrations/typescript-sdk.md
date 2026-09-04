# TypeScript SDK Integration Guide

This guide explains how to consume Wallow from a TypeScript frontend using the
`@bc-solutions-coder/sdk` package. The SDK ships a **browser
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
| `@bc-solutions-coder/sdk`                    | Browser (also safe to import from a Node SSR entry) | `createWallowSdk()` — the [per-request client factory](#browser-api) targeting the same-origin `/api` proxy — plus `logout()`, `loginRedirect()`, `getCurrentUser()`, the generated typed operations, the [CSRF module](#csrf-protection), and the [SSR wiring](#per-request-instances-for-server-rendered-loaders) |
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
    BFF->>Browser: Set-Cookie: __Host-wallow_bff=<sealed>; 302 -> /
    Browser->>BFF: GET /api/v1/... (Cookie: __Host-wallow_bff)
    BFF->>BFF: Silent refresh if near expiry
    BFF->>API: GET /v1/... (Authorization: Bearer <token>)
    API->>BFF: 200 OK
    BFF->>Browser: 200 OK
```

---

## Quickstart: from registration to first sign-in

The whole path is: register the client, install the SDK, paste the one-time reveal,
mount two routes. Each step links to the deeper section it summarises, and
**`apps/minimal-app` in the repository is the runnable form of exactly this
walk-through** — a TanStack Start app on its own origin consuming Wallow through the
published SDK alone.

### 1. Register your application

In wallow-web, under your organization's clients, register an **application**. The
[BFF pattern guide](bff-pattern.md#1-register-an-oauth-application-in-wallow)
documents the form field by field; what matters here is which URLs to register, all on your app's own
origin:

| URI                      | Value                                              | Why                                                                                                                                                            |
| ------------------------ | -------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Redirect URI             | `https://app.example.com/bff/callback`             | Where the authorization code lands; must match `OIDC_REDIRECT_URI` exactly                                                                                     |
| Post-logout redirect URI | `https://app.example.com/`                         | Where the browser lands after signing out                                                                                                                       |
| Front-channel logout URI | `https://app.example.com/bff/frontchannel-logout`  | Browser-delivered sign-out when the user's Wallow session ends in another app's tab                                                                             |
| Back-channel logout URI  | `https://app.example.com/bff/backchannel-logout`   | Server-to-server sign-out — the delivery that works with no browser open. Must be reachable **from the identity server** ([details](#receiving-back-channel-logout)) |

Keep `offline_access` among the requested scopes — without it no refresh token is
issued and the session dies with its first access token.

Registration ends in a **one-time reveal**: the client id, the client secret (shown
once, never retrievable again), and a ready-to-paste env block. Copy the block before
leaving the page.

### 2. Install the SDK

Two lines of setup, then a normal install — the committed scope mapping plus a
user-level `read:packages` token (a classic PAT for humans; CI and Docker are covered
under [Installation](#installation)):

```bash
echo "@bc-solutions-coder:registry=https://npm.pkg.github.com" >> .npmrc
npm config set "//npm.pkg.github.com/:_authToken" "$GITHUB_TOKEN"
npm install @bc-solutions-coder/sdk redis
```

`redis` is the SDK's optional peer for [server-side sessions](#session-stores) —
optional locally, required in production (step 5).

### 3. Paste the reveal

The reveal's env block is exactly the required set `loadBffConfigFromEnv()` reads —
paste it into your server's environment (a local `.env`, your deploy platform's
secret store) verbatim:

```ini
OIDC_ISSUER=https://your-wallow.example.com
OIDC_CLIENT_ID=app-your-org-your-app
OIDC_CLIENT_SECRET=<shown once>
OIDC_REDIRECT_URI=https://app.example.com/bff/callback
OIDC_POST_LOGOUT_REDIRECT_URI=https://app.example.com/
OIDC_SCOPES=openid profile email offline_access users.read
BFF_API_BASE_URL=https://your-wallow.example.com
COOKIE_PASSWORD=<generated for you>
```

Four things to know about the block, with the full reference in
[Environment variables](#environment-variables):

- **`COOKIE_PASSWORD` is generated at reveal time** and is already the required 32+
  characters. To change it later without logging every user out, don't replace it —
  **rotate at deploy time** with the keyed `COOKIE_PASSWORDS` form
  ([rotation guide](bff-pattern.md#rotating-the-cookie-password)).
- **`OIDC_ISSUER` is the browser-facing issuer origin.** If your server reaches the
  identity server under a different hostname than browsers do (split-horizon DNS,
  a container network), add `OIDC_METADATA_URL` for the server side; redirects stay
  pinned to the public issuer.
- **`COOKIE_SECURE=false` is for plain-HTTP local development only** — cookies
  default to `Secure`, which browsers drop over `http://`. Never set it in
  production.
- **`SESSION_TTL_SECONDS`** (default 24h) is the session cookie's lifetime. Keep it
  at or below the refresh-token lifetime your deployment issues — a session cannot
  refresh past the grant behind it.

### 4. Mount two routes

The entire integration surface is two splat server routes over the
[`createWallowBffServer()` preset](#server-setup-mounting-the-bff): `/bff/*` (the
OIDC tunnel — login, callback, user, logout, both logout receivers) and `/api/*`
(the proxy that attaches the session's bearer token server-side). Browser code then
talks to its own origin through [`createWallowSdk()`](#browser-api); state-changing
calls are [CSRF-gated](#csrf-protection) with a double-submit token the SDK wires
for you.

### 5. Production: give sessions a server to live in

Set `REDIS_URL` and sessions move into Valkey/Redis. This is **mandatory in
production**, not tuning: replicas must share sessions, and
[back-channel logout](#receiving-back-channel-logout) and cookie-password rotation
can only revoke sessions the server holds — sealed-cookie sessions are a
single-process development convenience nothing can revoke. Run the store with
authentication and TLS (`rediss://user:password@host`).

### 6. Anonymous server-to-server calls (optional)

A contact form, a webhook, a nightly job — anything that must reach the platform
with no user signed in — uses a **service account: a separate registration with its
own one-time reveal**. Register one under the same organization (kind "service
account"); its reveal is the `OIDC_SERVICE_*` trio that
[`createServiceClient()`](#service-accounts-createserviceclient) reads. Scope it
narrowly — a service account's scopes are its blast radius.

---

## Installation

`@bc-solutions-coder/sdk` is published to **GitHub Packages** under the repository owner's
scope. Because it is not on the public npm registry, configure npm to resolve
the `@bc-solutions-coder` scope from GitHub Packages and authenticate with a token that has
the `read:packages` permission.

Put the scope mapping in a `.npmrc` at your project root, where it can be
committed:

```ini
@bc-solutions-coder:registry=https://npm.pkg.github.com
```

Keep the credential out of that file and in your **user-level** config instead:

```bash
npm config set "//npm.pkg.github.com/:_authToken" "$GITHUB_TOKEN"   # or: pnpm config set …
```

> **Why not a token line in the project `.npmrc`?** A committed `.npmrc` could
> be edited to redirect the registry, which would hand your token to whoever
> controls it — so pnpm refuses to expand `${GITHUB_TOKEN}` from a project file
> and warns instead. The `pnpm config set` above writes to `~/.npmrc`, which
> both npm and pnpm honour. In CI, use `actions/setup-node` with
> `registry-url: https://npm.pkg.github.com` and a `NODE_AUTH_TOKEN` env var; it
> writes a user-level `.npmrc` for you.

> **Scope note:** GitHub Packages resolves scoped packages against the
> publishing organization, so the token — a personal access token or CI token —
> needs `read:packages` on that organization.

In a **Docker build**, the token crosses into the build the same way: as a
**build secret**, never a build `ARG` or an `ENV` — both bake the token into the
image history, where anyone who can pull the image can read it. Mount the secret
for the install step only:

```dockerfile
RUN --mount=type=secret,id=npm_token \
    npm config set "//npm.pkg.github.com/:_authToken" "$(cat /run/secrets/npm_token)" \
    && npm install \
    && npm config delete "//npm.pkg.github.com/:_authToken"
```

```bash
docker build --secret id=npm_token,env=GITHUB_TOKEN .
```

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
to remember to regenerate them. `packages/api-errors/src/generated/**` (the
`ErrorCode` catalogue) is generated from the same snapshot, and `pnpm check:generated`
inside `pnpm check` fails when either generated directory no longer matches it.

Both halves read the contract the same way — from the document
`Wallow.Api` emits at build time, via the shared
`.github/actions/openapi-document` composite action, so they can never disagree
about what changed:

- **On a pull request**, `openapi-drift.yml` fails if the committed snapshot no
  longer matches the contract, and prints the commands to refresh it.
- **On `main`**, `openapi-autoregen.yml` regenerates the snapshot, the typed
  client and the `api-errors` catalogue and opens a pull request titled
  `feat(sdk): regenerate OpenAPI snapshot and generated output`. Merging that PR
  feeds release-please, which bumps `@bc-solutions-coder/sdk` (and
  `@bc-solutions-coder/api-errors` when its catalogue moved) and lets you cut an
  `sdk-v*` tag as below.

The automated PR is byte-identical to what a manual refresh against a running API
produces, so accepting it never fights with a developer regenerating locally. Check
its commit type before merging: it is `feat(sdk):` by default, and a contract change
that removed or renamed an operation should be squashed as `feat(sdk)!:` instead so
the SDK takes a major bump.

### No runtime payload validation — a deliberate decision

The SDK does **no runtime validation of API payloads**, in the browser or in the
BFF. The generated types plus the drift check above are the contract. This was
evaluated and rejected, not overlooked:

- **In the browser it is not worth the bytes.** Generating zod validators into
  the client (hey-api's zod plugin) was measured at **+24.4 kB gzip** for a
  five-operation import — ~19.2 kB of that being the zod runtime floor — to
  re-validate data that already crossed Wallow's own same-origin BFF proxy.
- **If validation is ever wanted, it belongs on the Node side of the BFF proxy**
  (`@bc-solutions-coder/sdk/server`), where the proxy talks to the real API:
  zero bundle cost for end users, and a validation failure can become a proper
  502 / `WallowError` at the actual trust boundary instead of a thrown
  `ZodError` on an HTTP 200.

Anyone revisiting this must first close two **known fidelity gaps** between the
snapshot and what the API actually serializes. The drift check compares the
committed snapshot against the *spec* the API emits — never the spec against
actually-serialized payloads — so these do not trip CI, but a strict validator
trips on both:

1. **`format: date-time` values may carry a non-UTC offset.** The API serializes
   `DateTimeOffset` values as-is, and zod 4's `z.iso.datetime()` rejects
   non-UTC offsets by default (proven against
   `AccountLoginResponse.mfaGraceDeadline`; a validator must opt into offsets,
   e.g. `dates.offset`).
2. **Required-and-nullable properties can be omitted on the wire.** The
   generator marks non-optional constructor parameters `required` even when
   nullable, but the serializer can omit a null member entirely, so a
   presence-checking validator throws (proven against
   `MfaStatusResponse.method`, which is still `required` *and* nullable in the
   current snapshot).

---

## Publishing the SDK

The SDK is versioned and released **independently of the platform**. It does not
piggyback on the release-please `vX.Y.Z` releases (the platform version, e.g.
`v3.2.1`) — pushing a platform tag or cutting a platform release does **not**
publish the SDK.

Independently is not manually, though. release-please owns the SDK's version number too, as its
own manifest component: merging the SDK's Release PR is what bumps `packages/sdk/package.json` and
creates the `sdk-vX.Y.Z` tag that then triggers the publish below. See
[a published package's two release stages](../operations/versioning.md#a-published-package-releases-in-two-stages).

Publish a new SDK version in one of two ways:

- **Push an `sdk-v<version>` tag** — the `package-publish` workflow reads the
  package (`sdk`) and the version off the tag and publishes that version:

  ```bash
  git tag sdk-v0.1.0
  git push origin sdk-v0.1.0
  ```

- **Run the `package-publish` workflow manually** from the Actions tab (or via
  `gh workflow run package-publish.yml -f package=sdk -f version=0.1.0`),
  providing the package and the version (no leading `v`).

Either path installs, tests, and builds the package, syncs its `package.json` to the
requested version, and publishes to GitHub Packages. The SDK version is chosen
independently and has no relationship to the platform `vX.Y.Z` release-please
versions. `@bc-solutions-coder/api-errors` publishes the same way under
`api-errors-v<version>` tags.

---

## Server setup: mounting the BFF

`createWallowBffServer()` is the golden path: it loads the config, picks a session
store, builds the OIDC tunnel handlers and the `/api` proxy over that **one shared
store**, and dispatches by path. What comes back is three web-standard entry points —
`handleBff`, `handleApi`, and `handleHealth` — plus the resolved `config` and `store`:

```ts
// src/app/lib/bff.server.ts — build the host once, lazily
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
and every other route with it, and a failed build must not be memoised, so a
transient store outage at boot does not permanently disable the BFF.

Setting `REDIS_URL` is all it takes to move sessions into Valkey: the preset
connects itself, lazily, through the SDK's optional `redis` peer (install it —
`pnpm add redis` — and the first session write opens the connection; a missing
package fails there with an error naming it). A host that wants to own the
connection instead — to route the client's `error` events into its own logger,
or to fail at boot rather than on first use — passes a connected node-redis
client as `redisClient`; the client is assignable as-is. `REDIS_URL` never
degrades to cookie sessions: a deployment that asked for server-side ones gets
them or an error, never a silent stateless fallback.

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
        const { handleBffRequest } = await import("../../lib/bff.server");
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
TanStack Start app that mounts exactly these routes (`src/app/routes/bff/$.ts`,
`src/app/routes/api/$.ts`, `src/app/routes/health.ts`, over `src/app/lib/bff.server.ts`) and
consumes the proxy from its dashboard. The `app/` prefix is that app's host zone; a
flat app mounts the same files directly under `src/` — `apps/minimal-app` is that
flat, external-consumer form (`src/routes/{bff,api}/$.ts` over `src/lib/bff.server.ts`).

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
else `http://localhost:5001`. Pass the host runtime's request straight through to
`handle()` — the passthrough reads the peer address from `request.ip` (srvx exposes it;
a WHATWG `Request` has no socket) and stamps the resolved caller onto the upstream
`X-Forwarded-For`, so the API's per-IP rate limits apply per visitor rather than per
proxy. See [Client addresses behind a proxy](#client-addresses-behind-a-proxy) for
how a fronting proxy's headers become believed. `apps/wallow-auth/` is the reference
consumer (`src/shared/lib/api-passthrough.server.ts` plus three splat routes).

### The `/api` proxy and silent refresh

`createApiProxy(config)` reads the sealed session cookie on each request. Before
forwarding, it checks whether the access token is within a short skew window of
expiry and, if so, **silently refreshes** it using the stored refresh token and
re-seals the cookie — the browser sees only a normal API response. It then
strips the `/api` prefix and forwards the request to `apiBaseUrl` with the
`Authorization: Bearer <access_token>` header attached.

Requests that arrive without a valid session receive a `401`, which the
`getCurrentUser()` helper interprets as "unauthenticated".

A refresh the identity server **refuses** is terminal: the grant behind the
session was revoked — a logout on another application, a deactivated account —
or the refresh token was already spent. The proxy answers it by ending the
session exactly as a logout would: it destroys the store record, clears the
session cookie and its CSRF companion, and returns `401` problem details with
code `SESSION_REFRESH_FAILED`. Leaving the session in place would replay the
same doomed refresh on every request; tearing it down turns the refusal into a
clean re-login at the next navigation.

### Receiving back-channel logout

`createWallowBffServer()` also routes `POST /bff/backchannel-logout` — the endpoint
[OIDC Back-Channel Logout](bff-pattern.md#back-channel-logout-the-server-to-server-notification)
delivers logout tokens to when the user's Wallow session ends in another
application. There is nothing to write: register the URL on the client as its
`backchannelLogoutUri` and the handler does the rest:

```
https://<app-host>/bff/backchannel-logout
```

Two deployment facts make the registration actually work:

- **The URL must be server-reachable from the identity server.** The OP POSTs to
  it directly — no browser is involved — so the ingress must route the path from
  wherever Wallow runs, exactly like a public page of the app. Wallow's delivery
  gate refuses URIs that resolve to private or loopback hosts by default; a
  deployment where the OP legitimately reaches relying parties over a private
  network turns on `Identity:BackchannelLogout:AllowPrivateNetworkHosts` (see the
  [Configuration guide](../getting-started/configuration.md#identity-back-channel-logout)).
- **Server-side revocation needs the Valkey store.** The handler verifies the
  logout token against the issuer's JWKS — signature, `iss`, `aud`, `iat`/`exp`,
  the back-channel `events` claim, no `nonce`, a `sid` or `sub` — and then asks
  the session store to revoke the sessions it names: by `sid` when the token
  carries one, else every session of the `sub`. `ValkeySessionStore` indexes both
  at write time and destroys the records on the spot, then best-effort revokes
  each destroyed session's refresh token upstream (RFC 7009), so the token family
  dies with the session. `CookieSessionStore` exposes neither revocation method —
  the session lives sealed in the browser's cookie, out of the server's reach — so
  under cookie sessions logout tokens are accepted but revoke nothing.
  `createWallowBffServer()` warns at boot (via `console.warn`, or the `onWarning`
  option) when the issuer advertises `backchannel_logout_supported` but the
  selected store cannot revoke.

The endpoint reads no cookie and requires no CSRF token: the caller is the
identity server, not a browser, and the signed logout token is the entire
security of the request. An invalid token answers an undifferentiated
`400 {"error":"invalid_request"}`; a valid one answers `200`, including when the
named session is already gone; every response carries `cache-control: no-store`.

### Client addresses behind a proxy

The API rate-limits per client address and reads that address from the **rightmost**
entry of `X-Forwarded-For` — the one the last trusted hop appended. Both server presets
(`createWallowBffServer().handleApi` and `createApiPassthrough().handle`) append the
resolved caller to the outgoing `X-Forwarded-For` on every proxied request, so an
external relying party needs no code of its own to forward visitor addresses. What
"the caller" resolves to depends on whether the SDK believes the headers it received:

- **Nothing trusted (the default).** The caller is the socket peer, `request.ip`. An
  inbound `X-Forwarded-For` is forwarded untouched but never believed, so a visitor
  cannot spoof their address by sending the header themselves. Behind a reverse proxy
  this makes every visitor look like the proxy.
- **Trusted proxies configured.** When the peer is inside the trust list, the SDK walks
  the inbound `X-Forwarded-For` chain from the right, skipping trusted hops, and takes the
  first untrusted entry as the caller — the visitor the proxy saw. A request from an
  untrusted peer that carries a forged chain still resolves to the peer.

The trust list comes from the `trustedProxies` option on either preset, else the
`WALLOW_TRUSTED_PROXIES` environment variable (read from the `env` option, else
`process.env`). An explicit `trustedProxies: ""` trusts nothing even when the variable is
set. The value is a comma-separated list of IPv4/IPv6 addresses or CIDR ranges, with the
keyword `private` standing for every RFC 1918 / link-local / loopback range — the right
answer for an ingress on the same private network:

```ts
// trust the container network the ingress lives on
const server = createWallowBffServer({ config, trustedProxies: "private" });

// or an explicit hop
const passthrough = createApiPassthrough({ trustedProxies: "10.42.0.0/16, 203.0.113.7" });
```

Both presets take a `PeerRequest` — a `Request` with an optional `ip` — and every
server runtime this SDK targets (srvx under TanStack Start, Nitro) supplies one. Hand the
inbound request to the preset unchanged: an srvx request cannot be cloned (its class holds
private state the copy constructor cannot read) and a copy would lose `ip`. A runtime that
exposes no peer address writes no `X-Forwarded-For` entry at all rather than a fake one.

The primitives behind this — `resolveClientAddress`, `parseTrustedProxies`,
`createClientAddressResolver`, `createRequestOriginResolver` — ship on the dependency-free
`@bc-solutions-coder/sdk/server/forwarded` subpath, safe to import from an isomorphic
module, for hosts that need the same trust decision outside a proxied request (a log
ingest route stamping the caller, a server-rendered page resolving its public origin).
The operator-facing side of the same list is in
[Reverse proxy](../operations/reverse-proxy.md).

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
| `COOKIE_NAME`                   | No       | Session cookie name. Defaults to **`__Host-wallow_bff`** — the `__Host-` prefix is applied whenever the cookie is `Secure` and `COOKIE_HOST_PREFIX` is not `false`, which is the default on both counts. Plain `wallow_bff` only when one of those is off                                                                    |
| `COOKIE_HOST_PREFIX`            | No       | Whether the default session-cookie name carries the `__Host-` prefix, which binds the cookie to the exact host that set it. Fails secure: only the literal `false` opts out, and it relaxes the **name** only, never the `Secure` flag. Defaults to `true`                                                                    |
| `OIDC_METADATA_URL`             | No       | Server-side discovery URL, for split-horizon DNS where the issuer is reachable under different hostnames from the browser and the server. The backchannel uses its `token_endpoint`; browser-facing redirects stay pinned to the public `OIDC_ISSUER` origin. Defaults to `${OIDC_ISSUER}/.well-known/openid-configuration` |
| `SESSION_TTL_SECONDS`           | No       | Lifetime of the session cookie, written as its `Max-Age`, so a stale browser cookie cannot outlive the session it references. Must be a positive whole number — a malformed value throws at startup rather than silently falling back. Defaults to `86400` (24 hours)                                                       |
| `COOKIE_SECURE`                 | No       | Whether the session, transaction, and CSRF cookies carry the `Secure` flag. Fails secure: only the literal `false` clears it. Set `COOKIE_SECURE=false` for plain-HTTP local development. Defaults to `true`                                                                                                                |
| `COOKIE_SAMESITE`               | No       | `SameSite` on the session and CSRF cookies: `lax` (default) or `strict`. `strict` is a hardening step for SPAs that bootstrap through same-origin `/bff/user` — the trade is that the first document request after any cross-site navigation (the post-login landing included) arrives without the session cookie. The login transaction cookie is always `Lax`; it must ride the cross-site callback redirect from the IdP. `none` is rejected: the BFF is a same-origin pattern. Any other value throws at startup       |
| `REDIS_URL`                     | No       | Read by `createWallowBffServer()` (not `loadBffConfigFromEnv()`) and by `createServiceClient()`. When set, BFF sessions live in Valkey/Redis and the service-account token cache is shared across processes; the SDK connects through its optional `redis` peer. Unset, sessions seal into the cookie and the token cache is in-memory                                                                                       |
| `WALLOW_TRUSTED_PROXIES`        | No       | Read by `createWallowBffServer()` and `createApiPassthrough()` (not `loadBffConfigFromEnv()`); the `trustedProxies` option overrides it. Comma-separated addresses / CIDRs, or `private`, whose inbound `X-Forwarded-For` is believed when resolving the caller stamped onto proxied API requests (and whose `X-Forwarded-Proto` is believed by the `./server/forwarded` origin resolver). Unset, nothing is trusted and the socket peer is the caller. See [Client addresses behind a proxy](#client-addresses-behind-a-proxy) |

`createServiceClient()` reads its own subset — it shares `OIDC_ISSUER`,
`OIDC_METADATA_URL`, `BFF_API_BASE_URL` and `REDIS_URL` with the BFF and adds
three of its own, so a process that only runs a service account never has to
define `OIDC_CLIENT_ID` or `COOKIE_PASSWORD`. Every missing variable is reported
in ONE error, not one per restart:

| Variable                     | Required | Description                                                                                                                                         |
| ---------------------------- | -------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| `OIDC_SERVICE_CLIENT_ID`     | Yes      | The service account's client identifier — a separate client from the BFF's, registered for the client-credentials grant                            |
| `OIDC_SERVICE_CLIENT_SECRET` | Yes      | Its secret — server-side only                                                                                                                       |
| `OIDC_SERVICE_SCOPES`        | Yes      | Space-separated scopes to request, e.g. `inquiries.write`. Required with no default: a service account's scopes are its blast radius, so name them |

> **Confidential values:** `OIDC_CLIENT_SECRET`, `OIDC_SERVICE_CLIENT_SECRET` and
> `COOKIE_PASSWORD` must never be shipped to the browser or committed to source
> control. They belong in the server process environment (or a secrets manager) only.

---

## Session stores

Where the token set lives is pluggable. `createBffHandlers(config, store)` and
`createApiProxy(config, store)` both accept a `SessionStore` as an optional
second argument — pass the **same instance** to both. Omitting it defaults to a
cookie-only store built from `COOKIE_PASSWORD`, so single-argument callers keep
working.

| Store                | Where the session lives                                                         | Use it when                                                                                                                       |
| -------------------- | ------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| `CookieSessionStore` | Sealed into the session cookie itself                                           | **Development only** — nothing extra to run                                                                                       |
| `ValkeySessionStore` | In a Redis-compatible server; the cookie holds only an opaque sealed session id | Production — small cookies, server-side revocation, and a refresh lock that serializes concurrent token refreshes for one session |

The single-argument default is `CookieSessionStore`, and that default exists so a fork runs with
zero infrastructure. It is a **development default only**: production deployments must construct a
`ValkeySessionStore` explicitly and pass it as the second argument. See
[Choosing a Session Store](bff-pattern.md#choosing-a-session-store) for the full reasoning.

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
(`get`, `set` with optional `ex`/`nx` flags, `del`, plus `sadd`/`srem`/`smembers`
and `expire` behind the [back-channel logout](#receiving-back-channel-logout)
indexes), so the SDK carries no hard Redis dependency — you adapt the client you
already run. Three ways to get one:

- **`REDIS_URL` alone.** `createWallowBffServer()` builds the store over
  `createRedisFromUrl(url)`, which connects on first use through the optional
  `redis` peer. This is the zero-code path.
- **A node-redis client.** `createRedisAdapter(client)` — or the preset's
  `redisClient` option — accepts a `createClient()` result directly; the port is
  wide enough that no hand-written bridge is needed.
- **Any other client.** Implement `RedisLike` yourself. The `nx` flag must reach
  the server as a real conditional set; that is what makes the refresh lock a
  lock. The package README shows a complete `ioredis` adapter:
  [`packages/sdk/README.md`](https://github.com/bc-solutions-coder/wallow/blob/main/packages/sdk/README.md).

---

## CSRF protection

The `/api` proxy **rejects every state-changing request that does not present a
valid CSRF token**, answering `403` with the code `CSRF_INVALID`. This is the
first thing to reach for when a `POST`, `PUT`, `PATCH`, or `DELETE` through the
tunnel comes back as `403`. The gate names those four methods explicitly, so
everything else — `GET`, `HEAD`, `OPTIONS`, and `TRACE` — passes ungated. The
client-side `isSafeMethod` helper is narrower, covering only the three RFC 9110
safe methods; `TRACE` is ungated by the server without being "safe" by that
definition.

The SDK uses a synchronizer token with a double-submit companion cookie:

1. On successful login, `/bff/callback` mints a token, stores it inside the
   sealed session, and writes it to a cookie named `<COOKIE_NAME>-csrf`
   (default `__Host-wallow_bff-csrf`, since the companion inherits whatever the
   session cookie's name resolved to, `__Host-` prefix included). That cookie is
   deliberately **not** `HttpOnly`,
   because browser JavaScript must read it. It carries no credential of its own
   — the session cookie stays `HttpOnly`, and the token is worthless without it.
2. `GET /bff/user` returns the same token as `csrfToken` in its JSON body.
3. The browser echoes it in the `x-csrf-token` header on every state-changing
   request. The proxy compares it against the session-bound token in constant
   time before refreshing anything or forwarding anything upstream.

The SDK's `csrf` module owns this exchange on the client side, so app code
never hand-rolls a request interceptor or reads the companion cookie itself.
`createWallowSdk()` already wires the interceptor onto every instance it builds,
and the interceptor reads the companion cookie at request time — there is no
token to hand over and no state to clear on logout:

```ts
import { createWallowSdk } from "@bc-solutions-coder/sdk";

// CSRF interceptor already wired; it reads the double-submit cookie live.
const sdk = createWallowSdk({ baseUrl: "/api" });
```

- `wireCsrfInterceptor(client)` registers a request interceptor exactly once:
  on every request whose method is not CSRF-exempt it stamps the double-submit
  cookie's value into `x-csrf-token`, and it leaves safe methods (and the
  cookie-less pre-login and server-side states) untouched. It takes any object
  shaped like the generated client (`CsrfInterceptorClient`), so it also wires
  onto a client you built yourself — but you only need to call it directly for
  a client the factory did not build.
- `csrf: false` on `createWallowSdk()` skips the interceptor for a passthrough
  topology (wallow-auth's shape): that app holds no BFF session and mints no
  token, and behind a shared-hostname ingress its jar could hold *another*
  app's companion cookie, which the interceptor would otherwise present as its
  own.
- `readCsrfCookie()` reads the companion cookie (preferring the `__Host-`
  prefixed name), returning `null` outside the browser or when no cookie is
  set. The interceptor and `logout()` both resolve the token through it; it is
  exported for anything else that must echo the same token.
- `isSafeMethod(method)` is the RFC 9110 safe-method check
  (`GET`/`HEAD`/`OPTIONS`) the interceptor uses internally; it is exported for
  hosts that need the same rule outside the interceptor.

`GET /bff/user` still returns the token as `csrfToken` in its body for
non-browser clients; browser code never needs it, because the cookie carries
the same token.

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

A refresh that fails — the proactive one before the forward, or the forced one
after a reactive `401` — destroys the store record, clears the session cookies,
and answers `401` with code `SESSION_REFRESH_FAILED` (see
[the `/api` proxy and silent refresh](#the-api-proxy-and-silent-refresh)).

---

## Browser API

Build one SDK instance per request, then read and change auth state through
the helpers.

```ts
import { createWallowSdk, getCurrentUser, loginRedirect, logout } from "@bc-solutions-coder/sdk";

// Point a fresh typed client at the same-origin BFF proxy.
const sdk = createWallowSdk({ baseUrl: "/api" });

// Render current auth state (null when anonymous).
const user = await getCurrentUser({ client: sdk.client });

// Send an anonymous visitor to sign in. loginRedirect() only BUILDS the target
// — render it as an <a href>, or throw it through the router's redirect().
const { href } = loginRedirect("/dashboard"); // /bff/login?returnTo=%2Fdashboard

// Sign out. This is an async POST, not a navigation: await it (or catch the
// rejection) so a refused logout is not swallowed.
await logout();
```

- `createWallowSdk({ baseUrl })` — builds a request-scoped instance owning its
  own generated client, cookie, and interceptor list. `baseUrl` is REQUIRED and
  has no default: the browser passes the same-origin relative BFF path (`/api`),
  while an SSR render passes an absolute origin Node's `fetch` can parse. Pass
  the full origin (`https://app.example.com/api`) when the app is not served
  from the BFF's origin. Bind a generated operation to the instance with the
  standard `{ client }` call option.
- `getCurrentUser({ client })` — resolves the signed-in user through the `/api`
  proxy, or `null` on a `401`. Any other failure throws the error it arrived
  as, so an outage can never masquerade as a signed-out user. In a TanStack
  app, prefer `@bc-solutions-coder/auth`'s `currentUserQuery`, which caches
  this read behind TanStack Query.
- `loginRedirect(returnTo = "/", hints?)` — returns `{ href, reloadDocument }`
  pointing at `/bff/login` with an encoded `returnTo`. It never touches
  `location`, so it is SSR-safe: render the `href` as a plain document link, or
  throw it from a `beforeLoad` via the router's `redirect()` (`requireAuth()`
  wraps that guard pattern). There is deliberately no imperative `login()` — a
  helper that assigned to `location` turned gated SSR loads into HTTP 500s.
  `hints.organization` (an organization id) is forwarded to the IdP as the
  `organization` authorize parameter: a signed-in user follows that link to
  switch organization context — the re-authorize is silent against the SSO
  cookie and the new session is scoped to that organization. Wallow-web's
  *My organizations* renders one such link per membership; that page is the
  organization picker.
- `logout(options?)` — returns `Promise<void>`. `/bff/logout` is state-changing
  and answers `405 + Allow: POST` to a bare `GET`, so this cannot be a plain
  navigation: it issues `POST /bff/logout` with `credentials: "include"` and an
  `x-csrf-token` header, then navigates the browser to the IdP end-session URL
  the handler answers in its JSON body (`{ logoutUrl }`) — that hop ends the
  SSO session and fans out front-/back-channel logout to the other relying
  parties. The token is resolved most-specific
  first — an explicit `options.csrfToken`, then the token learned from
  `/bff/user`, then the readable double-submit cookie. The promise **rejects**
  when the BFF refuses (`Logout failed: the BFF answered <status>`), leaving the
  browser where it is, so handle it rather than firing and forgetting. The
  browser-context guard throws synchronously.

The user shape is `WallowUser`: always `sub`, optionally `email`/`name`, plus
any additional claims. `GET /bff/user` returns it directly (with the session's
`csrfToken` alongside — see [CSRF protection](#csrf-protection)) for code that
wants the raw endpoint rather than a typed operation.

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

## Service accounts: `createServiceClient()`

Not every caller is a browser. A backend job, a webhook receiver, or an external
relying party's own server calls the Wallow API as **itself**, with no user
session to proxy — the OAuth client-credentials grant. That is what
`createServiceClient()` on the `@bc-solutions-coder/sdk/server/service` subpath
is for:

```ts
// src/lib/service-client.server.ts
import { createServiceClient, type WallowServiceClient } from "@bc-solutions-coder/sdk/server/service";

let service: WallowServiceClient | undefined;

export function getServiceClient(): WallowServiceClient {
  service ??= createServiceClient(); // reads OIDC_SERVICE_* from process.env
  return service;
}

// anywhere server-side — a generated operation is called exactly as with a
// user session's SDK instance: pass the service client's `client`
import { inquiriesCreate } from "@bc-solutions-coder/sdk";
const { data } = await inquiriesCreate({ client: getServiceClient().client, body });
```

What it does for you:

- **One token, fetched once.** The access token is cached under a
  `wallow:service-token:<clientId>:<scopes>` key and renewed 30 seconds before
  it expires. Concurrent first calls collapse into one grant in-process, and a
  `SET NX EX` lock serialises renewals **across** processes when the cache is a
  shared store — a fleet of workers behind one service account performs one token
  request, not one per instance.
- **A shared cache when you have one.** Pass `store` (any `RedisLike`) to share
  the token across processes; with `REDIS_URL` set and no `store`, the client
  connects itself through the optional `redis` peer, exactly as the BFF preset
  does. With neither, the cache is in-memory — correct, just per-process.
- **One replay on `401`.** A revoked or rotated token is evicted, re-fetched, and
  the request replayed **once**, with its original body; a second `401` is
  returned to the caller as the error it is.
- **A readable environment error.** `OIDC_ISSUER`, `OIDC_SERVICE_CLIENT_ID`,
  `OIDC_SERVICE_CLIENT_SECRET`, `OIDC_SERVICE_SCOPES` and `BFF_API_BASE_URL` are
  all required; every missing one is listed in the same error.

Its own subpath, like `./server/passthrough`, so a service-only process never
pulls the BFF handler graph into its bundle. `accessToken()` is exposed for the
rare call that has to go outside the typed client; nothing else about the token
is — it never reaches a browser, and the SDK's browser entry does not know this
subpath exists.

To bypass the environment (tests, a multi-tenant worker) pass `config` directly:

```ts
const service = createServiceClient({
  config: {
    issuer: "https://auth.example.com",
    clientId: "billing-worker",
    clientSecret: process.env.BILLING_WORKER_SECRET!,
    scopes: ["invoices.write"],
    apiBaseUrl: "https://api.example.com",
  },
  store, // optional RedisLike
});
```

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
  browser-facing one. A container published as `127.0.0.1:5053:3000` (the classic default for
  `docker/docker-compose.test.yml`'s `wallow-web` service; `./scripts/e2e.sh` publishes it on a
  per-run port instead, Wallow-joo0) cannot self-fetch the browser's origin, so without this every
  SSR'd page falls back to an error boundary. It
  rewrites the outgoing request's origin inside the instance's `fetch` ONLY, leaving the
  configured `baseUrl` — and therefore the request identity an SSR-primed cache shares with
  the browser — untouched.

Nothing here reads module scope, so there is no `AsyncLocalStorage` to own and no resolver to
register. The old request-context seam — `configureSsrClient`, `getSsrRequestContext`,
`setSsrRequestContextResolver`, `wireSsrCookieInterceptor` — existed only to feed per-request
values to a module-global client, and is deleted along with it.
`apps/wallow-web/src/app/start.ts` is the reference host: it mints the instance in a global request
middleware and `getRouter()` lifts it into the router context.

---

## Local development: the seeded `bff-example-client`

The repository's `seed.json` ships a ready-to-use confidential client for local
BFF development so you do not have to register one by hand. It is the *external
site* reference client — a second application signing users in through Wallow —
so it sits on its own port; `wallow-web-client` is the one `apps/wallow-web` uses
on port 3000. This is the client the containerised E2E stack
(`docker/docker-compose.test.yml`) hands to `apps/minimal-app` as the
`bff-example` service, which the three-origin acceptance suite drives end to end.
After running the
[seeder](../getting-started/developer-guide.md), the following client exists in
the `Wallow` organization:

| Setting                  | Value                                                                                                             |
| ------------------------ | ----------------------------------------------------------------------------------------------------------------- |
| `clientId`               | `bff-example-client`                                                                                                     |
| `clientSecret`           | `bff-example-secret`                                                                                              |
| Redirect URI             | `http://localhost:3003/bff/callback`                                                                              |
| Post-logout redirect URI | `http://localhost:3003/`                                                                                          |
| Scopes                   | `openid email profile roles offline_access inquiries.read inquiries.write notifications.read notifications.write` |

Point your BFF at it with a local `.env` (adjust the API/issuer origins to your
running stack):

```ini
OIDC_ISSUER=http://localhost:5001
OIDC_CLIENT_ID=bff-example-client
OIDC_CLIENT_SECRET=bff-example-secret
OIDC_REDIRECT_URI=http://localhost:3003/bff/callback
OIDC_POST_LOGOUT_REDIRECT_URI=http://localhost:3003/
BFF_API_BASE_URL=http://localhost:5001
COOKIE_PASSWORD=dev-only-change-me-to-a-long-random-string
```

The redirect and post-logout URIs must match the seeded client exactly, so keep
your BFF on port `3003` locally (or update `seed.json` and re-seed) —
`apps/minimal-app` defaults to port `3010`, so run it as `PORT=3003` to pair it
with these values, or register your own client per the
[quickstart](#quickstart-from-registration-to-first-sign-in) as its README does.
This is the manual, by-hand convention; the containerised E2E stack seeds the
same client with a per-run port instead (`./scripts/e2e.sh`, Wallow-joo0).

> **Development secret:** `bff-example-secret` and the sample `COOKIE_PASSWORD`
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
| `getCurrentUser()` always resolves `null`               | Session cookie not being sent                          | Serve the app and BFF on the same origin; on the server, confirm the request's `cookieHeader` reaches `createWallowSdk()`               |
| `invalid_client` on callback                            | `OIDC_CLIENT_ID`/`OIDC_CLIENT_SECRET` mismatch         | Confirm they match the registered (or seeded) confidential client                                                                       |
| `redirect_uri` mismatch                                 | `OIDC_REDIRECT_URI` does not match the registered URI  | Register `http://localhost:3000/bff/callback` (or your value) and keep them identical                                                   |
| `401` from `/api/**` after login                        | Session missing or refresh token unavailable           | Ensure `offline_access` is in the requested scopes so a refresh token is issued                                                         |
| `403` with code `CSRF_INVALID` on POST/PUT/PATCH/DELETE | The `x-csrf-token` header is missing or stale          | Echo the `wallow_bff-csrf` cookie (or `/bff/user`'s `csrfToken`) in the `x-csrf-token` header — see [CSRF protection](#csrf-protection) |
| Session cookie not set over plain HTTP locally          | Cookies carry `Secure` by default                      | Set `COOKIE_SECURE=false` in local development only                                                                                     |
| `npm install` `401 Unauthorized`                        | GitHub Packages token missing or lacks `read:packages` | Add the `@bc-solutions-coder:registry` line to the project `.npmrc` and set the token with `npm config set` (see [Installation](#installation)) |

---

## See also

- [Integration Cookbook](integration-cookbook.md) — the start-to-finish "new fork,
  new app" recipe: install, mount the splat routes, mint the per-request instance,
  and ship a first feature.
- [BFF Pattern Integration Guide](bff-pattern.md) — the underlying protocol the
  SDK implements.
- [External Auth Setup](external-auth.md) — configuring Wallow as an identity
  provider.
