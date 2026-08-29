# apps/wallow-auth — Agent Guide

Everything in `apps/CLAUDE.md` applies; this file is only what is true of **this** app.

## It is a reverse proxy, not a BFF

This app holds **no session, no cookie jar and no OIDC client** — no `/bff` prefix, nothing to
seal; wallow-web's shape will mislead you here. `/v1/**`, `/connect/**` and `/.well-known/**`
are forwarded verbatim to `WALLOW_API_INTERNAL_URL` by one bridge,
`src/shared/lib/api-passthrough.server.ts`, over the SDK's `createApiPassthrough()` preset;
every upstream `Set-Cookie` comes back untouched — that is what lands the login cookies on this
origin rather than the API's.

- **Do not add CSRF, session or token-refresh logic here** — that contract is wallow-web's BFF
  and the API's.
- **`/.well-known/**` is not optional.** The discovery document advertises `jwks_uri` on _this_
  origin; dropping the prefix 404s discovery and breaks login with no useful error. Directory:
  `src/app/routes/[.]well-known/` (`[.]` = route-codegen escape for a leading dot).
- **One `ANY` handler per proxy route, never a method map** — the upstream owns which verbs a
  path answers; filtering here turns an upstream `405` into a local `404`.
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

The only app with a base path, and the knob is **build-time** — Vite bakes the prefix into
asset URLs; the Dockerfile promotes an `ARG` to `ENV` before `pnpm build`. `vite.config.ts`
spells it three ways, all required — Vite's `base`, the Start plugin's `router.basepath`,
nitro's `baseURL` (miss it and the page renders but never hydrates); see its comments. Fourth
place: **the passthrough strips it** (`stripBasePath`) — the API knows nothing about the
prefix. `src/shared/lib/base-path.ts` owns `AUTH_BASE_PATH_ENV_KEY` (imported back by
`vite.config.ts`), `BASE_PATH` — read from `import.meta.env.BASE_URL`, **not** `process.env`,
so client and server agree — and `toAppHref`, required for every root-relative internal
`href`: a literal `href="/login"` under a based build points at the site root, behind a
path-based ingress a _different_ app.

## The vitest browser project names deps explicitly

`vitest.config.ts` passes `extraBrowserOptimizeDeps` naming the `@bc-solutions-coder/query`
**facade** — never the react-query specifier it re-exports, which this app cannot resolve; an
unresolvable pre-bundle entry is only a **warning**, after which Vite pre-bundles nothing and
the browser project reloads mid-run — plus `zod`. A workspace package a browser spec touches
usually goes here too; full rationale in the config's comment.

## Feature shape

Beyond the `index.ts` barrel / `api.ts` seam / `components/` template, several features carry a
**`*-result.ts`** module (`auth-result.ts`, `challenge-result.ts`, …): pure-function narrowing
from API response to screen outcome (next step, redirect, error code), no React, usable by both
the component and a node spec. Put that logic there, not inside a component.

`returnUrl` handling is shared: `src/shared/lib/return-url.ts` owns the ONE decision function,
`decideReturnUrl(returnUrl, mode)` — modes `refuse-empty` (mount guards), `empty-ok` (the
oracle's `IsNullOrEmpty` parity), `server-allowlist` (MfaChallenge's ask-the-server arm) — plus
`ERROR_HREF` and the `isRedirectUriAllowed` narrowing. Screens **refuse** an unsafe value
rather than sanitizing it — `ERROR_HREF` (`/error?reason=invalid_redirect_uri`), never a silent
fallback to `/`, which would swallow the open-redirect attempt. Do not re-inline a check in a
screen; pick a mode (the adjudicated exceptions are documented in the module's header).

## Runtime environment is small, and has no OIDC in it

The compose contract is three variables: `PORT`, `HOST`, `WALLOW_API_INTERNAL_URL`. Optional:
`WALLOW_TRUSTED_PROXIES` (gates whether inbound `X-Forwarded-*` headers are believed — unset,
nothing is trusted), `WALLOW_WEB_INTERNAL_URL` (this app's _own_ self-reachable origin),
`WALLOW_REPOSITORY_URL` / `WALLOW_DOCS_URL`, `OTEL_EXPORTER_OTLP_ENDPOINT`. `AUTH_BASE_PATH`
is build-time only.

Every `process.env` read is in `src/app/start.ts` except two server-only modules
(`client-address.server.ts`, `log-ingest.server.ts`) and `vite.config.ts`. Start aliases
`start.ts` into the **client** graph too, so a `process.env` read anywhere else either breaks
the client build or leaks a server value into it.

**The `Dockerfile`'s two COPY lists must match the manifest's `workspace:*` deps** — a
forgotten line fails minutes into an image build with `ERR_MODULE_NOT_FOUND` out of a
`.vite-temp` config stub.
