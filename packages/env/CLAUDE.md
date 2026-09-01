# packages/env — @bc-solutions-coder/env Agent Guide

The addressing a Start app derives from its deployment: the origin it can reach itself on
(`./internal-origin`) and the URL prefix it is served under (`./base-path`). Subpath-only —
deliberately no `.` barrel. Adding a module is a new subpath; `wallow/module-lists-in-sync`
fails `pnpm lint` naming any list it is missing from.

**`./published-global` is the document channel's shared machinery** — the escape rules for
the inline `<script>` a server render publishes a per-deployment value with, and the
raw read-back off `globalThis`. Every publish/read-back pair (`./auth-origin`, styles' fork
links, wallow-auth's web-app URL) delegates to it so the two halves cannot drift; each
caller keeps its own VALIDATION and fallback. It lives here rather than
`@bc-solutions-coder/utils` because this package already owns the pattern's canonical
instance and the bottom-of-graph trio (utils, env, logger) may not import each other.

**Request-origin and client-address resolution do NOT live here.** They are the trusted-proxy
decision, and they ship on the SDK's dependency-free `@bc-solutions-coder/sdk/server/forwarded`
subpath so an external consumer needs nothing beyond the SDK — see `packages/sdk/CLAUDE.md`.
Do not bring them back.

**`WALLOW_WEB_INTERNAL_URL` is not `WALLOW_API_INTERNAL_URL`.** The first is the app's OWN
origin, the one this package resolves; the second is the upstream API a passthrough forwards
to. One word apart, different failure modes.

## No env reads at module scope

No module here may touch `process.env` or `import.meta.env` at module scope: every app's
`start.ts` is aliased into the CLIENT module graph too, so an import-time env read breaks
the client build or leaks a server value into it. Nothing enforces this mechanically —
treat it as load-bearing on review. Every helper takes the env record as a parameter and the
one `process.env` read stays at the call site (`start.ts` reads it INSIDE the server callback).

`./base-path` is loaded by `apps/wallow-auth/vite.config.ts` at **config load time** as
plain Node ESM — it must stay free of validation and side effects; a throw on a missing
runtime variable would fail `vite build`.

## What stays out of this package

- **Anything a bundler substitutes.** `BASE_PATH` and `toAppHref` stay in
  `apps/wallow-auth/src/shared/lib/base-path.ts`: `import.meta.env.BASE_URL` is a
  build-time literal, and a library copy would freeze every published consumer's prefix
  to `/`.
- **Boot-time validation** — that is the SDK's `loadBffConfigFromEnv`; duplicating it here
  creates an `sdk → env` edge. This package throws nothing; it answers `undefined`.
- **Trust decisions** — anything gated on `WALLOW_TRUSTED_PROXIES` belongs beside the
  proxies that act on it, in the SDK.
