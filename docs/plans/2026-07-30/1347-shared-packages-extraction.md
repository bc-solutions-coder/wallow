**status: superseded**

> Superseded by `docs/plans/2026-07-31/1933-shared-packages-extraction-v2.md`. Three premises
> here are now false (`aliases.ts`, `zone-dag.test.ts`, `packages/config`'s name). Kept for its
> log-ingest security model and decision table, which v2 carries forward by reference.

# Slices 1–5 — Shared Package Extraction (`utils`, `config`, `logger`, `navigation`, rehomes)

> **For Claude:** REQUIRED SKILL: Use `/team-build` to implement this. You are a **coordinator** —
> decompose this into beads, then drive `bead-scout` / `bead-implementer` / `bead-verifier` through
> them. Do not read code, run tests, or carry payloads yourself.

**Goal:** Extract four shared packages so a fork inherits them instead of re-deriving them, then
rehome the tail of helpers that belong in an existing package rather than in an app's `shared/`.

**Design doc:** `docs/plans/2026-07-30/1201-shared-packages-and-app-zones-design.md`

**Predecessor:** `docs/plans/2026-07-30/1346-slice-0-app-zones-restructure.md` (Slice 0). That plan
is executed separately, with `/executing-plans`, and **must be fully landed and merged before any
bead here is claimed.**

---

## Why this is a team-build and Slice 0 was not

Slice 0 is one mutating sequence over one working tree — no parallelism, and its detail was already
fully derived. These five slices are the opposite: four largely independent packages, each a real
vertical slice (scaffold → implement → migrate both apps → document → gate), with one clean
dependency edge (`utils` first, everything else after). That is the shape team-build exists for.

The cost is that **these outlines are not step-level plans.** Slice 0 shipped with exact line
numbers, verified counts, and literal code; these are outlines by intent — the design says to expand
them once Slice 0's lessons are known. So Phase 1 scouting here is real work, not a re-derivation of
something already written down, and the scout beads matter more than they would have in Slice 0.

---

## Prerequisite gate — verify before decomposing

Delegate this check; do not run it yourself. Spawn one `bead-scout` to confirm **all** of the
following on `main`, and to write the answers onto the epic bead:

```bash
test -d apps/wallow-web/src/app && test -d apps/wallow-web/src/shared        # zones exist
test -d apps/wallow-auth/src/app && test -d apps/wallow-auth/src/shared
test -f apps/wallow-web/aliases.ts && test -f apps/wallow-auth/aliases.ts    # alias maps exist
grep -q '^catalogs:' pnpm-workspace.yaml                                     # Task 0.0 landed
grep -q 'zustand' pnpm-workspace.yaml                                        # the catalog:react seed
test -f apps/wallow-web/src/zone-dag.test.ts                                 # the DAG guard exists
pnpm check                                                                   # green baseline
```

**If any check fails, STOP and report.** Every slice below assumes the three-zone layout, the alias
map, and the catalogs. Building `packages/navigation` against a pre-Slice-0 tree means migrating
`src/stores/ui-store.ts` from a path that no longer exists.

---

## What Slice 0 established — context every agent needs, and none of them will have read

Put this on the **epic bead** verbatim, and instruct each feature scout to read it. Agents here will
not have seen Slice 0's plan file.

1. **Both apps are three-zone.** `src/app/` (routes, router, start, routeTree.gen, styles.css, and
   any server-only module), `src/features/<name>/` (each with a public `index.ts` barrel),
   `src/shared/` (`components`, `lib`, `stores`, `testing`, `types`). Root-level `src/*.test.ts(x)`
   files are app-wide policy specs.
2. **Cross-zone imports go through aliases** — `@app/*`, `@features/<name>` (barrel only, no deep
   path), `@shared/*` — declared in each app's root `aliases.ts` and mirrored into `tsconfig.json`,
   `vite.config.ts`, `vitest.config.ts`. Intra-zone imports stay relative.
3. **`apps/*/src/zone-dag.test.ts` enforces the import DAG** and pins `shared/`'s subdirectory
   allowlist. Adding a top-level directory under `shared/` fails that spec on purpose.
4. **Version pins come from pnpm catalogs**, not literals. `catalog:start` is exact-pinned
   (`@tanstack/react-start`, `react-router`, `react-router-ssr-query`); `catalog:react` is
   caret-ranged (`react`, `react-dom`, `@tanstack/react-form`, `@tanstack/react-query`, `zustand`).
   A new package declares `catalog:<name>`, never a literal version.
5. **`.github/workflows/sdk-publish.yml` runs `pnpm publish --no-git-checks`**, which resolves both
   `catalog:` and `workspace:*` at pack time. This is a standing constraint on that workflow.
6. **A co-move leaves its specifier untouched** — when both ends of a relative import move by the
   same amount, the specifier does not change. Slice 0 shipped that error twice.
7. **The vitest two-project split is keyed on file EXTENSION.** node is
   `["src/**/*.test.ts", ...nodeTsxSpecs]`; browser is `["src/**/*.test.tsx"]` minus `nodeTsxSpecs`.
   A `.test.ts` can never run in Chromium; a browser spec must be `.test.tsx` and cannot use
   `node:fs`.

---

## Decomposition — the bead shape to build

One **epic**. Five **features** (one per slice). Tasks under each.

### Dependency graph

```
                 ┌─────────────────────────────────────┐
  utils (S1) ────┼──> config (S4 deps: none beyond utils)
                 ├──> logger (S3)
                 └──> navigation (S4)  [also: ui, styles, zustand]
                                │
  all four ─────────────────────┴──> rehomes (S5)
```

`config`, `logger` and `navigation` are mutually independent and **should run concurrently** once
`utils` closes. `navigation` is by far the largest — start it in the same wave, not after.

### The serialization hazard the graph does not show

Every package's "wire into both apps" task edits the **same four files**:
`apps/wallow-web/package.json`, `apps/wallow-auth/package.json`, and both `Dockerfile`s. The docs
task of every package edits `docs/toc.yml` and the repo-map tables in `CLAUDE.md` / `apps/CLAUDE.md`.

**Chain those tasks with `bd dep add` so exactly one runs at a time**, even though their parent
features run in parallel. Two implementers editing `apps/wallow-web/package.json` concurrently is a
conflict the verifier will report as a mystery. Alternatively spawn those specific implementers with
worktree isolation — but the dependency chain is simpler and this is not the slow part.

### Task shape within each package feature

1. **scout** — read the design doc + the existing package this one is modelled on; write findings
   onto the feature bead.
2. **scaffold** — manifest, exports map, tsconfig, build script, `check:exports` clean.
3. **content** — implement or migrate the modules, TDD.
4. **wire** — both apps' `package.json`, both Dockerfiles (two COPY lines each), both
   `extraBrowserOptimizeDeps` lists if any app-side spec mounts a consumer. *(chained — see above)*
5. **docs** — new `docs/development/` guide, `docs/toc.yml` entry, repo-map table rows. *(chained)*
6. **gate** — `pnpm check`, plus `test:e2e` for `navigation`.

**Acceptance criteria for every package feature** (give these to the verifier verbatim):

- `pnpm --filter @bc-solutions-coder/<pkg> build` succeeds and `pnpm check:exports` is clean.
- The package declares **no literal versions** for anything the catalogs cover.
- `apps/*/src/docker-workspace-copies.test.ts` passes in both apps — this is what catches a missing
  Dockerfile COPY line, and it fails *there* rather than minutes into a CI image build.
- `pnpm check` green from a clean install.
- The docs guide exists, is listed in `docs/toc.yml`, and the repo-map tables in `CLAUDE.md` and
  `apps/CLAUDE.md` name the package.

---

## Decisions already made — DO NOT REOPEN

An autonomous agent with a plausible alternative will relitigate these. Each was decided against a
specific failure. Put this list on the epic bead and have every implementer read it.

| Decision | Why it is closed |
| --- | --- |
| **`packages/utils` subpath names are `./format`, `./string`, `./array`, `./result`, `./guards`** | These are the design doc's names. Subpaths are public API; renaming one is breaking for forks. An earlier draft renamed three. |
| **On the BFF path only, the logger's CSRF token rides in the BODY on the `sendBeacon` path**, and `handleLogIngest` accepts it from either body or header | `sendBeacon` cannot set headers, and wallow-web's route lives under `/bff/`, where the BFF blanket-rejects every unsafe method that does not echo the token. Not optional there. **Not universal** — see "Log ingest security model" for why sessionless apps do not get a CSRF substitute. |
| **Log ingest is guarded by origin + caps + per-IP rate limit + server-side stamping, in BOTH apps** | These are the controls this endpoint class actually needs; CSRF is inherited from route placement, not from log semantics. Full rationale in "Log ingest security model". |
| **`ui-store.ts` moves wholesale into `packages/navigation` as `useNavStore`** | All five members are navigation state and every consumer is `DashboardNav`, `DashboardLayout`, or one of their nine specs. There is no non-navigation part to leave behind. |
| **`apps/wallow-web/src/shared/stores/` is DELETED, with no pass-through re-export** | A shim that exists only to preserve an import path is what a fork inherits and never removes. Re-adding an app-level store when the app has real non-nav global UI state is a two-line change. |
| **`zustand` is a `peerDependency` of `packages/navigation` (`catalog:react`), and also a devDependency at the same range** | The store is a module-global singleton; two copies of the package means two stores and a nav that silently desyncs. Same hazard class `@bc-solutions-coder/query` exists to solve for `QueryClient`. |
| **`packages/utils` has empty `dependencies` and `peerDependencies`, `"lib": ["ESNext"]`, `"types": []`** | This is the charter that makes a generic utility package acceptable at all. It is machine-enforced by a spec, not a convention. |
| **`DashboardNav` imports `@bc-solutions-coder/ui` by per-component SUBPATH, never the root barrel** | A spec stubs `@tanstack/react-router` down to `Link`; the barrel drags in `FocusOnNavigate` → `useRouterState`, which the stub cannot satisfy. The explanatory comment atop `DashboardNav.tsx` moves with the code. |
| **Nav testids derive from `testIdPrefix` + `id`, defaulting to `"dashboard"`** | Reproduces `dashboard-nav-organizations`, `dashboard-nav-drawer`, `dashboard-logout-link` exactly, so the E2E specs and seven `__screenshots__` suites do not churn. |
| **`packages/navigation` has NO `auth` and NO `sdk` edge** | The visibility predicate is an app-supplied prop; the logout control is a footer slot. |

---

## Blockers — resolve before the affected feature is scoped, not during it

Genuine open questions. A `bead-implementer` that hits one mid-task will invent an answer. Make each
**still-open** one its own blocking bead that a human or a scout+decision closes first. Item 1 is
struck through because it is now decided; it stays on the list so the question is not re-raised.

1. ~~`wallow-auth` has no BFF, and therefore no CSRF token to send.~~ **RESOLVED — see "Log ingest
   security model" under Slice 3.** Kept here so nobody re-derives it: wallow-auth mounts
   `createApiPassthrough`, holds no session, and `readCsrfCookie()` returns `null` there. The
   resolution is that it gets its own ingest route and CSRF is not the control that matters for this
   endpoint. **No longer blocks.**
2. **`packages/navigation`'s dependency list is stated differently in the design doc and in this
   plan.** Reconcile before any navigation task starts. **Blocks Slice 4 entirely.**
3. **`site-links.ts` — `navigation` or `styles`?** It is branding-adjacent. The design leaves it
   open. **Blocks the Slice 5 rehome task only**; decide it with the finished packages in front of
   you, which is the whole reason it is in Slice 5.
4. **`apps/examples/minimal-app` and `scripts/fork-smoke/` scope.** Neither adopted the zones in
   Slice 0. Do they take the new packages? `scripts/fork-smoke/`'s README documents the canonical
   fork layout, so its answer is also a docs answer. **Blocks the Slice 1 wire task**, which is the
   first one that would have to decide.

---

## Slice 1 — `packages/utils`

The bottom of the dependency graph and zero-risk, which is why it goes first: it proves the
new-package pipeline (manifest, exports map, build, `check:exports`, workspace link, Dockerfile COPY
lines) before a slice with real behaviour rides on it.

1. Scaffold `@bc-solutions-coder/utils` from `packages/query`'s manifest shape (smallest existing
   package). Five thinly-populated subpaths. **Use the design doc's names — `./format`, `./string`,
   `./array`, `./result`, `./guards`** — subpaths are public API where a rename is breaking for
   forks.
2. **Machine-enforce the charter**, which is the whole reason this package is allowed to be generic:
   - a spec asserting `dependencies` and `peerDependencies` are empty,
   - `"lib": ["ESNext"]` and `"types": []` in its tsconfig, so a DOM or Node API will not compile,
   - an oxlint override banning `react`, `react-dom`, `zustand` and `@bc-solutions-coder/*` under
     `packages/utils/**` — and, because an oxlint `overrides` entry REPLACES the root rule's options
     rather than merging them, re-declaring the root's `no-restricted-imports` bans,
   - an export-coverage spec diffing the exports map against the source tree.
3. Seed each subpath from something that already exists in the apps, not from imagination.
4. Add to both apps' `package.json`, both Dockerfiles (two COPY lines each — the
   `docker-workspace-copies.test.ts` spec will tell you if you miss one), and both
   `extraBrowserOptimizeDeps` lists if any app-side spec mounts a consumer.
5. Docs: a new `docs/development/` guide + `docs/toc.yml` entry + the repo-map table in `CLAUDE.md`
   and `apps/CLAUDE.md`.

## Slice 2 — `packages/config`

Env/config validation. Depends on `utils` only.

1. Scaffold; define the schema-validated env contract.
2. Migrate `apps/wallow-auth/src/shared/lib/base-path.ts`'s `BASE_PATH` derivation and both apps'
   `WALLOW_API_INTERNAL_URL` reads onto it.
3. Fail loudly at boot on a missing/invalid var — the point of the package is that a fork learns
   about a misconfiguration at startup, not at the first request.
4. Same manifest/Dockerfile/docs checklist as Slice 1.

> `apps/wallow-auth/vite.config.ts` imports `./src/shared/lib/base-path` **at config load time** —
> a build file reaching into `src/`. Whatever this slice does to that module has to keep working
> from a Vite config, which runs before any app bundle exists.

## Slice 3 — `packages/logger`

Depends on `utils`. The design doc's logger section has been **corrected to match the security model
below** — both now say "browser → app server → OTLP", not "through the BFF". If you find a copy
still claiming BFF transport, this file wins.

1. Scaffold: a browser entry that buffers and posts, a server entry that writes structured records.
2. **One transport, one record format, one server handler, TWO mount points.** The browser entry
   takes the ingest path as configuration and knows nothing about CSRF; it takes an optional
   credential hook that the BFF app supplies and the passthrough app does not.
   - `wallow-web` mounts the handler under `/bff/logs` (existing `/bff/$` namespace).
   - `wallow-auth` mounts it as **its own Start server route** — it has a server; that was never the
     problem. Do not route logs through `createApiPassthrough` (see "rejected" below).
3. Wire the existing `x-request-id` correlation contract from the SDK through log records.
4. Replace the five `console.*` call sites the audit found.
5. Point it at the `grafana/otel-lgtm` stack already in `docker/`.
6. Same manifest/Dockerfile/docs checklist.

### Log ingest security model — DECIDED, resolves blocker 1

**CSRF is not the control this endpoint needs, and never was.** `/bff/logs` inherits the check
because of *where it lives* — the BFF blanket-rejects every unsafe method that does not echo the
token — not because log ingest has CSRF semantics. Classic CSRF damage requires the endpoint to act
with the victim's authority; writing a log record does not. So `wallow-auth` does **not** get a CSRF
substitute. It gets the controls this endpoint class always needed, and so does `wallow-web`.

`handleLogIngest` applies all of these **in both apps**:

| Guard | Why, and what already exists |
| --- | --- |
| **Origin allowlist** — reject any POST whose `Origin` is not this app's own | The load-bearing one, and the only one that survives the beacon path: `Origin` is a forbidden header name, so script cannot forge it, and a cross-origin `sendBeacon` carries the attacker's. **New code** — there is no origin or `Sec-Fetch-Site` checking anywhere in `packages/sdk/src/server/` today. |
| **Payload caps** — max body bytes, max records per batch, max message length | Reject, do not truncate. `sendBeacon` caps at 64 KiB regardless, so treat that as the ceiling, not the budget. |
| **Per-IP rate limit** | The plumbing exists: `CLIENT_IP_HEADER` and the `PeerRequest.ip` seam were built for Wallow-tt5j so the API could rate-limit through the proxy. Reuse that seam; **the limiter itself is new** — today all rate limiting is backend-side. |
| **Server-side stamping** — timestamp, IP, tenant, `x-request-id` | Never trust a client's copy of any of these, or a forged record can impersonate a real one. |
| **CSRF — `wallow-web` only** | Not optional there: the route is under `/bff/` and the BFF gates all POSTs. `wallow-auth` has no session, so `readCsrfCookie()` returns `null` and there is nothing to check. |

**Rejected, with reasons — do not revisit:**

- *Server-side logging only for `wallow-auth`.* Loses client-side error capture on login, MFA and
  consent — the highest-value logs in the product, from unauthenticated users you have no other
  visibility into.
- *Proxy logs through to the API.* Puts an unauthenticated write endpoint on the real API surface,
  and the passthrough forwards verbatim, so you get no origin check, no caps, no local rate limit.
  Strictly worse than a local route.
- *Drop the CSRF check on the shared path so one code path serves both.* The asymmetry is real;
  encode it in configuration, not by weakening `wallow-web`.

**One open sub-question, scoped to `wallow-web` and safe to defer.** Note the design already uses
`fetch(..., { keepalive: true })` with an `x-csrf-token` **header** for normal flushes — only the
**terminal unload flush** uses `sendBeacon`, and only that one path needs the token in the body.
MDN's own note on `sendBeacon` says code needing to set request properties should use
`fetch` + `keepalive` instead, which suggests the terminal flush could use it too and drop the
body-carried token entirely. Evaluate it, but **check browser support first** — `keepalive` landed
in Firefox far later than `sendBeacon` did, and both share the same 64 KiB quota. It changes nothing
for `wallow-auth`, which has no token on any path, so it must not block this slice.

## Slice 4 — `packages/navigation`

The largest slice. Depends on `ui`, `styles`, `utils` and `zustand`; deliberately **no `auth` and no
`sdk` edge** — the visibility predicate is an app-supplied prop and the logout control is a footer
slot. **Blocked on open question 2 (dependency list reconciliation).**

1. **`ui-store.ts` moves into the package as a navigation store — DECIDED, not open.** Navigation
   owns its own state; an app-level store that happens to hold nav flags is the thing that couples
   every fork's app shell to the nav implementation.

   **The whole store moves, and that is a finding, not an assumption.** All five members
   (`isNavCollapsed`, `toggleNavCollapsed`, `isMobileNavOpen`, `openMobileNav`, `closeMobileNav`) are
   navigation state, and every consumer is `DashboardNav`, `DashboardLayout`, or one of their nine
   specs. `ui-store` was named aspirationally; there is no non-navigation part to leave behind today.

   - `packages/navigation` exports **`useNavStore`** (renamed from `useUiStore` — the name should say
     what it owns) with the same five members and the same two-axis contract. **Carry the
     `TWO AXES, NOT ONE` comment across verbatim**: `isNavCollapsed` is the desktop rail,
     `isMobileNavOpen` is the mobile drawer, and neither may be derived from the other. That comment
     records a real regression (Wallow-0byr.1), not a preference.
   - **`apps/wallow-web/src/shared/stores/` is deleted in this slice**, since it becomes empty. Do not
     leave a pass-through re-export or an empty store behind — re-adding an app-level `ui-store` when
     the app actually has non-nav global UI state is a two-line change, and a shim that exists only to
     preserve an import path is exactly the kind of thing a fork inherits and never removes.
     **Remember to drop `stores` from the `shared/` subdirectory allowlist in `zone-dag.test.ts` at
     the same time**, or that assertion goes stale in the permissive direction.
   - **The store is a module-global singleton, and moving it into a package puts that singleton's
     identity at the mercy of resolution.** This is the same hazard class `@bc-solutions-coder/query`
     exists to solve for `QueryClient` — two copies of the package means two stores and a nav that
     silently desyncs. Declare `"zustand": "catalog:react"` as a **peerDependency** (Slice 0's Task
     0.0 seeded the catalog entry for exactly this), so the app supplies the one copy rather than the
     package bundling a second; keep it in `devDependencies` too, at the same `catalog:react`, so the
     package's own specs resolve it. Export the store from exactly one entry — never duplicated
     across subpaths, which reintroduces the two-instances problem inside a single install. **Move
     `ui-store.test.ts`'s re-import identity case with it**, strengthened to assert identity across a
     *package* import rather than a relative one.
   - The nine consuming specs move with the components in step 2 and switch to `useNavStore`.
2. Move `DashboardNav.tsx`, `DashboardLayout.tsx`, `nav-icons.ts` and their specs.
   **Preserve the module-graph hazard documented atop `DashboardNav.tsx`**: it imports
   `@bc-solutions-coder/ui` by per-component subpath, not the root barrel, because a spec stubs
   `@tanstack/react-router` down to `Link` and the barrel drags in `FocusOnNavigate` →
   `useRouterState`, which the stub cannot satisfy. Keep that comment with the code.
3. Testids derive from `testIdPrefix` + `id`, defaulting to `"dashboard"` — this reproduces
   `dashboard-nav-organizations`, `dashboard-nav-drawer` and `dashboard-logout-link` exactly, so the
   E2E specs and the seven `__screenshots__` suites do not churn.
4. Preserve all three modes and `data-nav-open`, including the collapsed rail's icon-only rendering
   with the label moved to `aria-label`.
5. Reconcile the dependency list — the design and this plan state it differently. Do it before step 1.
6. Migrate wallow-web to consume it; verify with `test:e2e` and the cross-app journey.
7. Same manifest/Dockerfile/docs checklist.

> **This is the one slice whose acceptance criteria include E2E.** `./scripts/e2e.sh` runs all three
> Playwright suites against the containerised stack. The testid and screenshot constraints above
> exist precisely so that run comes back clean; a churned baseline means step 3 was not honoured.

## Slice 5 — remaining rehomes

The tail the other slices did not absorb: helpers that belong in `sdk`, `styles` or `forms` rather
than in an app's `shared/`. Includes the design's open item on whether `site-links.ts` belongs to
`navigation` or `styles` (branding-adjacent) — decide it here with the packages in front of you.

Depends on all four packages. Expect this feature to be re-scoped once Slices 1–4 close: what is
left is defined by what they absorbed, so decompose it **last**, from a scout report rather than
from this outline.

---

## bd memory — seed these before Phase 1

These are timeless and repo-wide, and every implementer here will need them:

```bash
bd remember "New workspace packages need TWO Dockerfile COPY lines per consuming app (manifest before pnpm install, sources before the build RUN); apps/*/src/docker-workspace-copies.test.ts is the guard that catches a missing one before CI's image build does."
bd remember "Package versions for react, react-dom, @tanstack/react-form, @tanstack/react-query and zustand come from the 'react' pnpm catalog; @tanstack/react-start, react-router and react-router-ssr-query from the 'start' catalog. Declare catalog:<name>, never a literal."
bd remember "A zustand store or QueryClient exported from a workspace package must declare its runtime as a peerDependency, not a dependency: two resolved copies means two singletons and silent desync."
bd remember "An oxlint overrides entry REPLACES the root rule's options rather than merging them, and later overrides win. A per-package no-restricted-imports override must re-declare the root's bans."
```

Per `CLAUDE.md`'s memory discipline: no dates, no bead IDs, no plan references, no line numbers.

---

## Reminders

- `docs/plans/` is gitignored. Never `git add` this file.
- Run `pnpm --filter @bc-solutions-coder/sdk build` before typechecking an app — apps typecheck
  against `dist/`.
- Commit messages: Conventional Commits, lowercase, imperative, no trailing period, first line < 72
  chars. A new package is `feat(<pkg>):`. Unlike Slice 0, these slices **do** cut releases.
- **These outlines are not step-level plans.** Where a bead's scope is genuinely ambiguous, that is a
  scout task, not an implementer guess. Three of the four listed questions are still open (item 1 is
  decided); a fifth is an escalation.
- Per `CLAUDE.md`, work is not complete until `git push` succeeds. Per `.claude/rules/TEAMS.md`,
  shut each agent down as soon as its role is complete — scouts after Phase 1, not after the build.
