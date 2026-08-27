# apps/wallow-auth — Agent Guide

Everything in `apps/CLAUDE.md` applies here — zones and `wallow/zone-dag`, the alias map, the
`*.server.*` naming that makes Start's import protection real, the `api.ts` data seam and its
`no-restricted-imports` ordering, the component-catalog rules, logging, theme, and the
`*.ssr.test.tsx` convention. Read that first. This file is only what is true of **this** app.

## It is a reverse proxy, not a BFF

The single most load-bearing fact, and the one wallow-web's shape will mislead you about: this app
holds **no session, no cookie jar and no OIDC client**. It has no `/bff` prefix and nothing to seal.
`/v1/**`, `/connect/**` and `/.well-known/**` are forwarded verbatim to `WALLOW_API_INTERNAL_URL`
by one bridge, `src/shared/lib/api-passthrough.server.ts`, over the SDK's `createApiPassthrough()`
preset; every upstream `Set-Cookie` comes back untouched, which is what makes the login cookies land
on this origin rather than the API's.

Consequences worth knowing before you add anything:

- **Do not add CSRF, session or token-refresh logic here.** That contract is wallow-web's BFF and
  the API's. There is no session for a token to hang off.
- **`/.well-known/**` is not optional.** The discovery document advertises `jwks_uri` on _this_
  origin, so dropping the prefix 404s discovery and breaks login with no useful error.
  The directory is spelled `src/app/routes/[.]well-known/` — `[.]` is the route-codegen escape for
  a leading dot; a literal `.well-known/` reads as a path separator.
- **The three proxy routes use one `ANY` handler each, never a method map.** The upstream owns
  which verbs a path answers; filtering here turns an upstream `405` into a local `404`.
- **`GET /health` returning `ready` is a container contract**, not an internal detail — both compose
  stacks probe it with `node -e "fetch(...)"`.

## srvx requests cannot be copied — mutate in place

Start's server routes hand the handler an srvx request: a bespoke class that only claims to be a
`Request` via `Symbol.hasInstance`. `new Request(request, { headers })` passes undici's instance
check and then throws `Cannot read private member #state` at runtime. So
`handleApiPassthrough` sets the client-IP header on the inbound request, and rebases the URL by
`Object.defineProperty(request, "url", …)` shadowing the prototype getter. Both are safe — the
passthrough copies headers before touching them and the request is dead once the handler returns —
but a "cleaner" clone is a production 500 that no component spec would catch.
`src/shared/lib/api-passthrough.server.test.ts` pins the identity, not just the headers.

## `AUTH_BASE_PATH`: one value, three shapes, plus a strip

This is the only app with a base path, and the knob is **build-time** — Vite bakes the prefix into
every asset URL, so the Dockerfile promotes an `ARG` to `ENV` before `pnpm build` and a container
started with a different value still serves the prefix it was built with.

`vite.config.ts` spells it three ways and all three are required: Vite's `base` (trailing slash),
the Start plugin's `router.basepath` (no trailing slash), and nitro's `baseURL`. Miss nitro's and
the page renders but never hydrates, because the server keeps serving `.output/public` at the root.

The fourth place is easy to forget: **the passthrough strips it**. Start rebases the pathname it
matches against the route tree but hands the handler the _original_ request, and the API knows
nothing about the prefix — hence `stripBasePath(url.pathname, basePath)` before forwarding.

String arithmetic lives in `@bc-solutions-coder/env/base-path`. Three things are this app's, in
`src/shared/lib/base-path.ts`: `AUTH_BASE_PATH_ENV_KEY` (which `vite.config.ts` imports back out of
`src/`, the one config-time reach into app source), `BASE_PATH` — read from Vite's
`import.meta.env.BASE_URL`, **not** from `process.env`, so client and server agree and the client
bundle needs no `process` — and `toAppHref`. Use `toAppHref` for every root-relative internal
`href`: a literal `href="/login"` under a based build points at the site root, which behind the
path-based ingress this knob exists for is a _different_ app.

## Scripts pass `--configLoader runner`

`dev`, `build`, `test` and `test:watch` all do. `vite.config.ts` imports
`@bc-solutions-coder/env/base-path` as plain Node ESM before any bundle exists; the default config
loader cannot serve that. Copying a script from another app without the flag is how the config
stops loading.

## The vitest browser project needs three deps named explicitly

`vitest.config.ts` passes `extraBrowserOptimizeDeps: ["@bc-solutions-coder/query",
"@bc-solutions-coder/auth", "zod"]`. TanStack Query is named under the **facade**, never under the
react-query specifier the facade re-exports: this app does not declare react-query, so under pnpm's
strict `node_modules` that specifier cannot resolve at all — and an unresolvable pre-bundle entry is
only a **warning**, after which Vite pre-bundles nothing and the browser project reloads mid-run.
`@bc-solutions-coder/auth` rides the same facade and is a linked workspace package; `zod` arrives
through `@bc-solutions-coder/forms` and it is the schema module, not the form package, that the
scanner misses. Adding a workspace package a browser spec touches usually means adding it here.

## Feature shape

Sixteen verticals under `src/features/`, one per screen: `accept-terms`, `access-request`,
`consent`, `error`, `forgot-password`, `invitation`, `login`, `logout`, `mfa-challenge`,
`mfa-enroll`, `not-found`, `privacy`, `register`, `reset-password`, `terms`, `verify-email`.

Beyond the `index.ts` barrel / `api.ts` seam / `components/` template, several carry a
**`*-result.ts`** module — `auth-result.ts`, `otp-result.ts`, `magic-link-result.ts`,
`challenge-result.ts`, `enroll-result.ts`, `register-result.ts`, `invitation-result.ts`. That is
where the narrowing lives that turns an API response into a screen outcome (next step, redirect,
error code), as a pure function with no React in it, so both the component and a node spec can use
it. Put that logic there rather than inside a component.

`returnUrl` handling is shared, in `src/shared/hooks/use-return-url-guard.ts` and
`src/shared/lib/return-url.ts`, and it **refuses** an unsafe value rather than sanitizing it —
routing to `ERROR_HREF` (`/error?reason=invalid_redirect_uri`) instead of silently falling back to
`/`, which would swallow the open-redirect attempt.

`qrcode.react` is the app's one non-workspace UI dependency, used by `MfaEnrollForm` alone.

## Runtime environment is small, and has no OIDC in it

The compose contract is three variables: `PORT`, `HOST` and `WALLOW_API_INTERNAL_URL`. Optional on
top of those: `WALLOW_TRUSTED_PROXIES` (which gates whether an inbound `X-Forwarded-For` is
believed — unset, nothing is trusted), `WALLOW_WEB_INTERNAL_URL` (this app's _own_ self-reachable
origin, a different variable from `WALLOW_API_INTERNAL_URL`), `WALLOW_REPOSITORY_URL` /
`WALLOW_DOCS_URL`, and `OTEL_EXPORTER_OTLP_ENDPOINT`. `AUTH_BASE_PATH` is build-time only.

Every `process.env` read is in `src/app/start.ts` except two server-only modules
(`client-address.server.ts`, `log-ingest.server.ts`) and `vite.config.ts`. Start aliases `start.ts`
into the **client** graph too, so a `process.env` read anywhere else either breaks the client build
or leaks a server value into it.

## Two conventions nothing enforces any more

`Wallow-xg9t.1` deleted this app's source-reading guard specs. Both constraints are still real:

- **Server-only modules must be named `*.server.*`** — `api-passthrough.server.ts`,
  `client-address.server.ts`, `log-ingest.server.ts`. Start's import protection matches the
  _filename_; `node:*` and `@bc-solutions-coder/sdk/server` are not on its specifier list, so a
  plainly-named wrapper builds clean and ships to the browser (`Wallow-v940`).
- **The `Dockerfile`'s two COPY lists must match the manifest's `workspace:*` deps.** Adding a
  workspace dependency means adding two lines by hand; forgetting fails minutes into an image build
  with `ERR_MODULE_NOT_FOUND` out of a `.vite-temp` config stub.

## Related documentation

- App overview and commands: [`README.md`](README.md)
- Playwright suite conventions: [`e2e/CLAUDE.md`](e2e/CLAUDE.md)
- Cross-app rules: [`apps/CLAUDE.md`](../CLAUDE.md) · Repo rules: [`/CLAUDE.md`](../../CLAUDE.md)
