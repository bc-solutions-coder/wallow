# packages/env — @bc-solutions-coder/env Agent Guide

The addressing a Start app derives from its deployment: the origin the browser reached it
on (`./request-origin`), the origin it can reach itself on (`./internal-origin`), the URL
prefix it is served under (`./base-path`), and the caller's address behind a proxy
(`./client-address`). Subpath-only — deliberately no `.` barrel. Adding a module is a new
subpath; `wallow/module-lists-in-sync` fails `pnpm lint` naming any list it is missing from.

**`WALLOW_WEB_INTERNAL_URL` is not `WALLOW_API_INTERNAL_URL`.** The first is the app's OWN
origin, the one this package resolves; the second is the upstream API a passthrough forwards
to. One word apart, different failure modes.

## No env reads at module scope

No module here may touch `process.env` or `import.meta.env` at module scope: every app's
`start.ts` is aliased into the CLIENT module graph too, so an import-time env read breaks
the client build or leaks a server value into it. Nothing enforces this mechanically —
treat it as load-bearing on review. Two binding shapes:

- Server-only files (`log-ingest.server.ts`, `client-address.server.ts`) bind
  `create...Resolver(process.env)` at module scope.
- `start.ts` declares a module-scope `let` and memoizes INSIDE the server callback
  (`requestOriginFor ??= createRequestOriginResolver(process.env)`).

`./base-path` is loaded by `apps/wallow-auth/vite.config.ts` at **config load time** as
plain Node ESM — it must stay free of validation and side effects; a throw on a missing
runtime variable would fail `vite build`.

## Forwarded headers are a trust decision

`x-forwarded-for` and `x-forwarded-proto` are believed only when the immediate peer is
inside `WALLOW_TRUSTED_PROXIES` — one trust policy for both headers. Unset, nothing is
trusted and the request's own values stand; behind a TLS-terminating ingress that means
`http` query keys — production compose sets the variable on both apps for exactly this
reason. The validation mechanics are readable in `src/request-origin.ts`. `PeerRequest`
here is the only copy — never re-declare it in an app.

## What stays out of this package

- **Anything a bundler substitutes.** `BASE_PATH` and `toAppHref` stay in
  `apps/wallow-auth/src/shared/lib/base-path.ts`: `import.meta.env.BASE_URL` is a
  build-time literal, and a library copy would freeze every published consumer's prefix
  to `/`.
- **Boot-time validation** — that is the SDK's `loadBffConfigFromEnv`; duplicating it here
  creates an `sdk → env` edge. This package throws nothing; it answers `undefined`.
