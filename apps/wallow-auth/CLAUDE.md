# apps/wallow-auth — Agent Guide

Everything in `apps/CLAUDE.md` applies here — zones and `wallow/zone-dag`, the alias map, the
`*.server.*` naming, the `api.ts` data seam, the component-catalog rules, logging, theme, and
the `*.ssr.test.tsx` convention. Read that first. This file is only what is true of **this** app.

## It is a reverse proxy, not a BFF

The most load-bearing fact, and the one wallow-web's shape will mislead you about: this app holds
**no session, no cookie jar and no OIDC client**. It has no `/bff` prefix and nothing to seal.
`/v1/**`, `/connect/**` and `/.well-known/**` are forwarded verbatim to `WALLOW_API_INTERNAL_URL`
by one bridge, `src/shared/lib/api-passthrough.server.ts`, over the SDK's `createApiPassthrough()`
preset; every upstream `Set-Cookie` comes back untouched, which is what lands the login cookies
on this origin rather than the API's.

- **Do not add CSRF, session or token-refresh logic here.** That contract is wallow-web's BFF and
  the API's.
- **`/.well-known/**` is not optional.** The discovery document advertises `jwks_uri` on _this_
  origin; dropping the prefix 404s discovery and breaks login with no useful error. The directory
  is spelled `src/app/routes/[.]well-known/` — `[.]` is the route-codegen escape for a leading
  dot.
- **The three proxy routes use one `ANY` handler each, never a method map.** The upstream owns
  which verbs a path answers; filtering here turns an upstream `405` into a local `404`.
- **`GET /health` returning `ready` is a container contract** — both compose stacks probe it.

## srvx requests cannot be copied — mutate in place

Start's server routes hand the handler an srvx request: a bespoke class that only claims to be a
`Request` via `Symbol.hasInstance`. `new Request(request, { headers })` passes undici's instance
check and then throws `Cannot read private member #state` at runtime. So `handleApiPassthrough`
sets the client-IP header on the inbound request, and rebases the URL by
`Object.defineProperty(request, "url", …)` shadowing the prototype getter. Both are safe — but a
"cleaner" clone is a production 500 no component spec would catch.
`src/shared/lib/api-passthrough.server.test.ts` pins the identity, not just the headers.

## `AUTH_BASE_PATH`: one value, three shapes, plus a strip

The only app with a base path, and the knob is **build-time** — Vite bakes the prefix into every
asset URL, so the Dockerfile promotes an `ARG` to `ENV` before `pnpm build`; a container started
with a different value still serves the prefix it was built with.

`vite.config.ts` spells it three ways and all three are required: Vite's `base` (trailing slash),
the Start plugin's `router.basepath` (no trailing slash), and nitro's `baseURL`. Miss nitro's and
the page renders but never hydrates. The fourth place is easy to forget: **the passthrough strips
it** (`stripBasePath(url.pathname, basePath)`) — Start hands the handler the _original_ request
and the API knows nothing about the prefix.

String arithmetic lives in `@bc-solutions-coder/env/base-path`. This app owns three things in
`src/shared/lib/base-path.ts`: `AUTH_BASE_PATH_ENV_KEY` (imported back by `vite.config.ts`, the
one config-time reach into `src/`), `BASE_PATH` — read from `import.meta.env.BASE_URL`, **not**
`process.env`, so client and server agree — and `toAppHref`. Use `toAppHref` for every
root-relative internal `href`: a literal `href="/login"` under a based build points at the site
root, which behind a path-based ingress is a _different_ app.

## Scripts pass `--configLoader runner`

`dev`, `build`, `test` and `test:watch` all do. `vite.config.ts` imports
`@bc-solutions-coder/env/base-path` as plain Node ESM before any bundle exists; the default
config loader cannot serve that. Copying a script from another app without the flag is how the
config stops loading.

## The vitest browser project needs three deps named explicitly

`vitest.config.ts` passes `extraBrowserOptimizeDeps: ["@bc-solutions-coder/query",
"@bc-solutions-coder/auth", "zod"]`. TanStack Query is named under the **facade**, never the
react-query specifier it re-exports: this app does not declare react-query, so under pnpm's
strict `node_modules` that specifier cannot resolve — and an unresolvable pre-bundle entry is
only a **warning**, after which Vite pre-bundles nothing and the browser project reloads
mid-run. `zod` arrives through `@bc-solutions-coder/forms`; it is the schema module the scanner
misses. Adding a workspace package a browser spec touches usually means adding it here.

## Feature shape

One vertical per screen under `src/features/` (login, register, mfa-challenge, mfa-enroll,
consent, …). Beyond the `index.ts` barrel / `api.ts` seam / `components/` template, several carry
a **`*-result.ts`** module (`auth-result.ts`, `challenge-result.ts`, …): the pure-function
narrowing that turns an API response into a screen outcome (next step, redirect, error code),
with no React in it, so both the component and a node spec can use it. Put that logic there, not
inside a component.

`returnUrl` handling is shared (`src/shared/hooks/use-return-url-guard.ts`,
`src/shared/lib/return-url.ts`) and **refuses** an unsafe value rather than sanitizing it —
routing to `ERROR_HREF` (`/error?reason=invalid_redirect_uri`) instead of silently falling back
to `/`, which would swallow the open-redirect attempt.

`qrcode.react` is the app's one non-workspace UI dependency (`MfaEnrollForm` only).

## Runtime environment is small, and has no OIDC in it

The compose contract is three variables: `PORT`, `HOST`, `WALLOW_API_INTERNAL_URL`. Optional:
`WALLOW_TRUSTED_PROXIES` (gates whether inbound `X-Forwarded-For`/`X-Forwarded-Proto` are
believed — unset, nothing is trusted), `WALLOW_WEB_INTERNAL_URL` (this app's _own_
self-reachable origin), `WALLOW_REPOSITORY_URL` / `WALLOW_DOCS_URL`, and
`OTEL_EXPORTER_OTLP_ENDPOINT`. `AUTH_BASE_PATH` is build-time only.

Every `process.env` read is in `src/app/start.ts` except two server-only modules
(`client-address.server.ts`, `log-ingest.server.ts`) and `vite.config.ts`. Start aliases
`start.ts` into the **client** graph too, so a `process.env` read anywhere else either breaks
the client build or leaks a server value into it.

## Two conventions nothing enforces

- **Server-only modules must be named `*.server.*`** — Start's import protection matches the
  _filename_; `node:*` and `@bc-solutions-coder/sdk/server` are not on its specifier list, so a
  plainly-named wrapper builds clean and ships to the browser.
- **The `Dockerfile`'s two COPY lists must match the manifest's `workspace:*` deps.** Adding a
  workspace dependency means adding two lines by hand; forgetting fails minutes into an image
  build with `ERR_MODULE_NOT_FOUND` out of a `.vite-temp` config stub.

## Related documentation

- App overview and commands: [`README.md`](README.md)
- Playwright suite conventions: [`e2e/CLAUDE.md`](e2e/CLAUDE.md)
- Cross-app rules: [`apps/CLAUDE.md`](../CLAUDE.md) · Repo rules: [`/CLAUDE.md`](../../CLAUDE.md)
