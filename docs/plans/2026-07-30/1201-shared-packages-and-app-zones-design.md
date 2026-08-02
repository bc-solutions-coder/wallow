# Shared packages and app zones — design

**status: active**

Fork-first expansion of the pnpm workspace: four new packages, six rehomes, and a
three-zone restructure of both apps with alias-expressed import boundaries.

## Driver

Fork-first capability, not de-duplication. Exploration found almost nothing
duplicated today — exactly one byte-identical file across the two apps
(`src/lib/request-origin.ts`), one `toLocaleDateString` call repo-wide, and five
`console.*` calls in all non-test source. These packages exist so a fork inherits
logging, an app shell, formatting, and env validation without writing them, and
so the second consumer is designed for before it lands.

## Scope

**New packages (4)**

| Package | Owns |
| --- | --- |
| `@bc-solutions-coder/navigation` | nav rail, mobile drawer, collapse state, the layout frame |
| `@bc-solutions-coder/logger` | isomorphic structured logging, browser → app server → OTLP |
| `@bc-solutions-coder/utils` | pure TS helpers behind a machine-enforced charter |
| `@bc-solutions-coder/config` | zod-validated app-host env with fail-fast boot |

**Rehomes (6)**

| From | To |
| --- | --- |
| `apps/*/src/lib/request-origin.ts` | `packages/sdk/src/server/` |
| `apps/wallow-auth/src/lib/base-path.ts` | `packages/sdk/src/server/` |
| `apps/*/src/lib/brand-assets.ts` | `packages/styles` |
| `apps/wallow-web/src/lib/error-text.ts` | `packages/forms` |
| `apps/wallow-web/src/lib/site-links.ts` | `packages/navigation` |
| `apps/wallow-web/src/lib/use-is-desktop.ts` | `packages/navigation` |

**Cross-cutting** — three-zone app restructure (`app/`, `features/`, `shared/`),
three path aliases, oxlint DAG rules, feature barrels.

**Explicitly not doing** — no `errors` package (`error-text.ts` rehomes into
`forms`; `WallowError` stays in `sdk`), no `flags` package (no backend flag store,
zero call sites), no shared build-config package (see *Alias map ownership*).

## Dependency graph

```
utils          zero deps — pure TS, no React, no DOM
  ^
config         + zod, server-only
  ^
logger    .         browser: batching + beacon
          ./server  + config, OTel SDK -> OTLP
  ^
navigation     + ui, zustand, lucide-react
               peers: react, @tanstack/react-router
  ^
apps
```

Two deliberate non-edges:

- **`navigation` does not depend on `auth`.** Destinations carry an optional
  `requires`, and the app supplies `can={(r) => hasRole(user, r.role)}`. A fork
  with a different auth model still uses the package.
- **`logger` does not depend on `sdk`.** It only wants `x-request-id`, so it takes
  a `getCorrelationId()` binding instead of importing a heavy package.

## `@bc-solutions-coder/navigation`

Named `navigation`, not `shell` — `packages/web-shell` was deleted as a breaking
change two commits ago and the name would be actively confusing in `git log` and
fork upgrade notes.

Single entry point (`.`), not a per-component catalog like `ui`: this is one
cohesive frame.

```ts
interface NavRequirement { role?: string; permission?: string }

interface NavDestination {
  id: string                    // stable key; also the testid suffix
  to: LinkProps["to"]           // typed against the app's route tree
  label: string                 // accessible name in all three modes
  icon: ComponentType<SVGProps<SVGSVGElement>>
  requires?: NavRequirement
}

<AppShell
  destinations={destinations}
  can={(r) => hasRole(user, r.role)}   // omit => everything visible
  header={<OrgSwitcher />}
  footer={<SignOut />}
  testIdPrefix="dashboard"             // default
>
  <Outlet />
</AppShell>

useNavStore   // zustand: isNavCollapsed, isMobileNavOpen + actions
useIsDesktop  // rehomed from apps/wallow-web/src/lib
```

**Slot composition.** Sign-out is the `footer` slot, not built in — the current
`NavLogout` imports `logout` from the SDK, and building it in would add a
`navigation -> sdk` edge for one button.

**Icons.** Destination icons come from the manifest. `nav-icons.ts` exists to stop
the three render modes drifting, but the manifest now enforces that structurally
(one entry, three renders), so the name-keyed map is not needed for destinations.
The three *control* icons (collapse, mobile menu, close) have no destination to
hang off, so the package ships lucide defaults with an `icons` override prop.

**Testids** derive from `testIdPrefix` + `id`, verbatim the `forms` package idiom.
Defaulting the prefix to `"dashboard"` reproduces `dashboard-nav-organizations`,
`dashboard-nav-drawer`, and `dashboard-logout-link` exactly, so the E2E specs and
the seven existing `__screenshots__` suites do not churn on extraction.

**Three modes and `data-nav-open` are preserved as-is**, including the rule the
original epic exists for: the collapsed rail renders icon-only with the label
moved to `aria-label`, never clipped text.

**State is zustand, moved wholesale from the app.** `useUiStore`'s three nav
concerns (`isNavCollapsed`, `isMobileNavOpen`, actions) move into the package. The
store module lives in the package, so there is exactly one store identity
regardless of how pnpm hoists the library — no provider, nothing for a fork to
wire. `apps/wallow-web/src/stores/ui-store.ts` is deleted; app-specific UI state,
if any appears, goes to `@app/stores`.

**Inherited SSR obligation.** `apps/*/vite.config.ts` carries a 12-line comment
about `use-sync-external-store/shim` loading a second React during SSR and
breaking every zustand-backed component. Both apps already alias around it; a fork
consuming `navigation` will need the same and will not know that. So
`packages/navigation/CLAUDE.md` documents the required `resolve.alias` entries,
and an SSR spec `renderToString`s `AppShell` — that failure is silent (empty
document, client-only fallback), so it needs a test, not prose.

**Out of scope for v1.** `PublicLayout` stays in wallow-web and `auth-layout.tsx`
stays in wallow-auth. Folding them in now is the over-reach that made `web-shell`
a grab-bag.

**Testing.** Storybook stories are the render coverage (one per mode, per the
`packages/ui` pattern); a co-located spec covers behavioural edges stories cannot
express (Escape dismissal, `can()` gating dropping the whole `<li>`, collapsed-mode
`aria-label`); plus the SSR spec above.

## `@bc-solutions-coder/logger`

Two entries mirroring the SDK's split. `.` is the browser core; `./server` is a
web-standard `Request -> Response` handler with no host-framework dependency, the
same contract `packages/sdk/src/server/handlers.ts` follows.

**One transport, two mount points.** `endpoint` and `getCsrfToken` are per-app
configuration, not package assumptions — see "Ingest security model" below for why
`wallow-auth` supplies neither a `/bff/` path nor a token.

```ts
// wallow-web — has a BFF, so the route lives under /bff/ and CSRF applies
const log = createLogger({
  service: "wallow-web",
  level: "info",
  endpoint: "/bff/logs",
  getCorrelationId: () => currentRequestId(),
  getCsrfToken: () => readCsrfCookie(), // BFF apps only; omitted in wallow-auth
  redact: ["password", "token", "email"],
})

log.info("form.submitted", { formId })
log.error("bff.logout.failed", {}, err)
const scoped = log.child({ tenantId })
```

**Events are named, not prose.** `log.info("form.submitted", {...})` — dotted,
low-cardinality, groupable in Grafana. Free-text messages are unqueryable at
volume, and this package exists so a fork can read its logs offsite.

**Wire contract**, exported from both entries so sender/receiver drift is a type
error:

```ts
interface LogEvent {
  ts: string
  level: "debug" | "info" | "warn" | "error"
  event: string
  attrs: Record<string, unknown>
  correlationId?: string
  error?: { name: string; message: string; stack?: string }
}
```

**Delivery and the beacon constraint.** Events buffer and flush on size, interval,
and `visibilitychange`/`pagehide`. Normal flushes use `fetch(..., { keepalive:
true })` carrying `x-csrf-token` as a header. The terminal unload flush must use
`navigator.sendBeacon`, which cannot set headers — so on that path the token rides
in the body, and `handleLogIngest` accepts it from either place. Stated here
rather than discovered later as "logs vanish on tab close."

CSRF context: the BFF uses double-submit — a JS-readable `<cookie>-csrf` cookie
echoed in an `x-csrf-token` header (`packages/sdk/src/server/proxy.ts`,
`CSRF_HEADER`).

**Ingest security model — and why CSRF is not the control that matters.**
`wallow-auth` mounts `createApiPassthrough`, not `createWallowBffServer`: it holds
no session and no cookie jar, so `readCsrfCookie()` returns `null` there and there
is no token to send by either route. That is not a gap to patch. `/bff/logs`
inherits its CSRF check because of *where it lives* — the BFF blanket-rejects every
unsafe method that does not echo the token — not because log ingest has CSRF
semantics. Classic CSRF damage requires the endpoint to act with the victim's
authority; writing a log record does not.

So the real controls, applied in **both** apps:

- **Origin allowlist.** The load-bearing guard, and the only one that survives the
  beacon path: `Origin` is a forbidden header name, so script cannot forge it and a
  cross-origin `sendBeacon` carries the attacker's. New code — nothing in
  `packages/sdk/src/server/` checks `Origin` or `Sec-Fetch-Site` today.
- **Payload caps.** Max body bytes, max records per batch, max message length;
  reject rather than truncate. `sendBeacon`'s 64 KiB is the ceiling, not the budget.
- **Per-IP rate limit.** Reuse the `CLIENT_IP_HEADER` / `PeerRequest.ip` seam built
  for Wallow-tt5j. The limiter itself is new; all rate limiting today is backend-side.
- **Server-side stamping** of timestamp, IP, tenant and `x-request-id`, so a forged
  record cannot impersonate a real one.
- **CSRF on `wallow-web` only**, where it is not optional.

**Three failure rules**, because a logger that fails loudly is worse than none:

1. Transport errors never call the logger — fall back to `console`, disable
   transport for a backoff window.
2. The buffer drops oldest on overflow and reports a `logger.dropped` count rather
   than growing unbounded.
3. Ingest answers `204` regardless — the page never changes behavior because
   telemetry is down.

**Redaction happens twice.** Key-based scrubbing client-side before an event
leaves the browser, and again server-side before OTLP emit. The server pass is
authoritative (a fork cannot bypass it from the client); the client pass is
defense in depth so PII never sits in a buffer or a beacon body.

**Server side.** `handleLogIngest(request, { session, otlp, allowedOrigins, limits })`
applies the guards above, validates the batch, enriches each event with the
`traceparent` and — **when a session is present** — its user/tenant, redacts, and
emits OTLP logs to the endpoint `@bc-solutions-coder/config` validated — landing in
the `grafana/otel-lgtm` stack already in `docker/`, correlated with backend spans
through the `x-request-id` the SDK already stamps (`packages/sdk/src/request-id.ts`).
Dev config emits to console.

`session` is **optional**: `wallow-auth` has none, and its records are simply
unattributed to a user — which is correct, since its highest-value logs come from
visitors who have not authenticated yet.

Each app mounts the handler explicitly as one server route, at a path that suits its
own topology:

| App | Route | Why |
| --- | --- | --- |
| `wallow-web` | `src/app/routes/bff/logs.ts` | existing `/bff/$` namespace, session available, CSRF enforced |
| `wallow-auth` | a standalone Start server route | it has a server; it has no `/bff/` namespace. Do **not** route logs through `createApiPassthrough` — that forwards verbatim, giving no origin check, no caps and no local rate limit, and would put an unauthenticated write endpoint on the real API surface. |

## `@bc-solutions-coder/utils`

**Subpaths only, no root barrel.** Every import names a category
(`@bc-solutions-coder/utils/format`). Dropping the `.` export is deliberate: a
barrel makes `import { anything } from "utils"` frictionless, and frictionless is
how a junk drawer forms. `packages/web-shell` is the local precedent for what that
costs.

Subpaths: `./format`, `./string`, `./array`, `./result`, `./guards` — all five
seeded now, thinly populated.

**The charter is four green checks, not a review habit:**

| Rule | Enforced by |
| --- | --- |
| Zero runtime dependencies | test reads `package.json`, asserts `dependencies` empty |
| No DOM | tsconfig `"lib": ["ESNext"]`, `"types": []` — `window`/`document` fail typecheck |
| No React, no workspace packages | oxlint override banning `react`, `react-dom`, `zustand`, `@bc-solutions-coder/*` under `packages/utils/**` |
| No domain knowledge | falls out of the above — it cannot import an SDK type |
| Every export tested | spec diffs the export list against covered names |

The last two are the point: they would normally stay aspirational, but the import
ban makes "no domain knowledge" structural, and the export diff keeps a thin seed
from becoming quietly unverified.

**Two consequences of the no-DOM rule.** `formatDate` cannot read
`navigator.language`, so locale and `timeZone` are explicit options defaulting to
the runtime's. That is the right answer regardless — SSR and the browser must
agree or every rendered date is a hydration mismatch. And `Intl` formatter
construction is expensive, so instances are memoized per `(locale, options)` key.

## `@bc-solutions-coder/config`

Narrower than it first appears. `packages/sdk/src/server/` already owns BFF/OIDC
env: `loadBffConfigFromEnv`, `INTERNAL_ORIGIN_ENV_KEY`, `resolveInternalOrigin`.
Re-implementing those would seed exactly the overlap that makes a grab-bag.

**Boundary: the SDK keeps its own env; `config` owns the app-host env and the
mechanism.** What remains is three things:

1. `defineEnv()` reporting **every** invalid variable at boot, not dying on the
   first.
2. Typed app-host vars — `PORT`, `BASE_PATH`, `OTEL_EXPORTER_OTLP_ENDPOINT`.
3. A single documented manifest a fork can diff against.

Server-only, guarded by an oxlint rule confining the import to app server routes.

## App structure — three zones

```
src/
  app/                  composition root — nothing imports it
    routes/             TanStack file-based routes (routesDirectory)
    routeTree.gen.ts    (generatedRouteTree)
    router.tsx
    lib/                app-level only: bff.ts, host wiring
    stores/             app-specific UI state, if any
  features/
    <feature>/
      index.ts          THE public contract — the only importable path
      api.ts            existing SDK seam, internal to the feature
      components/
  shared/
    components/         ready-indicator, SelectControl, PublicLayout
    hooks/              cross-feature hooks
    lib/                cross-feature pure helpers
    stores/             cross-feature UI state
    testing/            test utilities
    types/              cross-feature types
```

`shared/`'s top-level subdirectories are exactly that list — an allowlist, pinned
by `zone-dag.test.ts`. See "Promotion into `shared/`" below for why.

`routesDirectory` and `generatedRouteTree` are configurable through the same
`router` key `vite.config.ts` already uses for `routeFileIgnorePattern`
(`@tanstack/router-plugin` config schema), so `src/app/routes/` is achievable.

~~Top-level *files* (`start.ts`, `styles.css`, `vite-env.d.ts`) stay put; only
folders collapse to the three zones.~~

**Revised in Slice 0.** `start.ts` and `styles.css` moved into `src/app/` as well.
Both are host concerns — `start.ts` IS the composition root and `styles.css` is
imported for side effects by `app/routes/__root.tsx` — so leaving them at the top
level would have put the composition root outside the zone that exists to hold it.
Only `vite-env.d.ts` and the root-level policy specs stay directly under `src/`.

**Server-only modules live in `app/`, never `shared/`.** This is the sharpest edge
of the whole restructure. `bff.ts` pulls in `node:crypto` and `openid-client`;
`shared/` is reachable from every feature by definition, so a server-only module
placed there is one import away from the client bundle. `app/lib/` is the home for
anything that must not be reachable, and the DAG is what makes "must not be
reachable" true rather than aspirational.

### Aliases

```
@app/*       -> src/app/*
@features/*  -> src/features/*
@shared/*    -> src/shared/*
```

Three, not five. `@app/*` is unambiguous because it maps to `src/app`, not `src` —
an `@app/* -> src/*` entry would overlap every other alias and give two spellings
for the same module, the exact drift the pin test exists to prevent.

### Import DAG

| Zone | may import | may not |
| --- | --- | --- |
| `app/**` | `@features/<name>` (barrel only), `@shared/*`, packages | — it's the top |
| `features/**` | `@shared/*`, packages, relative-to-self | `@features/*` **at all**, `@app/*` |
| `shared/**` | packages | `@features/*`, `@app/*` |

Plus, everywhere: a specifier that RESOLVES outside its own zone must be spelled
as an alias. Without that row the rules above are one `../lib/foo` away from being
bypassed. Slice 0 sharpened the phrasing: the ban is on the resolved edge, not on
the shape of the string, so a relative import of any depth is fine as long as it
lands inside the importer's own zone (`app/routes/dashboard/x.tsx` -> `../../lib/y`
is legal; `features/login/x.tsx` -> `../../shared/lib/y` is not, and must be
`@shared/lib/y`). The one exemption is a root-level policy spec reaching outside
`src/` — `alias-map.test.ts` has to read `vite.config.ts`, which is the whole point
of it.

**One rule solves two problems.** Banning `@features/*` inside `features/**` stops
cross-feature contamination *and* stops a feature module importing its own barrel
— the self-referential cycle. No per-feature enumeration, no glob negation, because
features already import relatively within themselves.

### Enforcing the DAG

> **Superseded by Slice 0: the DAG is enforced by `src/zone-dag.test.ts`, one spec
> per app, and `.oxlintrc.json` was left untouched.** The section below is kept for
> its empirical findings about oxlint's override semantics, which remain true and
> still govern the bans oxlint *does* own (the react-query facade rule).
>
> The reason for the switch is not effort — it is that `no-restricted-imports`
> globs the specifier STRING, and the rule here is about where a path RESOLVES.
> Whether `../../lib/thing` leaves its zone depends entirely on which file wrote
> it. The `../../**` catch-all the table below proposes is what a string matcher
> has to fall back on, and it is both too strict (it bans legitimate deep intra-zone
> imports) and too loose (`../../../wallow-auth/src/shared/*` walks straight around
> it). The spec instead resolves every specifier against its importer's real
> directory and judges the resulting edge, which is the rule as actually stated.
>
> Two things the spec gets that a lint rule could not:
>
> - **Dynamic `import("…")` is matched alongside static imports.** It is the exact
>   form server-only modules in `app/` are reached by, so a guard blind to it has a
>   hole shaped like the violation it exists to catch.
> - **The DAG constrains the PRODUCT graph, not the test graph.** A spec may import
>   `@app/routes/<name>` and mount the real route, because the component's contract
>   IS the route's `validateSearch` schema — testing it against a hand-rolled stub
>   would test the stub. Product modules get no such licence. Expressing that as a
>   lint override would mean whitelisting `**/*.test.tsx` globally, which is a much
>   blunter exemption than "a spec may reach `app`, and nothing else may".


oxlint's `no-restricted-imports` matches the **specifier string**, so it needs no
alias resolution, and the `import` plugin is not enabled, so there is no
unresolved-import checking to teach about the new paths. Aliases are **app-only**;
packages stay flat and relative.

**An `overrides` entry replaces the root rule's options rather than merging them.**
Verified empirically, not assumed: with the root banning `root-banned` and an
override on `src/features/**` adding one pattern, a `root-banned` import inside
`src/features/` stopped being flagged the moment the override existed, while the
same import in `src/app/` (not covered by the override) stayed flagged.

| file | import | no `overrides` | with `overrides` |
| --- | --- | --- | --- |
| `src/app/b.ts` | `root-banned` | flagged | flagged |
| `src/features/a.ts` | `root-banned` | flagged | **not flagged** |
| `src/features/a.ts` | `@features/other` | — | flagged |

The rule is plenty expressive (`paths`, `patterns`, `group`, `regex`,
`importNames`, `caseSensitive`); merge semantics were the only problem. So each of
the three zone overrides **re-declares the root's four `paths` entries and two
`patterns` verbatim**, then appends its zone rows. Duplication is the cost of not
silently losing the `@tanstack/react-query` ban inside `features/**` — the one ban
whose whole point is that it is unconditional.

```jsonc
{
  "files": ["apps/*/src/features/**"],
  "rules": {
    "no-restricted-imports": ["error", {
      "paths":    [ /* 4 root entries, verbatim */ ],
      "patterns": [ /* 2 root patterns, verbatim */
        { "group": ["@features/*", "@app/*"],
          "message": "features are isolated: no cross-feature or app imports" },
        { "group": ["../../**", "../lib/*", "../components/*"],
          "message": "cross-zone traffic must use an alias" }
      ]
    }]
  }
}
```

`oxlint-override-superset.test.ts` guards the duplication: it reads
`.oxlintrc.json` and asserts every root `paths` and `patterns` entry appears in
each of the three zone overrides. Adding a root ban without propagating it fails
the suite — the mirror is pinned, exactly as `alias-map.test.ts` pins the alias
map.

**Barrel-only is a glob, not an enumeration.** `@features/*/**` matches
`@features/apps/components/AppList` and `@features/apps/api` but *not*
`@features/apps` — also verified empirically. One pattern in the `app/**` override
enforces barrel-only access to every feature, with nothing to update when a
feature is added.

### Feature barrels

`index.ts` exports **entry points and types** — the route-level components the app
actually mounts, plus public types. Internal components, helpers, and `api.ts` stay
unexported.

```ts
// features/apps/index.ts
export { AppList } from './components/AppList'
export { RegisterAppForm } from './components/RegisterAppForm'
export type { AppSummary } from './types'
```

Deep imports into a feature are banned from outside (`@features/*/**`), permitted
within it via relative paths.

### Promotion into `shared/`

"If two features need it, it goes to `shared/`" is a rule with no brake, and Slice 0
found that the brake is the part that matters. The ladder:

1. **Compose at the route first.** When two features need the same *behaviour*, the
   first answer is that the route composes both features — not a shared component.
   Only genuinely presentational, feature-agnostic pieces go to `shared/components/`.
   Without this rule every "two features need X" resolves to promotion, and the
   subdirectory allowlist becomes the only thing holding the line.
2. **The trigger is not a count.** Two consumers **and** the module has no
   feature-specific types in its signature. If promoting it means widening a prop
   from `LoginSubmitState` to `unknown`, do not promote it: duplication is cheaper
   than a bad abstraction, and the de-typing cost has to be named out loud in the
   PR rather than absorbed silently.
3. **`shared/`'s top-level subdirectories are an allowlist** — `components`,
   `hooks`, `lib`, `stores`, `testing`, `types` — pinned by `zone-dag.test.ts`. You
   cannot dump if there is nowhere to dump to, and a new top-level directory is a
   design change that should fail until it is argued for. (`stores` comes off that
   list in Slice 4, when wallow-web's `ui-store` becomes `packages/navigation`'s
   `useNavStore`; an allowlist naming a directory nothing uses drifts permissive.)

**No barrel for `shared`.** A barrel expresses a bounded context's public
contract; `shared` is a toolbox, not a context. A barrel there would be one giant
module every consumer drags in, and would recreate the same self-import cycle
inside `shared`. `@shared/components/select-control` stays a deep import by design.

**Barrels stay thin**, but the reason stated here was slightly wrong and the
correction matters because it changes what to watch for. The comment atop
`DashboardNav.tsx` describes a real hazard — importing `@bc-solutions-coder/ui`'s
root barrel fails in a dev/test module graph because it drags in `FocusOnNavigate`
-> `useRouterState` — and the actual rule is:

> **A barrel is safe as long as no consumer stubs a module that a sibling export
> imports at module scope.**

The risk therefore scales with how aggressively specs stub router internals, **not**
with barrel size. A twenty-export barrel nothing stubs is fine; a three-export
barrel whose sibling reaches `useRouterState` under a spec that stubs the router is
not. Size is still worth keeping down for a separate and less dramatic reason — a
fat barrel pulls a feature's every component into whichever route chunk touches it —
but do not read a link failure as "the barrel got too big".

### Alias map ownership

**No shared build-config package.** A shared preset would mean deep-rooted build
files coupling every app to a package — rejected.

Instead the map lives in an **app-local** `apps/<app>/aliases.ts`: a plain data
module, not build machinery, not shared. That app's own `vite.config.ts` and
`vitest.config.ts` both import it and derive `resolve.alias`, so those two cannot
drift by construction. Only `tsconfig.json` cannot import it — JSON — so
`alias-map.test.ts` reads both off disk and pins `compilerOptions.paths` to the
module.

This is the `docker-workspace-copies.test.ts` idiom, which already solves exactly
this problem in this repo: the Dockerfile's `COPY` list is a hand-maintained
mirror of `package.json`, verified by a spec rather than abstracted into shared
machinery.

(Vitest does not merge a sibling `vite.config.ts` when `vitest.config.ts` exists,
which is why both import the module rather than one inheriting from the other.)

## Sequencing

Vertical slices, with the restructure pulled out in front.

**Slice 0 — restructure (both apps).** Move `src/` to `app/`/`features/`/`shared/`
in wallow-web and wallow-auth, land the three aliases, the DAG rules, feature
barrels, and `alias-map.test.ts`; rewire `routesDirectory`/`generatedRouteTree`.

**Slices 1–5 — one package each**, vertically: extract, rehome its helpers, migrate
both apps, document, test.

1. `utils` (bottom of the graph, zero risk)
2. `config`
3. `logger` (+ an ingest route in both apps — `bff/logs` in wallow-web, a standalone
   server route in wallow-auth)
4. `navigation` (+ the six rehomes it and the others absorb)
5. Remaining rehomes into `sdk`, `styles`, `forms`

Slice 0 is one wide diff on purpose. The alias churn is roughly fixed-cost whenever
it happens, and doing it once beats colliding with five extractions.

### Known collateral in slice 0

Moving files breaks path assumptions in:

- `apps/*/src/features-api-seam.test.ts` — scopes to `src/features/**` and
  `src/routes/**`
- both `vitest.config.ts` `nodeTsxSpecs` lists
- `apps/*/src/docker-workspace-copies.test.ts` if COPY paths shift
- `tanstackStart({ router: ... })` in both `vite.config.ts`

## Open items

- Whether `apps/examples/minimal-app` adopts the three zones or stays flat as a
  minimal reference.
- Exact `redact` default key list for `logger`.
- Whether `site-links.ts` belongs in `navigation` or `styles` (branding-adjacent).
