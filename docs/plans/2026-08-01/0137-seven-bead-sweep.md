**status: active**

# Seven-Bead Sweep Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan
> task-by-task.

**Goal:** Close Wallow-tvn3, Wallow-l77c, Wallow-luni, Wallow-uc2c, Wallow-1lt5, Wallow-75pg and
Wallow-a5mt in one pass, without touching anything the multi-org-user work owns.

**Architecture:** Four phases ordered by risk, cheapest first. Phase 0 closes the three beads that
other landed work already fixed (verify, then close — no code). Phase 1 is a docs truth pass. Phase
2 removes the last runtime `__require("react")` from the server bundle by aliasing the CJS
`with-selector` shim to a real ESM module. Phase 3 is the only substantial change: a shared
client-address resolver in `@bc-solutions-coder/env`, gated on TWO settings — a CIDR allowlist of
peers permitted to write `X-Forwarded-For` at all, and a hop index saying which entry to read —
consumed by both apps' log-ingest routes and all three apps' outbound API proxies. It also fixes an
independent header-forgery bug found while scoping it: no proxy deletes an inbound
`x-wallow-client-ip` before stamping its own.

**Tech Stack:** TypeScript, Vite 8 / Rolldown, Nitro, vitest (node + browser projects), pnpm
workspace, Caddy ingress, ASP.NET Core `UseForwardedHeaders`.

**Collision surface with the multi-org work:** none of these tasks touch
`api/src/Modules/Identity/**`, tenant/org claims, `seed.json`, or any EF migration. Phase 1 edits
two backend READMEs (`api/src/Shared/README.md`, `api/src/Modules/Notifications/README.md`); if the
other agent is rewriting those, do Phase 1 last.

---

## Reconnaissance already done — do not redo it

These facts were established before the plan was written. Trust them; re-verify only where a task
says to.

| Bead        | State on `main` today                                                                                                                                                      |
| ----------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Wallow-1lt5 | **Already fixed**, verified across the bead's whole scope: `apps/wallow-web/src/shared` runs 7 files / 84 tests with **zero** `does not recognize` warnings. The `Link` stubs in `PublicLayout.test.tsx` and `index.gate.test.tsx` are dead code — `PublicLayout.tsx` renders plain anchors, never a TanStack `Link`. The sole `activeProps` producer repo-wide is `packages/navigation/src/app-nav.tsx:84`. |
| Wallow-a5mt | **Already fixed**, verified by reading all 15 `api.test.ts` individually and by a sweep for every phrasing variant, not just the exact string. The 5 wallow-web files have no `SURFACE` constant at all; of the 10 wallow-auth files the comment is either absent or reads `/** The seam's whole surface, sorted. */`, which is **true** — `toSorted()` is lexicographic. |
| Wallow-uc2c | **Fixed**, confirmed by a real build: **one** react-query graph, all of it in `apps/examples/minimal-app/.output/server/_ssr/router-BLceR_E-.mjs`, with exactly one `QueryClientContext = import_react.createContext(void 0)` at `:1695`. |
| Wallow-luni | **Real and reproducible, but P2-shaped, not P0.** `apps/wallow-web/.output/server/_ssr/isElementDisabled-qaI2ByNu.mjs:2973-2974` holds the only two non-builtin `__require` calls in the whole output, and the IIFE at `:2966` instantiates the second React eagerly. Its one consumer (`:3062`) is Base UI's `useStoreLegacy`, which React 19 never selects — so nothing dispatches through it today. Cause is Vite's SSR externalization of `react`, not the module's CJS-ness. |
| Wallow-tvn3 | **Real. FIVE call sites**, not four: `apps/wallow-web/src/app/lib/bff.server.ts:151`, `apps/wallow-web/src/app/lib/log-ingest.server.ts:92`, `apps/wallow-auth/src/shared/lib/api-passthrough.server.ts:79`, `apps/wallow-auth/src/shared/lib/log-ingest.server.ts:52`, and `apps/examples/minimal-app/src/lib/api-passthrough.ts:53`. No code anywhere reads `x-real-ip`, `cf-connecting-ip`, `true-client-ip` or `x-client-ip`. |
| Wallow-l77c | **Real. THREE copies**, not two: `CLAUDE.md:88`, `apps/CLAUDE.md:18`, and `apps/wallow-web/README.md:22`. (The bead's own line numbers, 81 and 15, are stale.)               |
| Wallow-75pg | **Real, and larger than billing.** `api/src/Shared/README.md` and `api/src/Modules/Notifications/README.md` between them name **three** phantom modules — Billing, Metering (`Shared/README.md:59,66`) and Messaging (`Notifications/README.md:77`) — plus one wrong name, `PasswordResetEvent` for the real `PasswordResetRequestedEvent`. |

**The Wallow-luni root cause, spelled out** (it is not obvious from the code, and the obvious
explanation is wrong):

`@base-ui/utils/store/useStore.mjs`, `@tanstack/react-store` and `zustand/traditional` all import
`use-sync-external-store/shim/with-selector`, a CJS file whose body is a `process.env.NODE_ENV`
conditional `require`.

**That conditional is NOT the problem.** Rolldown resolves it statically: only the production
branch reaches the output — `.output/server/_ssr/isElementDisabled-qaI2ByNu.mjs:2896` carries the
`with-selector.production.js` licence header and the dev branch is absent entirely. Any reasoning
that starts "a CJS transform gives up on a NODE_ENV conditional require" is describing something
that did not happen.

The actual cause is the **externalization boundary**. Vite's SSR pass leaves `react` external:
`apps/wallow-web/node_modules/.nitro/vite/services/ssr/assets/isElementDisabled-qaI2ByNu.js:3-4`
reads `import * as React$1 from "react"`. Rolldown cannot lower a CJS `require()` of an **external**
module into a static import, so it emits `createRequire`. The `__require("react")` is already there
at that Vite stage (same file, `:2973-2974`), **before Nitro runs at all**.

The proof that this is the boundary and not the module: the identical CJS module compiles clean
where react is in-graph. `.output/server/_libs/@tanstack/react-form+[...].mjs:434-436` inlines the
same `with-selector.production.js` as `var React = require_react()`, and that chunk contains **zero**
`__require`.

React is genuinely fully bundled into the server output — no bare `"react"` specifier survives
anywhere in `.output/server` except those two lines — so the runtime require does resolve a
distinct second copy from `node_modules` (the Dockerfile is single-stage and ships them). The IIFE
at `:2966` runs at chunk load, so that second React is instantiated **eagerly**.

**Severity, stated accurately.** `import_with_selector` is consumed at exactly one place,
`:3062`, which is Base UI's `useStoreLegacy` — and `useStore.mjs` picks `useStoreFast` on React
≥ 19 (`canUseRawUseSyncExternalStore = isReactVersionAtLeast(19)`). So a second React is created
but no hook dispatches through it today. The empty-document hydration collapse belongs to the
already-landed `shim` → `react` alias, not to this bead. This is still worth fixing — an eagerly
instantiated second React is a live trap for the next dependency that reaches the legacy path —
but do not sell it as a broken app.

**Consequence for the fix:** because the cause is the boundary, the config-only options are
**candidates, not dead ends**, and one of them must be probed before a permanent runtime file is
added to `packages/config`. Task 2.2 does exactly that. Two are genuinely ruled out and need no
probe:

- `resolve.conditions` — `use-sync-external-store`'s exports map has no conditional objects
  (`"./shim/with-selector": "./shim/with-selector.js"` is a bare string), so no condition can
  redirect it.
- `optimizeDeps.include` — dev-only prebundling; `vite build` never consults it.

---

## Phase 0 — Verify and close (no code)

### Task 0.1: Confirm Wallow-1lt5

**Files:** none.

**Step 1: Run the two specs the bead names**

```bash
cd /Users/traveler/Repos/Wallow/apps/wallow-web
pnpm exec vitest run --configLoader runner \
  src/shared/components/DashboardLayout.test.tsx \
  src/shared/components/SignOut.contrast.test.tsx
```

Expected: `Test Files 2 passed (2)`, and **no** `React does not recognize the activeProps prop`
line anywhere in the output. (`--configLoader runner` is mandatory when running a subset by hand;
without it vitest cannot resolve `packages/styles/src/assets` and dies before the first test.)

**Step 2: Close with the evidence**

```bash
cd /Users/traveler/Repos/Wallow
bd note Wallow-1lt5 "Fixed by 53da0f7b (navigation extraction) and 5d2d6a6a (shared web spec prose strip). Both stubs in apps/wallow-web/src/shared/components/ destructure activeProps: _activeProps before spreading. Verified: 25 tests pass with zero React unknown-prop warnings."
bd close Wallow-1lt5
```

---

### Task 0.2: Confirm Wallow-a5mt

**Files:** none.

**Step 1: Sweep for the false claim, in every phrasing**

```bash
cd /Users/traveler/Repos/Wallow
grep -rn "namespace enumerates\|order an ESM\|ESM namespace\|enumeration order" \
  apps packages api docs --include='*.ts' --include='*.tsx' --include='*.md' \
  | grep -v node_modules
```

Expected: no output (exit 1). The bead's sweep target was `src/features/*/api.test.ts` in both
apps — all 15 files — and the phrase is gone from all of them.

**Step 2: Spot-check one seam spec still reads correctly**

```bash
sed -n '1,20p' apps/wallow-auth/src/features/login/api.test.ts
```

Expected: the header documents identity-vs-presence assertion and the magic-link GET, and the
`SURFACE` constant carries **either no comment or** `/** The seam's whole surface, sorted. */` —
which is true, since `toSorted()` is lexicographic. Five of the ten wallow-auth files carry that
comment; the five wallow-web files have no `SURFACE` constant at all. Either shape is what the bead
asked for.

**Step 3: Close**

```bash
bd note Wallow-a5mt "Read all 15 src/features/*/api.test.ts individually, plus a repo-wide sweep for every phrasing variant (namespace enumerates / order an ESM / enumeration order / declaration order / source order). Zero hits in any api.test.ts. The 5 wallow-web files have no SURFACE constant; of the 10 wallow-auth files the comment is either absent or reads 'The seam's whole surface, sorted', which is true because toSorted() is lexicographic. Closed as already-done."
bd close Wallow-a5mt
```

---

### Task 0.3: Confirm Wallow-uc2c

**Files:** none (verification only).

**Step 1: Build minimal-app**

```bash
cd /Users/traveler/Repos/Wallow
pnpm --filter @bc-solutions-coder/minimal-app build
```

Expected: a clean build emitting `apps/examples/minimal-app/.output/server/index.mjs`.

**Step 2: Count react-query graphs in the server output**

```bash
grep -rn "QueryClientContext = " apps/examples/minimal-app/.output/server
```

Expected: **exactly one** hit —
`.output/server/_ssr/router-<hash>.mjs:… var QueryClientContext = import_react.createContext(void 0);`
Two graphs means two `createContext` calls, and a provider from one cannot serve a consumer of the
other, so a single definition is the fact that settles the bead.

Do **not** use either of these, both of which were tried and are broken:

- `grep -rl "@tanstack/react-query" .output/server` returns **nothing** on a correct build — the
  bare specifier does not survive bundling. It reads as zero graphs and is a false signal in both
  directions.
- `grep -rc "QueryClientProvider\|createContext" .output/server/_libs/@tanstack/*` greps the wrong
  directory. `_libs/@tanstack/` holds only `react-router+[...].mjs`; react-query is in `_ssr/`. It
  returns a count of **react-router's** `createContext` calls, which is easily misread as a split.

The bead's failure mode was two graphs — one arriving bundled through
the `@bc-solutions-coder/query` workspace link, one arriving through the externalized
`@tanstack/react-router-ssr-query`. `wallowAppConfig()`'s
`ssr.noExternal: ["@tanstack/react-router-ssr-query", "@tanstack/react-query"]` collapses them.

**Step 3: If two graphs are still present — STOP and re-open the analysis.** Do not patch around
it; the preset is supposed to have fixed this and a surviving split means the preset is wrong for
this app, which is a different (and larger) bug than the bead describes. Note the finding on the
bead and leave it open.

**Step 4: If one graph — close**

```bash
bd note Wallow-uc2c "Fixed by 5fdc6ae0 (packages/config Vite presets). minimal-app now spreads wallowAppConfig(), which carries ssr.noExternal: ['@tanstack/react-router-ssr-query', '@tanstack/react-query']. Verified against a fresh build: one react-query graph in .output/server."
bd close Wallow-uc2c
```

---

### Task 0.4: Commit nothing, push the bead state

Phase 0 changes no files. Do not commit. `bd dolt push` happens once, in Phase 4.

---

## Phase 1 — Docs truth pass

### Task 1.1: Wallow-l77c — stop telling readers to build the SDK before typechecking

**Files:**

- Modify: `CLAUDE.md:88`
- Modify: `apps/CLAUDE.md:18`
- Modify: `apps/wallow-web/README.md:22`

**Context the edit has to preserve:** `packages/sdk/package.json`'s `exports` map resolves every
subpath (`.`, `./server`, `./server/passthrough`, `./query`) to `./src/*.ts` in-repo, so nothing
typechecks against `dist/`. But `pnpm check:exports` (publint + attw) **does** need `dist/`,
because those tools describe a published tarball. The replacement text must say that, not just
delete the line — a reader who removes the build entirely will break `pnpm check`.

**Step 1: Edit `CLAUDE.md`**

Replace:

```
pnpm --filter @bc-solutions-coder/sdk build   # build the SDK FIRST (apps typecheck against dist/)
```

with:

```
pnpm --filter @bc-solutions-coder/sdk build   # only for check:exports — in-repo everything resolves from src/
```

**Step 2: Edit `apps/CLAUDE.md`**

Replace:

```
**Build the SDK before touching an app** — apps typecheck against `packages/sdk/dist/`:
`pnpm --filter @bc-solutions-coder/sdk build`.
```

with:

```
**You do not need to build the SDK to work on an app.** `packages/sdk`'s `exports` map resolves
every subpath to `src/*.ts` in-repo, so an app typechecks against SDK source and there is no
stale-`dist/` failure mode. `pnpm --filter @bc-solutions-coder/sdk build` is still needed before
`pnpm check:exports`, which runs publint and attw over the built package.
```

**Step 3: Edit `apps/wallow-web/README.md`**

Replace line 22:

```
Build the SDK first — the app typechecks against its `dist/`.
```

with:

```
The app typechecks against SDK **source** — `packages/sdk`'s `exports` map resolves every subpath
to `src/*.ts` in-repo. Build the SDK only before `pnpm check:exports`.
```

**Step 4: Verify no fourth copy of the claim exists**

```bash
cd /Users/traveler/Repos/Wallow
grep -rn "typecheck against dist\|typechecks against .*dist\|build the SDK FIRST\|SDK FIRST" \
  --include='*.md' . | grep -v node_modules
```

Expected: no output. If this returns a hit, a copy was missed — edit it, do not improvise around it.

**Step 5: Commit**

```bash
git add CLAUDE.md apps/CLAUDE.md apps/wallow-web/README.md
git commit -m "docs: stop claiming apps typecheck against packages/sdk/dist"
bd close Wallow-l77c
```

---

### Task 1.2: Wallow-75pg — delete the phantom contracts

Three phantom modules, not one. Billing is the one the bead names; Metering and Messaging are the
same defect and were found by the Step 3 sweep. Fix all three in this task — leaving two behind
means Step 3 does not come back clean and the acceptance criterion is not met.

**Files:**

- Modify: `api/src/Shared/README.md` (lines 26, 53, 59, 65, 66)
- Modify: `api/src/Modules/Notifications/README.md` (lines 60, 73, 74, 77)

**Step 1: Prove each named type does not exist**

```bash
cd /Users/traveler/Repos/Wallow
for t in IInvoiceQueryService ISubscriptionQueryService IRevenueReportService \
         InvoiceCreatedEvent InvoicePaidEvent InvoiceOverdueEvent PaymentReceivedEvent InvoiceId \
         IMeteringQueryService IUsageReportService QuotaThresholdReachedEvent UsageFlushedEvent \
         MessageSentEvent PasswordResetEvent; do
  printf '%-28s %s\n' "$t" "$(grep -rl "$t" api/src --include='*.cs' | wc -l | tr -d ' ')"
done
```

Expected: `0` for every one. If any is non-zero, keep that entry and fix only the rest.

Note the loop's exit status follows its last `grep`, so do **not** chain this with `&&`.

**Step 2: Make the edits**

- `api/src/Shared/README.md:26` — the strongly-typed-ID example reads
  `` (e.g., `InvoiceId`, `TenantId`) ``. Replace `InvoiceId` with a real one. Confirm the
  replacement exists first:
  ```bash
  grep -rn "record struct \|readonly record struct " api/src/Shared --include='*.cs' | grep -i "id" | head
  ```
  Use whatever that returns (`TenantId` is already there and is real); if only one real ID type
  exists, drop the "e.g." to a single example rather than inventing a second.
- `api/src/Shared/README.md:53` — delete the whole `**Billing**:` line.
- `api/src/Shared/README.md:59` — delete the whole `**Metering**: QuotaThresholdReachedEvent,
  UsageFlushedEvent.` line.
- `api/src/Shared/README.md:65` — delete the whole `- IInvoiceQueryService, ...` bullet.
- `api/src/Shared/README.md:66` — delete the whole `- IMeteringQueryService, IUsageReportService
  (Metering)` bullet.
- `api/src/Modules/Notifications/README.md:74` — delete the whole `| Billing | ... |` table row.
- `api/src/Modules/Notifications/README.md:77` — delete the whole `| Messaging | MessageSentEvent |`
  table row.
- `api/src/Modules/Notifications/README.md:73` — the Identity row names `PasswordResetEvent`. This is
  a **wrong name**, not a phantom module: the real contract is
  `api/src/Shared/Wallow.Shared.Contracts/Identity/Events/PasswordResetRequestedEvent.cs`, and
  `Shared/README.md:51` already has it right. Rename it; do not delete the row.
- `api/src/Modules/Notifications/README.md:60` — `NotificationType` lists `BillingInvoice`. This one
  is different: verify against the real enum before touching it.
  ```bash
  grep -rn -A 20 "enum NotificationType" api/src --include='*.cs'
  ```
  The enum has 9 members and carries the tombstone at
  `api/src/Modules/Notifications/Wallow.Notifications.Domain/Enums/NotificationType.cs:9`
  (`// 4 was BillingInvoice (removed)`). Make the README row match the enum exactly — add missing
  members, drop members that do not exist. Leave the tombstone comment in the C# alone; it is doing
  its job.

**Step 3: Satisfy the bead's acceptance criterion mechanically**

The bead's criterion is "every type named in those two READMEs resolves to real code, verified by
grep", and its closing note is "a README that is wrong once is usually wrong twice". So sweep every
backticked PascalCase identifier in both files, not just the billing ones:

```bash
cd /Users/traveler/Repos/Wallow
for f in api/src/Shared/README.md api/src/Modules/Notifications/README.md; do
  echo "=== $f ==="
  grep -o '`[A-Z][A-Za-z0-9_<>,. ]*`' "$f" \
    | tr -d '`' | sed 's/<.*//' | tr -d ' ' | sort -u \
    | while read -r sym; do
        [ -z "$sym" ] && continue
        n=$(grep -rl "\b$sym\b" api/src --include='*.cs' 2>/dev/null | wc -l | tr -d ' ')
        [ "$n" = "0" ] && echo "  MISSING: $sym"
      done
done
```

Expected after the edits: no `MISSING:` lines. Every hit that remains is a real phantom — delete or
correct it the same way. (Some hits will be framework types like `ILogger` or `Guid`; those resolve
in `api/src` too because they are used there. A `MISSING:` line is genuinely absent.)

**Step 4: Commit**

```bash
git add api/src/Shared/README.md api/src/Modules/Notifications/README.md
git commit -m "docs(api): delete phantom billing contracts from module readmes"
bd close Wallow-75pg
```

No .NET build or `dotnet format` is needed — no `.cs` file changed.

---

## Phase 2 — Wallow-luni: remove the second React from the server bundle

### Task 2.1: Capture the current failure

**Files:** none.

**Step 1: Build wallow-web and count the offending requires**

```bash
cd /Users/traveler/Repos/Wallow
pnpm --filter @bc-solutions-coder/wallow-web build
grep -rho '__require("[^"]*")' apps/wallow-web/.output/server | sort | uniq -c | sort -rn
```

Expected **before** the fix: `2 __require("react")` alongside a handful of Node builtins
(`events`, `util`, `crypto`, `url`, `tls`, `string_decoder`, `stream`, `net`, `async_hooks`). The
builtins are correct and must stay. `__require("react")` is the entire bug.

Record the exact count — it is the acceptance test.

---

### Task 2.2: Probe the one-line config fix BEFORE writing any code

**Files:**

- Modify (temporarily): `packages/config/src/vite/app.ts`

The remaining tasks add a permanent runtime module to a package that has never held one, plus an
optional React peer dependency. That is a real cost, and it is only worth paying if the boundary
cannot be moved with a config line. It probably can: the same CJS module compiles clean in the
Nitro-bundled `_libs` chunks, where react is in-graph.

**Do this task first. If it succeeds, Tasks 2.3 and 2.4 are unnecessary** — go straight to 2.5.

**Step 1: Hand the module to Nitro instead of Vite**

In the existing `ssr` block, add:

```ts
      // Vite's SSR pass leaves `react` external, and rolldown cannot lower a CJS
      // `require()` of an external module into a static import — so a CJS module
      // needing react emits `createRequire` instead (Wallow-luni). Handing this
      // one to Nitro's pass, where react is in-graph, is the whole fix.
      external: ["use-sync-external-store"],
```

Do not merge it into an existing `external` array without reading what is there — this key may not
exist yet in `wallowAppConfig()`.

**Step 2: Rebuild and count**

```bash
cd /Users/traveler/Repos/Wallow
pnpm --filter @bc-solutions-coder/wallow-web build
grep -rho '__require("[^"]*")' apps/wallow-web/.output/server | sort | uniq -c | sort -rn
```

**If `__require("react")` is gone and only Node builtins remain:** run Task 2.5's verification in
full, keep this change, and skip Tasks 2.3 and 2.4 entirely. Adjust Task 2.6's documentation to
describe the `ssr.external` entry rather than a shim, and drop the `packages/config` peer-dependency
paragraph — there is no runtime file and no React dependency. Commit as
`fix(config): hand use-sync-external-store to nitro so react stays single`.

**If it fails** — the require survives, or the build breaks, or the client bundle regresses —
**revert this edit completely** (`git checkout packages/config/src/vite/app.ts`) and continue to
Task 2.3. Record what actually happened in the bd note; "we tried it and it did not work" with no
detail is what makes the next person try it again.

**Step 3: The second candidate, only if Step 2 failed**

`ssr.noExternal: ["react"]` pulls react into the Vite graph instead. It is riskier — it can yield a
Vite-bundled react **plus** Nitro's react for the still-external `@tanstack/react-router` chunks,
which is the same bug wearing a different hat. Probe it the same way, and check the `_libs` chunks
for a second `require_react` region before believing a clean grep.

Neither candidate has been tested against this app. The evidence they rest on is the existing
`.output` artifact, not a rebuild — which is exactly why this is a probe and not an assumption.

---

### Task 2.3: Write the ESM `with-selector` shim

**Files:**

- Create: `packages/config/src/vite/shims/use-sync-external-store-with-selector.ts`

**Why here, and what it costs:** `packages/config/CLAUDE.md` says this package is never built,
never published, and depends on `vite` only. This file amends that in exactly one way — it is the
first module here that is *runtime* code. It is still never **imported** by anything in this
package (Vite resolves it by absolute path from the alias table), so the package's
"no relative imports between modules" rule is untouched, and it stays out of every `packages/*`
library build. The alternative — a new one-file package — buys nothing and separates the alias from
its target. Amend the CLAUDE.md in Task 2.6 rather than leaving the contradiction.

**Step 1: Write the module**

This is React's own `useSyncExternalStoreWithSelector`, transcribed to ESM. React does not export
it (only `useSyncExternalStore`), which is why an alias to `react` cannot work for this subpath.

```ts
/**
 * `useSyncExternalStoreWithSelector`, as ESM.
 *
 * NOT imported by anything in this package — `wallowAppConfig()` points a Vite
 * `resolve.alias` entry at this file's absolute path, so it is compiled into each
 * app's graph and never into a `packages/*` library build.
 *
 * It exists because the upstream module cannot be aliased away. React ships
 * `useSyncExternalStore` but no `…WithSelector`, so unlike
 * `use-sync-external-store/shim` this subpath cannot be pointed at `react`. The
 * published entry is CJS, and Vite's SSR pass leaves `react` EXTERNAL — rolldown
 * cannot lower a CJS `require()` of an external module into a static import, so
 * it emits `createRequire` and the built server loads a SECOND React from
 * `node_modules` (Wallow-luni). Being ESM is what takes the module off that path.
 *
 * The `process.env.NODE_ENV` conditional in the upstream file is NOT the cause:
 * rolldown resolves it statically and only the production branch ships. Nor is
 * CJS-ness alone — the same module compiles clean into the Nitro `_libs` chunks,
 * where react is in-graph.
 *
 * Three packages reach it — `@base-ui/utils/store/useStore`,
 * `@tanstack/react-store` and `zustand/traditional` — which is most of the
 * catalog, the router's store and the UI-only stores respectively. They are split
 * on import shape, so this module exports BOTH a named binding and a default.
 *
 * Verified by building an app and asserting `.output/server` holds no
 * `__require("react")` — vitest never builds the Nitro bundle, so there is no
 * spec for this and a regression surfaces as a hydration failure.
 */
import { useDebugValue, useEffect, useMemo, useRef, useSyncExternalStore } from "react";

/** The mutable cell holding the last selection, so `isEqual` has a previous value to compare. */
interface Inst<Selection> {
  hasValue: boolean;
  value: Selection | null;
}

/**
 * Subscribe to an external store, projecting each snapshot through `selector` and
 * re-rendering only when `isEqual` says the projection changed.
 */
export function useSyncExternalStoreWithSelector<Snapshot, Selection>(
  subscribe: (onStoreChange: () => void) => () => void,
  getSnapshot: () => Snapshot,
  getServerSnapshot: undefined | (() => Snapshot),
  selector: (snapshot: Snapshot) => Selection,
  isEqual?: (a: Selection, b: Selection) => boolean,
): Selection {
  const instRef = useRef<Inst<Selection> | null>(null);
  let inst: Inst<Selection>;
  if (instRef.current === null) {
    inst = { hasValue: false, value: null };
    instRef.current = inst;
  } else {
    inst = instRef.current;
  }

  const [getSelection, getServerSelection] = useMemo((): [
    () => Selection,
    undefined | (() => Selection),
  ] => {
    // Closed over by both getters below: the selector must be memoised across
    // calls, not just across renders, or `useSyncExternalStore` sees a new value
    // every time it checks and loops.
    let hasMemo = false;
    let memoizedSnapshot: Snapshot;
    let memoizedSelection: Selection;

    const memoizedSelector = (nextSnapshot: Snapshot): Selection => {
      if (!hasMemo) {
        hasMemo = true;
        memoizedSnapshot = nextSnapshot;
        const firstSelection: Selection = selector(nextSnapshot);
        if (isEqual !== undefined && inst.hasValue) {
          const currentSelection: Selection = inst.value as Selection;
          if (isEqual(currentSelection, firstSelection)) {
            memoizedSelection = currentSelection;
            return currentSelection;
          }
        }
        memoizedSelection = firstSelection;
        return firstSelection;
      }

      const prevSnapshot: Snapshot = memoizedSnapshot;
      const prevSelection: Selection = memoizedSelection;
      if (Object.is(prevSnapshot, nextSnapshot)) {
        return prevSelection;
      }

      const nextSelection: Selection = selector(nextSnapshot);
      if (isEqual !== undefined && isEqual(prevSelection, nextSelection)) {
        memoizedSnapshot = nextSnapshot;
        return prevSelection;
      }

      memoizedSnapshot = nextSnapshot;
      memoizedSelection = nextSelection;
      return nextSelection;
    };

    const serverSnapshot: undefined | (() => Snapshot) = getServerSnapshot;

    return [
      (): Selection => memoizedSelector(getSnapshot()),
      serverSnapshot === undefined ? undefined : (): Selection => memoizedSelector(serverSnapshot()),
    ];
  }, [getSnapshot, getServerSnapshot, selector, isEqual, inst]);

  const value: Selection = useSyncExternalStore(subscribe, getSelection, getServerSelection);

  useEffect(() => {
    inst.hasValue = true;
    inst.value = value;
  }, [inst, value]);

  useDebugValue(value);

  return value;
}

/**
 * The same function as a default export.
 *
 * Not redundant — the consumers are genuinely split. `@base-ui/utils` and both
 * `@tanstack/react-store` versions take the NAMED binding; `zustand/esm/traditional.mjs`
 * does `import useSyncExternalStoreExports from
 * "use-sync-external-store/shim/with-selector.js"` and destructures. Export only the
 * named one and rolldown aborts the build with
 * `[MISSING_EXPORT] "default" is not exported`.
 */
export default { useSyncExternalStoreWithSelector };
```

**Step 1a: Prove both shapes resolve**

Do not take Step 1 on trust — the default export is the single most likely thing to be dropped as
"redundant", and dropping it breaks the build rather than the behaviour. Before touching the alias:

```bash
cd /Users/traveler/Repos/Wallow
grep -rn "use-sync-external-store" \
  node_modules/.pnpm/zustand@*/node_modules/zustand/esm/traditional.mjs \
  node_modules/.pnpm/@base-ui+utils@*/node_modules/@base-ui/utils/store/useStore.mjs
```

Expected: zustand's line is a **default** import of `.../with-selector.js`; Base UI's is a
**named** import of `.../with-selector`. Both must work against one module.

**Step 2: Give `packages/config` the React types it now needs**

`packages/config`'s gate is `tsc --noEmit` over `src`, and this file imports `react`. Without the
types the gate fails with `Cannot find module 'react'` — pnpm's strict `node_modules` gives a
package only what it declares.

**Leave `dependencies` alone.** `vite` stays exactly where it is; the three keys below are
ADDITIONS to `packages/config/package.json`, not a replacement for its dependency block:

```json
  "peerDependencies": {
    "react": "catalog:react"
  },
  "peerDependenciesMeta": {
    "react": { "optional": true }
  },
  "devDependencies": {
    "@types/node": "^24.0.0",
    "@types/react": "^19.2.17",
    "typescript": "catalog:tooling"
  }
```

**`@types/react` is a literal, not `catalog:react`.** The `react` catalog holds `react`,
`react-dom`, `@tanstack/react-form`, `@tanstack/react-query` and `zustand` — there is no
`@types/react` key, and `catalog:react` for it fails `pnpm install`. `packages/ui/package.json:60`
spells the same literal `^19.2.17`; copy that. Confirm before editing:

```bash
grep -n '"react"\|"@types/react"' packages/ui/package.json
sed -n '/^  react:/,/^  [a-z-]*:/p' pnpm-workspace.yaml
```

`react` is an **optional peer** because the shim is only ever pulled into a graph that already has
React; `packages/config` itself must never install one. `@types/react` is a devDependency because
the types are needed to typecheck this package and by nothing that consumes it.

```bash
pnpm install
```

---

### Task 2.4: Point the alias at it

**Files:**

- Modify: `packages/config/src/vite/app.ts`

**Step 1: Add the alias entry**

Immediately after the existing two `use-sync-external-store` entries, add a third — one entry, whose
regex covers both the shim subpath every consumer here actually imports and the root subpath none
of them do:

```ts
        // `…/shim/with-selector` needs a DIFFERENT target: React exports
        // `useSyncExternalStore` but no `…WithSelector`, so `react` is not a
        // valid replacement for this subpath. The shipped file is CJS, and Vite's
        // SSR pass leaves `react` external — rolldown cannot lower a CJS
        // `require()` of an external module into a static import, so it emits a
        // runtime `__require("react")` that loads a SECOND React beside the
        // bundled one (Wallow-luni). Pointing the specifier at a local ESM
        // transcription takes it off that path.
        //
        // Reached by `@base-ui/utils/store/useStore`, `@tanstack/react-store` and
        // `zustand/traditional` — the catalog, the router's store, the UI stores.
        // The `(\.js)?` group is load-bearing: zustand imports the `.js` form.
        //
        // The ROOT `use-sync-external-store/with-selector` subpath is the same CJS
        // shape and would recur the same way. Nothing in this tree imports it
        // today — the `require_with_selector` in the `_libs` chunks is the SHIM
        // subpath, hoisted — so it is covered here as cheap insurance, not
        // because a consumer needs it.
        {
          find: /^use-sync-external-store\/(shim\/)?with-selector(\.js)?$/u,
          replacement: WITH_SELECTOR_SHIM,
        },
```

**Check the regexes stay disjoint.** The two existing entries are
`/^use-sync-external-store\/shim$/u` and `/^use-sync-external-store\/shim\/index\.js$/u`; the one
above matches neither, so all three are mutually exclusive and order-independent. Both existing
entries are correct and load-bearing — `with-selector.production.js` does
`require("use-sync-external-store/shim")` and reads `useSyncExternalStore` off it, which is why
the current output reads `var shim = __require("react")`. Do not remove them.

**Step 2: Resolve the absolute path at the top of the file**

Above `AppConfigOptions`, add:

```ts
/**
 * Absolute path to the local ESM `with-selector`, resolved from this module's own
 * URL rather than written as a relative specifier: it is an alias TARGET fed to
 * Vite, never an import, so the package's "no relative imports" rule (which
 * exists because plain Node ESM loads this file) does not apply and cannot be
 * tripped.
 */
const WITH_SELECTOR_SHIM: string = fileURLToPath(
  new URL("./shims/use-sync-external-store-with-selector.ts", import.meta.url),
);
```

and at the top of the imports:

```ts
import { fileURLToPath } from "node:url";
```

**Step 3: Amend the existing comment that says the subpath is fine**

The current block comment ends with:

```
        // nonexistent `react/with-selector`. That subpath keeps its own
        // implementation (React ships no `useSyncExternalStoreWithSelector`) and
        // reads `useSyncExternalStore` off whatever this alias resolves to.
```

Replace the last sentence — it is now wrong, because the subpath no longer keeps its own
implementation:

```
        // nonexistent `react/with-selector`. That subpath is handled separately
        // below.
```

---

### Task 2.5: Verify the require is gone

**Step 1: Rebuild and re-count**

```bash
cd /Users/traveler/Repos/Wallow
pnpm --filter @bc-solutions-coder/wallow-web build
grep -rho '__require("[^"]*")' apps/wallow-web/.output/server | sort | uniq -c | sort -rn
```

Expected: `__require("react")` **absent**. The Node builtins are unchanged. If any other specifier
appeared, stop — the alias matched something it should not have.

**Step 1a: Count the Reacts, not just the requires**

A clean grep proves the *symptom* gone, not the *goal* met. The goal is one React in the server
bundle, and there are two ways to reach zero `__require("react")` while still shipping two — pulling
react into Vite's graph beside Nitro's copy (the failure mode Task 2.2 Step 3 warns about), or
aliasing the specifier somewhere that re-inlines React's source.

```bash
grep -rho 'react@[0-9][^/]*/node_modules/react/cjs/react.production.js' \
  apps/wallow-web/.output/server | sort | uniq -c
```

Expected: **exactly one** line, count `1` — today that region lives in
`_libs/@tanstack/react-form+[...].mjs`. Two lines, or one line with a count above 1, means a second
copy is bundled and the bead is not fixed regardless of what Step 1 printed. Run this before and
after the change; the pre-change baseline is also `1`, which is the point — the require was loading
a copy that never appeared in the bundle at all.

**Step 1b: Load a page from the dev server**

Nothing else in this phase exercises Vite's dev dependency-optimizer, which resolves aliases on a
different path from the build. An alias that satisfies rolldown and breaks `vite dev` would ship.

```bash
cd apps/wallow-web && PORT=3010 pnpm dev &
sleep 8
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:3010/bff-demo
kill %1
```

Expected: `200`. `/bff-demo` is the one route that needs no backend (see `.claude/rules/E2E.md`).
A 500 here with a `Failed to resolve import` or `does not provide an export named` message is the
alias failing in dev only.

**Step 2: Repeat for the other two apps**

```bash
pnpm --filter @bc-solutions-coder/wallow-auth build
pnpm --filter @bc-solutions-coder/minimal-app build
grep -rho '__require("[^"]*")' apps/wallow-auth/.output/server apps/examples/minimal-app/.output/server \
  | sort | uniq -c | sort -rn
```

Expected: no `react`.

**Step 3: Boot the built server and confirm it still renders**

This is the assertion that matters — the bead's stated risk is an SSR hydration collapse, which a
grep cannot see.

```bash
cd apps/wallow-auth && PORT=3002 node .output/server/index.mjs &
sleep 3
curl -s http://localhost:3002/login | wc -c
kill %1
```

Expected: a body length in the ~9,000–10,000 char range (the `app.ts` docblock records 9895 chars
as the healthy figure for `/login` and 2621 as the collapsed one). A number near 2,600 means SSR is
still falling back to client-only rendering and the fix did not take.

**Step 4: Run the JS gate**

```bash
cd /Users/traveler/Repos/Wallow
pnpm typecheck && pnpm lint && pnpm test
```

---

### Task 2.6: Document and commit

**Files:**

- Modify: `packages/config/CLAUDE.md`

**If Task 2.2 succeeded, do Step 1a instead of Step 1**, then go to Step 2 with the commit message
2.2 names.

**Step 1a (Task 2.2 branch): document the config line, not a shim**

No runtime file was added and no React peer dependency exists, so `packages/config/CLAUDE.md`'s two
standing claims — never built, nothing to contain — remain true as written and need no amendment.
Add only a short note under the Vite-preset section recording *why* the `ssr.external` entry is
there, because a one-word array is otherwise indistinguishable from cargo cult:

````markdown
## Why `use-sync-external-store` is `ssr.external`

Vite's SSR pass leaves `react` external, and rolldown cannot lower a CJS `require()` of an
**external** module into a static import — it emits `createRequire` instead. `use-sync-external-store`
is CJS and needs react, so left in Vite's graph it ships a runtime `__require("react")` that loads a
SECOND React beside the bundled one, and SSR collapses to a near-empty document (Wallow-luni).
Handing the package to Nitro's pass, where react is in-graph, compiles it clean.

Verify with a build, not a spec — vitest never produces the Nitro bundle:

```bash
pnpm --filter @bc-solutions-coder/wallow-web build
grep -rho '__require("[^"]*")' apps/wallow-web/.output/server | sort | uniq -c
```

Every remaining `__require` must name a Node builtin.
````

**Step 1: Amend the two claims this phase contradicts**

Under "This package is never built and never published", the bullet "Nothing imports it at runtime,
so there is nothing for a bundle to contain" is now half-true. Add a short subsection:

````markdown
## One exception: `src/vite/shims/`

`src/vite/shims/use-sync-external-store-with-selector.ts` **is** runtime code, and it is the only
file here that is. It is never imported by this package — `wallowAppConfig()` points a
`resolve.alias` entry at its absolute path, so Vite compiles it into each APP's graph and it never
reaches a `packages/*` library build. That is what keeps the two rules above intact: no relative
import between modules here, and nothing for this package's own (nonexistent) bundle to contain.

It exists because `use-sync-external-store/shim/with-selector` cannot be aliased to `react` the way
its sibling can — React ships no `useSyncExternalStoreWithSelector`. The published file is CJS, and
Vite's SSR pass leaves `react` external: rolldown cannot lower a CJS `require()` of an external
module into a static import, so it emits a runtime `__require("react")` that loads a SECOND React
beside the bundled one and collapses SSR to a near-empty document (Wallow-luni). `react` is
therefore an OPTIONAL peer dependency of this package: the shim is only ever pulled into a graph
that already has React, and this package must never install one.

Verify it with a build, not a spec — vitest never produces the Nitro bundle:

```bash
pnpm --filter @bc-solutions-coder/wallow-web build
grep -rho '__require("[^"]*")' apps/wallow-web/.output/server | sort | uniq -c
```

Every remaining `__require` must name a Node builtin.
````

**Step 2: Commit**

```bash
git add packages/config pnpm-lock.yaml
git commit -m "fix(config): alias the CJS with-selector shim to a local ESM module"
bd close Wallow-luni
```

**Step 3: File the follow-up**

There is no automated guard for this. File it rather than gold-plating the plan:

```bash
bd create --type task --priority 3 \
  "ci: assert .output/server holds no non-builtin __require after each app build" \
  -d "Wallow-luni was invisible to every gate: vitest never builds the Nitro bundle, so a second-React regression surfaces as an empty SSR document rather than a red test. The three app builds already run in CI; add a step that greps each .output/server for __require(\"...\") and fails on any specifier that is not a Node builtin. Same shape as the manual command in packages/config/CLAUDE.md."
```

---

## Phase 3 — Wallow-tvn3: make the client address proxy-aware — **SUPERSEDED**

> **Do not implement this phase.** `Wallow-tvn3` landed 2026-08-27 on the CIDR + right-to-left
> walk design in `docs/plans/2026-08-03/1639-proxy-trust-react-dupe-nav-flake.md` §1, not on the
> `WALLOW_TRUSTED_PROXY_HOPS` counted-position design below. A hop count has to be raised in
> lockstep with the real number of terminators and UNDER-limits when set too high — a client can
> pad the chain to move the counted position into the part it writes — which is the failure mode
> the long comments below spend most of their length warning about. Naming the trusted peers and
> walking in from the right removes the setting that could be wrong. What this phase DID
> contribute, and what shipped: the finding that `x-wallow-client-ip` is an ordinary inbound
> header a caller can forge, so the stamping sites must DELETE it when there is no peer.
>
> The rest of this plan is unaffected — Phase 2 (`Wallow-luni`) is still open.


### The design, decided

**Two gates, both required: a peer allowlist, then a hop index.** They answer different
questions, and either one alone is a hole.

- `WALLOW_TRUSTED_PROXIES` (default empty) — a comma-separated list of CIDRs. `X-Forwarded-For`
  is read **only** when the socket peer matches one. This answers *may this caller write the
  header at all*.
- `WALLOW_TRUSTED_PROXY_HOPS` (default `0`) — how many places in from the **right** of the chain
  the real client sits. Applied only after the peer check passes. This answers *which entry do I
  read*.

Both defaulting to "trust nothing" means the resolver returns the peer address — exactly today's
behaviour, so a fork that configures nothing is not made worse.

**Why the earlier hop-count-only design was wrong.** Hop count never answers the first question.
`docker-compose.production.yml` binds each app to `127.0.0.1` *and* attaches it to the shared
`wallow` bridge — the file's own comment says "the caddy ingress reaches this over the wallow
network". So anything on that network reaches Node without traversing Caddy, and under
hop-count-only trust `curl -H 'X-Forwarded-For: 1.2.3.4' http://wallow-web:8080/bff/logs` yields
an attacker-chosen rate-limit key and an attacker-chosen `clientIp` on every log record. Today
`request.ip` is the socket peer and that attack does not exist. **Hop count alone makes this
strictly worse than doing nothing.**

The reason given for skipping the allowlist — that the only workable CIDR is `172.16.0.0/12`,
which trusts every container — was not forced. Compose supports a dedicated `ipam` subnet, which
makes a tight allowlist available. Task 3.6 adds one.

**A too-high hop count under-limits; it does not over-limit.** Too *low* over-limits. Too high
lets the attacker pad rather than shorten: send `X-Forwarded-For: A, B`, the proxy appends the
peer giving `A, B, peer`, and `at(-2)` returns attacker-controlled `B`. Any docblock or CLAUDE.md
text claiming every uncertain case over-limits is false and must not be written.

**Caddy replaces `X-Forwarded-For`; it does not append** — unless `trusted_proxies` is set, and it
is commented out at `docker/caddy/Caddyfile.example:44-46`. Two consequences:

- `hops=1` works behind the reference stack because Caddy **discarded** the forgery, not because
  "forgeries stay to the left". That safety property evaporates the moment traffic arrives another
  way, which is defect 1 above.
- `hops=2` (Cloudflare → Caddy → app) is correct **only** if Caddy's `trusted_proxies` is
  configured so Caddy appends. Unconfigured, Caddy emits a single-entry chain, `at(-2)` is
  `undefined`, everything falls back to the peer, and all traffic shares one Caddy bucket.

**srvx already has a trust-proxy seam, reading the opposite end.** With `trustProxy` set,
`request.ip` becomes `firstForwardedValue(...)` — the **leftmost**, most attacker-controlled entry
(`srvx/dist/adapters/node.mjs:285`). Nothing in this repo sets it (grep-verified: zero hits across
`apps/` and `packages/`), which is the only reason `request.ip` is the peer and the fallback is
safe. The resolver's spec must pin that assumption rather than assume it silently.

**`normalize` must validate IP shape, not just length.** A length cap alone accepts `unknown`,
`_obfuscated` (both legal RFC 7239 values) and arbitrary attacker text as rate-limit keys and log
fields. And `/^\[(.*)]$/u` matches only a *wholly* bracketed value, so `[2001:db8::1]:443` passes
through with its port — and since the port varies per connection, every request gets its own
bucket and the limit never applies at all. Strip the port; reject anything that is not an IP.

**The API needs no change, but not for the reason first given.**
`api/src/Wallow.Api/Program.cs:398-407` calls `UseForwardedHeaders` with
`KnownIPNetworks`/`KnownProxies` cleared and the default `ForwardLimit` of 1, so it reads the
**rightmost** entry. Clearing those collections is the documented *enabling* pattern, not a no-op —
the middleware skips the known-address check entirely, and the same docs warn this permits
spoofing. It is safe only because the app tier is not directly reachable, which is the assumption
the compose topology breaks. The `!IsDevelopment()` gate breaks nothing: rate limiting is itself
only registered when `!IsDevelopment() && !IsEnvironment("Testing")` (`Program.cs:367`).

Do **not** claim the API "gets correct per-client limiting for free". Its limiter partitions on
**TenantId first** (`ServiceCollectionExtensions.cs:183-191`), with `RemoteIpAddress` only as a
fallback; the `developer-app-registration` policy partitions on userId first (`:177-181`). Client
IP is not the key for authenticated traffic at all.

**A separate real bug this phase must fix.** `bff.server.ts:151-154`,
`api-passthrough.server.ts:79-82` and `minimal-app/src/lib/api-passthrough.ts:53-55` only *set*
`CLIENT_IP_HEADER`; they never delete it. The header is on the proxy's `FORWARDED_REQUEST_HEADERS`
allowlist (`packages/sdk/src/server/proxy.ts:522`) and copied verbatim from the inbound request
(`:775-780`), then appended to `X-Forwarded-For` by `applyForwardedHeaders`. So when `request.ip`
is unavailable — which the logger's own docblock notes happens — an inbound forged
`x-wallow-client-ip` survives to the API. Fix: `delete` unconditionally before conditionally
setting.

**Chain shape after the fix:** the passthrough still *appends* rather than replaces, so the
outgoing chain reads `evil, 203.0.113.7, 203.0.113.7` when a client forges a prefix. The API takes
the rightmost and moves it to `X-Original-For`, so no C# code sees the duplicate and Serilog logs
no XFF at all. Leave the SDK's append behaviour alone; the cost is only that chain length stops
equalling hop count, which will bite whoever next tunes `WALLOW_TRUSTED_PROXY_HOPS`.

---

### Task 3.1: Write the failing spec for the resolver

**Files:**

- Create: `packages/env/src/client-address.test.ts`

**Step 1: Write it**

```ts
import { describe, expect, it } from "vitest";

import {
  resolveClientAddress,
  resolveTrustedProxies,
  resolveTrustedProxyHops,
  TRUSTED_PROXIES_ENV_KEY,
  TRUSTED_PROXY_HOPS_ENV_KEY,
} from "./client-address";

/**
 * The address a rate limit is keyed on and a log record is stamped with.
 *
 * Two gates, and either alone is a hole: the peer allowlist decides whether a
 * caller may write X-Forwarded-For at all, the hop index decides which entry to
 * read. A too-LOW hop count over-limits; a too-HIGH one under-limits, because a
 * client pads the chain to push its own value into the counted position.
 */

const CADDY = "172.28.0.2";
const OUTSIDE = "172.28.9.9";
const TRUSTED: readonly string[] = ["172.28.0.2/32"];

function requestWith(forwardedFor?: string): Request {
  return new Request("https://wallow.dev/logs", {
    method: "POST",
    ...(forwardedFor === undefined ? {} : { headers: { "x-forwarded-for": forwardedFor } }),
  });
}

function resolve(
  forwardedFor: string | undefined,
  peer: string | undefined,
  hops: number,
  trusted: readonly string[] = TRUSTED,
): string | undefined {
  return resolveClientAddress(requestWith(forwardedFor), peer, {
    trustedProxies: resolveTrustedProxies({ [TRUSTED_PROXIES_ENV_KEY]: trusted.join(",") }),
    hops,
  });
}

describe("an untrusted peer", () => {
  it("cannot put its own value in the counted position", () => {
    expect(resolve("1.2.3.4", OUTSIDE, 1)).toBe(OUTSIDE);
  });

  it("is untrusted when no allowlist is configured, however many hops are declared", () => {
    expect(resolve("1.2.3.4", CADDY, 1, [])).toBe(CADDY);
  });

  it("cannot reach a limiter bucket it chose", () => {
    expect(resolve("1.2.3.4, 5.6.7.8", OUTSIDE, 2)).toBe(OUTSIDE);
  });
});

describe("a trusted peer", () => {
  it("takes the entry the hop count names", () => {
    expect(resolve("203.0.113.7", CADDY, 1)).toBe("203.0.113.7");
  });

  it("is unmoved by a forged prefix", () => {
    expect(resolve("1.1.1.1, 203.0.113.7", CADDY, 1)).toBe("203.0.113.7");
  });

  it("takes the second entry from the right at two hops", () => {
    expect(resolve("1.1.1.1, 203.0.113.7, 10.0.0.9", CADDY, 2)).toBe("203.0.113.7");
  });

  it("falls back to the peer when the chain is shorter than the hop count", () => {
    expect(resolve("203.0.113.7", CADDY, 2)).toBe(CADDY);
  });

  it("falls back to the peer when the header is absent", () => {
    expect(resolve(undefined, CADDY, 1)).toBe(CADDY);
  });

  it("tolerates the whitespace and empty entries a chain accumulates", () => {
    expect(resolve("1.1.1.1 ,, 203.0.113.7 ", CADDY, 1)).toBe("203.0.113.7");
  });

  it("reads an attacker-padded entry when the hop count is too high", () => {
    // Pinned deliberately: this is the failure a too-HIGH hop count produces, and
    // it is an UNDER-limit. Any doc claiming every misconfiguration over-limits is
    // contradicted here.
    expect(resolve("1.1.1.1, 9.9.9.9, 203.0.113.7", CADDY, 2)).toBe("9.9.9.9");
  });
});

describe("entry normalization", () => {
  it("unwraps a bracketed IPv6 entry", () => {
    expect(resolve("[2001:db8::1]", CADDY, 1)).toBe("2001:db8::1");
  });

  it("strips the port from a bracketed IPv6 entry", () => {
    // A port that varies per connection mints a fresh limiter bucket per request,
    // so a surviving port disables the limit rather than loosening it.
    expect(resolve("[2001:db8::1]:443", CADDY, 1)).toBe("2001:db8::1");
  });

  it("strips the port from an IPv4 entry", () => {
    expect(resolve("203.0.113.7:51234", CADDY, 1)).toBe("203.0.113.7");
  });

  it("keeps a bare IPv6 address whole", () => {
    expect(resolve("2001:db8::1", CADDY, 1)).toBe("2001:db8::1");
  });

  it.each(["unknown", "_obfuscated", "not an address", "x".repeat(200)])(
    "rejects %s rather than keying a limiter on it",
    (value: string) => {
      expect(resolve(value, CADDY, 1)).toBe(CADDY);
    },
  );

  it("answers undefined when the host supplies no peer", () => {
    expect(resolve("203.0.113.7", undefined, 0)).toBeUndefined();
  });

  it("reads an empty peer as no answer", () => {
    expect(resolve(undefined, "", 0)).toBeUndefined();
  });

  it("rejects a peer that is not an address", () => {
    expect(resolve(undefined, "localhost", 0)).toBeUndefined();
  });
});

describe("resolveTrustedProxies", () => {
  it("names the variable it reads", () => {
    expect(TRUSTED_PROXIES_ENV_KEY).toBe("WALLOW_TRUSTED_PROXIES");
  });

  it("defaults to trusting nothing", () => {
    expect(resolveTrustedProxies({})).toHaveLength(0);
  });

  it("parses a comma list, ignoring blanks and whitespace", () => {
    expect(
      resolveTrustedProxies({ [TRUSTED_PROXIES_ENV_KEY]: " 10.0.0.0/8 ,, 172.28.0.0/16 " }),
    ).toHaveLength(2);
  });

  it("drops a malformed entry rather than widening trust", () => {
    expect(
      resolveTrustedProxies({ [TRUSTED_PROXIES_ENV_KEY]: "10.0.0.0/99,nonsense,10.0.0.0/8" }),
    ).toHaveLength(1);
  });

  it("accepts a bare address as a host route", () => {
    expect(resolve("203.0.113.7", CADDY, 1, ["172.28.0.2"])).toBe("203.0.113.7");
  });

  it("matches inside an IPv4 range and not outside it", () => {
    expect(resolve("203.0.113.7", "172.28.0.9", 1, ["172.28.0.0/24"])).toBe("203.0.113.7");
    expect(resolve("203.0.113.7", "172.28.1.9", 1, ["172.28.0.0/24"])).toBe("172.28.1.9");
  });

  it("matches an IPv6 range", () => {
    expect(resolve("203.0.113.7", "2001:db8::5", 1, ["2001:db8::/32"])).toBe("203.0.113.7");
    expect(resolve("203.0.113.7", "2001:db9::5", 1, ["2001:db8::/32"])).toBe("2001:db9::5");
  });

  it("does not match an IPv4 peer against an IPv6 range", () => {
    expect(resolve("203.0.113.7", "172.28.0.2", 1, ["::/0"])).toBe("172.28.0.2");
  });
});

describe("resolveTrustedProxyHops", () => {
  it("names the variable it reads", () => {
    expect(TRUSTED_PROXY_HOPS_ENV_KEY).toBe("WALLOW_TRUSTED_PROXY_HOPS");
  });

  it("defaults to trusting nothing", () => {
    expect(resolveTrustedProxyHops({})).toBe(0);
  });

  it.each([
    ["1", 1],
    ["2", 2],
    [" 3 ", 3],
  ])("parses %s", (value: string, expected: number) => {
    expect(resolveTrustedProxyHops({ [TRUSTED_PROXY_HOPS_ENV_KEY]: value })).toBe(expected);
  });

  it.each(["", "-1", "1.5", "yes", "1e3", "Infinity"])(
    "reads %s as trusting nothing",
    (value: string) => {
      expect(resolveTrustedProxyHops({ [TRUSTED_PROXY_HOPS_ENV_KEY]: value })).toBe(0);
    },
  );
});
```

**Step 2: Run it and watch it fail**

```bash
cd /Users/traveler/Repos/Wallow
pnpm --filter @bc-solutions-coder/env test
```

Expected: `Failed to resolve import "./client-address"`.

---

### Task 3.2: Implement the resolver

**Files:**

- Create: `packages/env/src/client-address.ts`

**Charter constraints this file must satisfy** (`packages/env/src/charter.test.ts` enforces all of
them, so getting one wrong fails a test rather than review): no `import` statement of any kind, no
`process.env`, no `import.meta.env`.

**Step 1: Write it**

```ts
/**
 * The client address a rate limit is keyed on and a log record is stamped with,
 * behind however many reverse proxies the deployment puts in front of the app.
 *
 * `request.ip` — the only address a Node host actually knows — is the immediate
 * peer. Behind an ingress that is the ingress, so every real user shares one
 * bucket and every stamped `client.address` names the proxy.
 *
 * `X-Forwarded-For` carries the truth, but it is also the header any caller can
 * write, so it takes TWO gates and either alone is a hole:
 *
 * 1. WHO may write it — the socket peer must match {@link TRUSTED_PROXIES_ENV_KEY}.
 *    Hop counting alone cannot express this, and the apps are reachable without
 *    traversing the ingress (they sit on the shared compose network as well as
 *    loopback), so a caller reaching Node directly would otherwise choose its own
 *    limiter bucket and its own stamped address.
 * 2. WHICH entry to read — {@link TRUSTED_PROXY_HOPS_ENV_KEY} places from the
 *    RIGHT, since each hop appends the address it observed.
 *
 * Both default to trusting nothing, which answers with the peer — today's
 * behaviour, so an unconfigured fork is not made worse. Note the asymmetry: a
 * too-LOW hop count over-limits, but a too-HIGH one UNDER-limits, because a
 * client pads the chain to push its own value into the counted position. Do not
 * write that every misconfiguration fails safe; it does not.
 */

/** What a proxy names the client chain. Each hop appends; the rightmost is the nearest. */
const FORWARDED_FOR_HEADER = "x-forwarded-for";

/** The variable naming the peers whose `X-Forwarded-For` this deployment believes. */
export const TRUSTED_PROXIES_ENV_KEY = "WALLOW_TRUSTED_PROXIES";

/** The variable naming how many proxies this deployment puts in front of the app. */
export const TRUSTED_PROXY_HOPS_ENV_KEY = "WALLOW_TRUSTED_PROXY_HOPS";

/** A non-negative integer, and nothing else. */
const HOP_COUNT = /^\d+$/u;

/**
 * Ceiling on a trusted entry's length.
 *
 * The value becomes a key in the ingest limiter's map. `maxTrackedKeys` bounds
 * how MANY keys that map holds; nothing else bounds how long one is, and both are
 * caller-influenced. An IPv6 address with a zone fits in well under this.
 */
const MAX_ADDRESS_LENGTH = 64;

/** An `[::1]` or `[::1]:443` entry. Only a WHOLLY bracketed value is one. */
const BRACKETED = /^\[([^\]]*)](?::\d+)?$/u;

/** A dotted quad carrying a port, which an IPv6 address never looks like. */
const IPV4_WITH_PORT = /^\d{1,3}(?:\.\d{1,3}){3}:\d+$/u;

/** A dotted quad. Range-checked separately — the pattern cannot say `<= 255`. */
const IPV4 = /^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$/u;

/** One IPv6 group. */
const HEX_GROUP = /^[0-9a-f]{1,4}$/iu;

/** A CIDR, or a bare address treated as a host route. */
const CIDR = /^([^/]+)(?:\/(\d{1,3}))?$/u;

/** A parsed allowlist entry: the network bytes and how many leading bits matter. */
export interface TrustedProxy {
  readonly bytes: readonly number[];
  readonly prefix: number;
}

/** The four octets, or `undefined` when this is not a dotted quad. */
function parseIpv4(value: string): number[] | undefined {
  const match: RegExpMatchArray | null = value.match(IPV4);
  if (match === null) {
    return undefined;
  }

  const octets: number[] = [Number(match[1]), Number(match[2]), Number(match[3]), Number(match[4])];

  return octets.every((octet: number): boolean => octet <= 255) ? octets : undefined;
}

/** Append `groups` to `out` as bytes, allowing a trailing IPv4-mapped tail. */
function pushGroups(groups: readonly string[], out: number[]): boolean {
  for (let index = 0; index < groups.length; index += 1) {
    const group: string = groups[index] ?? "";

    if (index === groups.length - 1 && group.includes(".")) {
      const mapped: number[] | undefined = parseIpv4(group);
      if (mapped === undefined) {
        return false;
      }
      out.push(...mapped);
      continue;
    }

    if (!HEX_GROUP.test(group)) {
      return false;
    }
    const word: number = Number.parseInt(group, 16);
    out.push(word >>> 8, word & 0xff);
  }

  return true;
}

/** The sixteen bytes, or `undefined` when this is not an IPv6 address. */
function parseIpv6(value: string): number[] | undefined {
  const zoneless: string = value.split("%")[0] ?? "";
  if (!zoneless.includes(":")) {
    return undefined;
  }

  const halves: string[] = zoneless.split("::");
  if (halves.length > 2) {
    return undefined;
  }

  const head: string[] = (halves[0] ?? "") === "" ? [] : (halves[0] ?? "").split(":");
  const tail: string[] =
    halves.length === 2 && (halves[1] ?? "") !== "" ? (halves[1] ?? "").split(":") : [];

  const headBytes: number[] = [];
  const tailBytes: number[] = [];
  if (!pushGroups(head, headBytes) || !pushGroups(tail, tailBytes)) {
    return undefined;
  }

  const gap: number = 16 - headBytes.length - tailBytes.length;
  // A `::` must elide at least one group (two bytes); without it the halves fill.
  if (halves.length === 2 ? gap < 2 : gap !== 0) {
    return undefined;
  }

  return [...headBytes, ...new Array<number>(gap).fill(0), ...tailBytes];
}

/** An address's bytes — 4 for IPv4, 16 for IPv6 — or `undefined` if it is neither. */
function toBytes(value: string): number[] | undefined {
  return parseIpv4(value) ?? parseIpv6(value);
}

/**
 * An address, or `undefined` when the value is absent, oversized or not an IP.
 *
 * Shape validation is the point, not tidiness. `unknown` and `_obfuscated` are
 * both legal RFC 7239 values, and a length check alone would let either become a
 * rate-limit key. A surviving port is worse than a loose key: it varies per
 * connection, so every request lands in its own bucket and the limit never binds.
 */
function normalize(value: string | undefined): string | undefined {
  if (value === undefined) {
    return undefined;
  }

  const trimmed: string = value.trim();
  if (trimmed === "" || trimmed.length > MAX_ADDRESS_LENGTH) {
    return undefined;
  }

  const bracketed: RegExpMatchArray | null = trimmed.match(BRACKETED);
  let unwrapped: string = trimmed;
  if (bracketed !== null) {
    unwrapped = bracketed[1] ?? "";
  } else if (IPV4_WITH_PORT.test(trimmed)) {
    unwrapped = trimmed.slice(0, trimmed.lastIndexOf(":"));
  }

  return toBytes(unwrapped) === undefined ? undefined : unwrapped;
}

/** Whether `address` falls inside `network`'s leading `prefix` bits. */
function withinPrefix(address: readonly number[], network: TrustedProxy): boolean {
  if (address.length !== network.bytes.length) {
    return false;
  }

  let remaining: number = network.prefix;
  for (let index = 0; index < address.length && remaining > 0; index += 1) {
    const take: number = Math.min(8, remaining);
    const mask: number = (0xff << (8 - take)) & 0xff;
    if (((address[index] ?? 0) & mask) !== ((network.bytes[index] ?? 0) & mask)) {
      return false;
    }
    remaining -= take;
  }

  return true;
}

/**
 * The peers whose `X-Forwarded-For` `env` says to believe.
 *
 * A malformed entry is DROPPED rather than widened to match-everything: the
 * failure direction of a typo must be "trust less", never "trust all".
 *
 * The env record is a PARAMETER because this package is aliased into the client
 * bundle as well as the server one; the caller does the read, inside its
 * server-only module.
 */
export function resolveTrustedProxies(
  env: Readonly<Record<string, string | undefined>>,
): readonly TrustedProxy[] {
  const raw: string = (env[TRUSTED_PROXIES_ENV_KEY] ?? "").trim();
  if (raw === "") {
    return [];
  }

  const parsed: TrustedProxy[] = [];
  for (const candidate of raw.split(",")) {
    const match: RegExpMatchArray | null = candidate.trim().match(CIDR);
    if (match === null) {
      continue;
    }

    const bytes: number[] | undefined = toBytes(match[1] ?? "");
    if (bytes === undefined) {
      continue;
    }

    const width: number = bytes.length * 8;
    const prefix: number = match[2] === undefined ? width : Number(match[2]);
    if (prefix > width) {
      continue;
    }

    parsed.push({ bytes, prefix });
  }

  return parsed;
}

/** How many proxies `env` says sit in front of this app. See {@link resolveTrustedProxies}. */
export function resolveTrustedProxyHops(
  env: Readonly<Record<string, string | undefined>>,
): number {
  const raw: string = (env[TRUSTED_PROXY_HOPS_ENV_KEY] ?? "").trim();

  return HOP_COUNT.test(raw) ? Number(raw) : 0;
}

/** How much of `X-Forwarded-For` this deployment vouches for. */
export interface ForwardedTrust {
  /** Peers allowed to write the header at all, from {@link resolveTrustedProxies}. */
  readonly trustedProxies: readonly TrustedProxy[];
  /** Places in from the right, from {@link resolveTrustedProxyHops}. */
  readonly hops: number;
}

/**
 * The client's address for `request`.
 *
 * @param request The inbound request, for its `X-Forwarded-For` header.
 * @param peer The immediate peer address, which only the host knows.
 * @param trust What this deployment vouches for. Either gate failing answers with
 *   the peer.
 */
export function resolveClientAddress(
  request: Request,
  peer: string | undefined,
  trust: ForwardedTrust,
): string | undefined {
  const fallback: string | undefined = normalize(peer);
  if (trust.hops <= 0 || trust.trustedProxies.length === 0 || fallback === undefined) {
    return fallback;
  }

  // Gate 1: may this peer write the header at all? A caller that reached Node
  // without traversing the ingress cannot, however the chain is shaped.
  const peerBytes: number[] | undefined = toBytes(fallback);
  if (
    peerBytes === undefined ||
    !trust.trustedProxies.some((network: TrustedProxy): boolean => withinPrefix(peerBytes, network))
  ) {
    return fallback;
  }

  const chain: string | null = request.headers.get(FORWARDED_FOR_HEADER);
  if (chain === null) {
    return fallback;
  }

  const entries: string[] = chain
    .split(",")
    .map((entry: string): string => entry.trim())
    .filter((entry: string): boolean => entry !== "");

  // Gate 2: counted from the right, because each hop appends what it observed.
  return normalize(entries.at(-trust.hops)) ?? fallback;
}
```

**Step 2: Run the spec**

```bash
pnpm --filter @bc-solutions-coder/env test
```

Expected: the new file's assertions all pass, and `charter.test.ts` now **fails** four ways — the
manifest, the publish manifest, the Vite lib entries and `tsconfig.build.json` all still list three
modules while `src/` holds four. That is the charter working.

---

### Task 3.3: Wire the new subpath into the package's four registries

**Files:**

- Modify: `packages/env/package.json`
- Modify: `packages/env/vite.config.ts`
- Modify: `packages/env/tsconfig.build.json`

**Step 1: `package.json` — add to BOTH maps**

To `exports`, keeping alphabetical order (after `./base-path`):

```json
    "./client-address": {
      "types": "./src/client-address.ts",
      "import": "./src/client-address.ts"
    },
```

To `publishConfig.exports`, same position:

```json
    "./client-address": {
      "types": "./dist/client-address.d.ts",
      "import": "./dist/client-address.js"
    },
```

Also extend the `description`, which currently reads
`"... (request origin, internal origin, base path)"` → add `client address`.

**Step 2: `vite.config.ts` — add the lib entry**

```ts
    "base-path": "src/base-path.ts",
    "client-address": "src/client-address.ts",
    "internal-origin": "src/internal-origin.ts",
    "request-origin": "src/request-origin.ts",
```

**Step 3: `tsconfig.build.json` — add the include**

```json
  "include": [
    "src/base-path.ts",
    "src/client-address.ts",
    "src/internal-origin.ts",
    "src/request-origin.ts"
  ]
```

**Step 4: Run the charter**

```bash
pnpm --filter @bc-solutions-coder/env test
pnpm --filter @bc-solutions-coder/env typecheck
```

Expected: all green.

**Step 5: Commit the package before wiring consumers**

```bash
git add packages/env
git commit -m "feat(env): add a trust-gated client-address resolver"
```

---

### Task 3.4: Consume it in the two log-ingest routes

**Files:**

- Modify: `apps/wallow-web/src/app/lib/log-ingest.server.ts`
- Modify: `apps/wallow-auth/src/shared/lib/log-ingest.server.ts`
- Create: `apps/wallow-web/src/client-address-wiring.test.ts`
- Create: `apps/wallow-auth/src/client-address-wiring.test.ts`

Both server modules change identically. In each:

**Step 1: Import**

```ts
import {
  type ForwardedTrust,
  resolveClientAddress,
  resolveTrustedProxies,
  resolveTrustedProxyHops,
} from "@bc-solutions-coder/env/client-address";
```

**Step 2: Read the variables once, at module scope**

Beside the existing module-scope constants (`SERVICE`, `otlpEndpoint`):

```ts
/**
 * How much of `X-Forwarded-For` this deployment vouches for. Read once, beside
 * the handler it configures — the values cannot change within a process, and the
 * env read has to stay in a `*.server.*` module.
 */
const FORWARDED_TRUST: ForwardedTrust = {
  trustedProxies: resolveTrustedProxies(process.env),
  hops: resolveTrustedProxyHops(process.env),
};
```

**Step 3: Change the `clientAddress` seam**

Give the callback a name and export it, so Step 5 has something to drive. Above the
`createLogIngestHandler(...)` literal:

```ts
/**
 * The peer this request is attributed to, for both the rate-limit key and the
 * stamped `clientIp`. Exported so a spec can drive it; the handler below is its
 * only caller.
 */
export function resolveIngestClientAddress(request: PeerRequest): string | undefined {
  return resolveClientAddress(request, request.ip, FORWARDED_TRUST);
}
```

and in the options literal:

```ts
  clientAddress: resolveIngestClientAddress,
```

**Step 4: Correct the docblock above the handler**

Both files currently say:

```
 * `clientAddress` answers with the address srvx read off the connection, and it
 * is the ONLY source of the peer for both the rate-limit key and the stamped
 * `clientIp`. Nothing inbound is consulted: this route is unauthenticated, so a
 * header the caller writes would be a rate-limit bypass and a forged field.
```

That is no longer true and, left standing, is exactly the kind of comment that makes the next
reader route around a problem the code does not have. Replace with:

```
 * `clientAddress` starts from the address srvx read off the connection. It reads
 * `X-Forwarded-For` instead only when BOTH gates pass: the peer matches
 * `WALLOW_TRUSTED_PROXIES`, and `WALLOW_TRUSTED_PROXY_HOPS` says how far in from
 * the right the client sits. Unconfigured, the header is ignored entirely — the
 * failure direction this route wants, since it is unauthenticated and a freely
 * writable header would be both a rate-limit bypass and a forged `clientIp`. The
 * peer gate is what the hop count cannot express: these apps are reachable
 * without traversing the ingress, and a direct caller must not pick its own key.
```

**Step 5: Spec the app-side wiring**

Two app-level facts are worth pinning, and neither is covered by `packages/env`'s own spec.

The first is the srvx assumption the whole fallback rests on: `request.ip` is the socket peer only
because nothing sets srvx's `trustProxy`. With it set, `request.ip` becomes the **leftmost**
`X-Forwarded-For` entry — the most attacker-controlled one — and the "safe fallback" is forgeable.
There is **no `server.ts` in either app** (Start owns hosting; the host files are gone), so the
option could only appear in `vite.config.ts`, where `nitro()` is configured. Read that.

The second is that an unconfigured deployment ignores the header. Vitest runs with neither variable
set, so this is the default path and needs no env stubbing.

Create `apps/wallow-web/src/client-address-wiring.test.ts` and the byte-identical
`apps/wallow-auth/src/client-address-wiring.test.ts` (adjusting only the import path — wallow-auth's
module is `@shared/lib/log-ingest.server`). This is a **new file in both apps**; wallow-web's
`log-headers.test.ts` covers the correlation header and nothing else, and wallow-auth has no
equivalent to add to.

```ts
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { resolveIngestClientAddress } from "@app/lib/log-ingest.server";

/**
 * How this app attributes an ingest request to a caller.
 *
 * With neither `WALLOW_TRUSTED_PROXIES` nor `WALLOW_TRUSTED_PROXY_HOPS` set — the
 * state vitest runs in — the header is ignored and the socket peer wins. That
 * fallback is only safe while srvx's own `trustProxy` stays off: with it on,
 * `request.ip` becomes the leftmost forwarded entry, which the caller writes.
 */

const appDir: string = resolve(dirname(fileURLToPath(import.meta.url)), "..");

function requestWith(forwardedFor: string, ip: string): Request & { readonly ip: string } {
  return Object.assign(new Request("https://app.example/bff/logs", { headers: { "x-forwarded-for": forwardedFor } }), { ip });
}

describe("the ingest route's client address", () => {
  it("ignores a forwarded chain when no proxy is trusted", () => {
    expect(resolveIngestClientAddress(requestWith("1.1.1.1", "10.0.0.5"))).toBe("10.0.0.5");
  });

  it("answers undefined when the host supplies no peer", () => {
    expect(resolveIngestClientAddress(new Request("https://app.example/bff/logs"))).toBeUndefined();
  });

  it("leaves srvx's forwarded-header trust off, so request.ip stays the socket peer", () => {
    expect(readFileSync(resolve(appDir, "vite.config.ts"), "utf8")).not.toContain("trustProxy");
  });
});
```

`Object.assign` onto a `Request` is how the app's own `PeerRequest` shape is reproduced — srvx adds
`ip` the same way, and a WHATWG `Request` is not frozen.

If a fork ever needs `trustProxy`, this resolver is the thing to delete, not the assertion.

---

### Task 3.5: Consume it in the three outbound API proxies

**Files:**

- Modify: `apps/wallow-web/src/app/lib/bff.server.ts` (around line 151)
- Modify: `apps/wallow-auth/src/shared/lib/api-passthrough.server.ts` (around line 79)
- Modify: `apps/examples/minimal-app/src/lib/api-passthrough.ts` (around line 53)

Three sites, not two. The example app is a real consumer of the same seam and is the one a fork
copies from, so leaving it on the old shape ships the bug as the reference.

Same three edits in each — the import, the module-scope `FORWARDED_TRUST`, and the read.

**Step 1: Replace the read, and delete the inbound header unconditionally**

In all three files the stamping currently reads:

```ts
  const clientIp: string | undefined = request.ip;
  if (clientIp !== undefined && clientIp !== "") {
    request.headers.set(CLIENT_IP_HEADER, clientIp);
  }
```

Change to:

```ts
  // Deleted unconditionally FIRST: the seam header is on the proxy's forwarded
  // allowlist and is copied verbatim from the inbound request, so a caller that
  // sends its own survives into the outgoing X-Forwarded-For whenever the host
  // has no peer to overwrite it with. Only this hop may write it.
  request.headers.delete(CLIENT_IP_HEADER);

  const clientIp: string | undefined = resolveClientAddress(request, request.ip, FORWARDED_TRUST);
  if (clientIp !== undefined) {
    request.headers.set(CLIENT_IP_HEADER, clientIp);
  }
```

(`resolveClientAddress` already reads an empty value as no answer, so the `!== ""` guard goes.)

**Step 1a: Cover the delete with a spec**

This is a real forgery, independent of the rest of the phase, so it gets its own assertion. Add to
each app's passthrough spec (`bff.server.test.ts`, `api-passthrough.server.test.ts`,
`api-passthrough.test.ts`), which already builds requests and inspects the forwarded headers:

```ts
it("drops an inbound client-IP header the caller wrote", async () => {
  await handle(requestWith({ headers: { [CLIENT_IP_HEADER]: "1.2.3.4" } }));

  expect(forwarded?.headers.get("x-forwarded-for")).not.toContain("1.2.3.4");
});
```

Drive it through a request whose host peer is absent — that is the only path on which the old code
leaks, so a spec with a peer present passes against the bug.

**Step 2: Extend the docblock in `api-passthrough.server.ts`**

It already explains the header and cites Wallow-tt5j. Append:

```
 * The address itself comes from `resolveClientAddress`, so behind an ingress it
 * is the client's rather than the ingress's, and an inbound copy of the seam
 * header is dropped before this hop writes its own. The SDK APPENDS the value to
 * the outgoing `X-Forwarded-For` chain rather than replacing it, which is
 * correct: the API runs `UseForwardedHeaders` with the default `ForwardLimit` of
 * 1 and so reads the rightmost entry — the one stamped here.
```

Add the equivalent sentence to `bff.server.ts`'s comment at line 135 and to `minimal-app`'s at
line 37.

**Step 3: Run all three suites**

```bash
cd /Users/traveler/Repos/Wallow
pnpm --filter @bc-solutions-coder/wallow-web test
pnpm --filter @bc-solutions-coder/wallow-auth test
pnpm --filter @bc-solutions-coder/minimal-app test
```

`apps/wallow-web/src/log-headers.test.ts` and any spec around the passthrough may assert the old
shape. Fix the SPEC only where the new behaviour is genuinely correct — a spec asserting
`request.ip` reaches the header unchanged is still right at the default (no trusted proxies), so
most should pass untouched. If one fails, read it before editing: a failure here may be telling you
the default is not actually inert.

**Step 4: Commit**

Two commits, because these are two independent changes and the header-forgery fix stands on its own
whatever happens to the rest of the phase:

Name the first commit's files **exactly**. A directory add here does not work: Task 3.4 already
edited `apps/wallow-web/src/app/lib/log-ingest.server.ts` and
`apps/wallow-auth/src/shared/lib/api-passthrough.server.ts` sits beside its log-ingest sibling, so
`git add <dir>` sweeps the logging change into the header-forgery commit and there is no longer a
standalone fix to cherry-pick.

```bash
git add apps/wallow-web/src/app/lib/bff.server.ts \
        apps/wallow-auth/src/shared/lib/api-passthrough.server.ts \
        apps/examples/minimal-app/src/lib/api-passthrough.ts \
        apps/wallow-web/src/app/lib/bff.server.test.ts \
        apps/wallow-auth/src/shared/lib/api-passthrough.server.test.ts \
        apps/examples/minimal-app/src/lib/api-passthrough.test.ts
git commit -m "fix(sdk): drop an inbound client-ip header before stamping our own"

git add apps/wallow-web/src apps/wallow-auth/src apps/examples/minimal-app/src
git commit -m "fix(logging): key the rate limit on the real client behind a proxy"
```

`git status` after the first commit should show only the Task 3.4 files and the two new
`client-address-wiring.test.ts` files still unstaged.

---

### Task 3.6: Configure the deployment

**Files:**

- Modify: `docker/.env.production.example`
- Modify: `docker/docker-compose.production.yml` (the `wallow-web` and `wallow-auth` service
  `environment:` blocks, near lines 486 and 553)
- Modify: `docker/caddy/Caddyfile.example`

**Step 1: Give the compose network a fixed subnet and Caddy a fixed address**

Without this the peer is a dynamic Docker address and no tight allowlist is expressible — which is
the objection that led to the wrong design in the first place. In the top-level `networks:` block:

```yaml
networks:
  wallow:
    ipam:
      config:
        - subnet: 172.28.0.0/16
```

and on the `caddy` service:

```yaml
    networks:
      wallow:
        ipv4_address: 172.28.0.2
```

**Step 2: `.env.production.example`**

Beside the existing `WALLOW_REPOSITORY_URL` / `WALLOW_DOCS_URL` block:

```bash
# Which peers may write X-Forwarded-For, and how far in from the RIGHT of that
# header the real client sits. BOTH are required — the first says who is allowed
# to speak, the second says which word to believe.
#
# The reference stack has exactly one ingress, the Caddy container in this
# compose file, pinned to 172.28.0.2 so this allowlist can name it exactly.
# Anything else on the network — or on the host loopback the apps also publish
# to — is NOT trusted and gets keyed on its own socket address.
WALLOW_TRUSTED_PROXIES=172.28.0.2/32

# Raise the hop count only if you put another terminator (Cloudflare, an ALB, an
# ingress controller) in FRONT of that Caddy, and set it to the total. Doing so
# ALSO requires uncommenting `trusted_proxies` in the Caddyfile, because Caddy
# REPLACES X-Forwarded-For rather than appending unless it is configured to trust
# the hop in front of it — leave that out and the chain has one entry, this count
# overshoots it, and every caller falls back to sharing Caddy's bucket.
#
# Setting this HIGHER than the real number is the dangerous direction: it moves
# the counted position into the part of the chain a client can write, by padding
# the chain with entries of its own. That UNDER-limits. Too low over-limits.
WALLOW_TRUSTED_PROXY_HOPS=1
```

**Step 3: Both service blocks in `docker-compose.production.yml`**

```yaml
      WALLOW_TRUSTED_PROXIES: ${WALLOW_TRUSTED_PROXIES:-172.28.0.2/32}
      WALLOW_TRUSTED_PROXY_HOPS: ${WALLOW_TRUSTED_PROXY_HOPS:-1}
```

The defaults are deliberate: this compose file *is* the one-Caddy topology at that fixed address, so
a fork that never opens `.env.production` still gets the correct values. The library defaults stay
"trust nothing" for everything else.

**Step 4: `Caddyfile.example`**

The header comment already explains that Caddy stamps `X-Forwarded-For` by default and that a
replacement ingress must reproduce it. It is missing the part that matters here — Caddy **discards**
what the client sent rather than appending to it, which is why one hop is safe. Extend that
paragraph:

```
# Caddy REPLACES X-Forwarded-For with what it observed, discarding whatever the
# client sent, unless `trusted_proxies` below is configured. That discard is what
# makes WALLOW_TRUSTED_PROXY_HOPS=1 safe in this stack — not any property of the
# chain itself. If you put a terminator in FRONT of this Caddy you must BOTH
# uncomment `trusted_proxies` (so Caddy appends instead of replacing) AND raise
# the apps' hop count to the total. Doing only one of the two silently breaks
# client attribution: uncommented alone trusts a forged prefix, raised alone
# reads past the end of a one-entry chain.
```

**Step 5: Verify the direct-reach path is actually closed**

The whole point of the peer gate. With the stack up:

```bash
docker compose -f docker/docker-compose.production.yml --env-file docker/.env.production \
  exec caddy wget -qO- --header 'X-Forwarded-For: 1.2.3.4' http://wallow-web:8080/health
```

Then send the same forged header from a container that is **not** Caddy, and confirm the log record
it produces carries that container's own address rather than `1.2.3.4`. If it carries `1.2.3.4`, the
allowlist is not being read — stop and fix it, because that is the exact defect this task exists to
close.

**Step 6: Commit**

```bash
git add docker/
git commit -m "build(docker): pin the ingress address and declare proxy trust"
```

---

### Task 3.7: Document it

**Files:**

- Modify: `packages/env/CLAUDE.md`
- Modify: `packages/logger/CLAUDE.md`
- Modify: `packages/logger/src/server.ts`
- Modify: `apps/CLAUDE.md`
- Modify: `docs/getting-started/configuration.md`
- Modify: `docs/development/logging.md`

**Three of these carry claims this phase makes FALSE.** They are not optional polish. Two of the
seven beads in this plan exist *because* prose outlived the code it described; shipping this phase
without amending them creates the eighth.

**Step 1: `packages/env/CLAUDE.md`** — add the entries-table row:

```markdown
| `./client-address`  | `resolveClientAddress(request, peer, trust)` + `resolveTrustedProxies(env)` + `resolveTrustedProxyHops(env)` + both env keys — the caller's address behind an ingress. |
```

and a short section after "It reads no environment of its own", since this is the second module
following the env-key-plus-parameter pattern:

```markdown
## Two gates, not one

`resolveClientAddress` takes its trust as a parameter for the same reason `resolveInternalOrigin`
takes the env record: the read stays at the server-only call site. What it takes is TWO settings,
because they answer different questions and either alone is a hole.

`WALLOW_TRUSTED_PROXIES` says WHO may write `X-Forwarded-For`, checked against the socket peer. A
hop count cannot express this, and the apps are reachable without traversing the ingress — they sit
on the shared compose network as well as loopback — so a caller that reaches Node directly would
otherwise choose its own rate-limit bucket and its own stamped address. That is strictly worse than
reading the peer, which is why the hop count did not ship alone.

`WALLOW_TRUSTED_PROXY_HOPS` says WHICH entry, counted from the RIGHT, since each hop appends what it
observed. Both default to trusting nothing, which answers with the peer.

The asymmetry matters and is easy to get backwards: a too-LOW hop count over-limits, but a too-HIGH
one UNDER-limits — a client pads the chain with entries of its own until one lands in the counted
position. Do not write that every misconfiguration fails safe.
```

**Step 2: `packages/logger/src/server.ts`** — two claims at lines 20–24 are now false:
"keyed on the address the HOST supplies rather than on anything inbound", and "The client IP comes
from `clientAddress`, never off the wire." It still comes from `clientAddress`, but `clientAddress`
may now consult `X-Forwarded-For`. Rewrite to say the callback is still the only source **and** that
what the callback trusts is the caller's decision — this package neither reads nor names a header
for it.

**Step 3: `packages/logger/CLAUDE.md`** (lines 60–68) — "A header cannot be that source" and "**Both
apps answer with `request.ip`**" are both now false. The reasoning in that paragraph is still right
about *why* a raw header is unusable; what changed is that the apps now answer with a resolver that
gates the header on the peer. Keep the argument, correct the fact, and say the package still holds
no client-IP constant.

**Step 4: `apps/CLAUDE.md`** (line 140) — "which is why there is **no logging environment variable
beyond** the standard `OTEL_EXPORTER_OTLP_ENDPOINT`" is falsified by two new variables. Amend it.

**Step 5: `docs/getting-started/configuration.md`** — add both variables to whichever
environment-variable table the two Node apps' variables already live in. Match the existing row
format; keep the prose to what a fork operator needs: what each is, that the reference stack pins
Caddy at `172.28.0.2/32` with one hop, that the Caddyfile's `trusted_proxies` must be uncommented
before raising the hop count, and that too high is the dangerous direction.

**Step 6: `docs/development/logging.md`** — find where the ingest rate limit is described and note
that its key is the resolved client address, so behind an ingress both variables must be set or the
limit applies to the ingress as a whole.

**Step 7: Check nothing else asserts the old shape**

```bash
cd /Users/traveler/Repos/Wallow
grep -rn "request\.ip\|never off the wire\|A header cannot be that source\|no logging environment variable" \
  --include='*.md' --include='*.ts' . | grep -v node_modules | grep -v docs/plans
```

Expected: only the five call sites themselves. Any prose hit is a claim this phase falsified — amend
it here rather than leaving it for the next reader.

**Step 4: Verify the docs site still builds**

```bash
cd /Users/traveler/Repos/Wallow
docfx docfx.json --warningsAsErrors 2>&1 | tail -20
```

(Skip if `docfx` is not installed locally — CI covers it. No `toc.yml` entry is needed; no new page
was added.)

**Step 5: Commit and close**

```bash
git add packages/env/CLAUDE.md packages/logger/CLAUDE.md packages/logger/src/server.ts \
  apps/CLAUDE.md docs/
git commit -m "docs: document the two proxy-trust variables and amend stale claims"
bd close Wallow-tvn3
```

---

## Phase 4 — Gate and push

### Task 4.1: Full quality gate

```bash
cd /Users/traveler/Repos/Wallow
pnpm check
```

No pre-build step. `pnpm check` is `format:check && lint && lint:tests && build && typecheck &&
test && check:exports` — `build` already sits ahead of everything that needs a `dist/`, and the
package this plan gives a new entrypoint to is `packages/env`, not the SDK. `check-exports.sh`
covers `packages/env` in its list, so the fourth registry from Task 3.3 is verified here: get
`publishConfig.exports` wrong and attw reports `./client-address` resolving to no types.

Expected: all seven steps green, in that order. If `format:check` fails, run `pnpm format` and
amend the relevant commit.

Two of those steps are easy to skip by hand and both matter here:

- `pnpm lint:tests` covers `packages/env/src/client-address.test.ts`; `pnpm lint` does **not**.
- `pnpm build` is where Phase 2's fix actually lands — the alias only shows up in build output.

No backend test run is required: Phase 1 touches only `.md` files and no `.cs`, so neither
`./scripts/run-tests.sh` nor `dotnet format` applies.

### Task 4.2: E2E

Phase 3 changed the passthrough's outbound headers, which the backend-dependent suites exercise.

```bash
./scripts/e2e.sh
```

Expected: wallow-auth, wallow-web and the wallow-web cross-app login journey all pass. If the stack
is slow to build, `E2E_SKIP_IMAGE_BUILD=1` reuses prebuilt images — but **not** on this run: the
apps changed, so the images must be rebuilt.

### Task 4.3: Push

```bash
git pull --rebase
bd dolt push
git push
git status
```

Expected: `Your branch is up to date with 'origin/main'`.

### Task 4.4: Final bead state

```bash
bd show Wallow-tvn3 Wallow-l77c Wallow-luni Wallow-uc2c Wallow-1lt5 Wallow-75pg Wallow-a5mt \
  | grep -E "^[○✓✗]"
```

Expected: all seven closed, plus the one new follow-up bead from Task 2.6 open.

---

## What this plan deliberately does not do

- **No trust inferred from the network topology.** Both gates are explicit environment variables
  that default to trusting nothing. An unconfigured fork behaves exactly as today — the peer, and
  only the peer — which is the only default that is safe without knowing the deployment.
- **No `Forwarded` (RFC 7239) support.** Caddy does not emit it, the API does not read it, and a
  second header format is a second thing to get the trust boundary wrong in. `X-Forwarded-For` only.
- **No change to `api/src/Wallow.Api/Program.cs`.** Its `UseForwardedHeaders` already reads the
  rightmost chain entry, which is the one the fixed Node side stamps.
- **No build-output regression spec for Wallow-luni.** Vitest never produces the Nitro bundle, so
  the check belongs in CI, not in a `*.test.ts`. Filed as a follow-up in Task 2.6 rather than
  smuggled into this plan.
- **No change to the SDK's append-not-replace `X-Forwarded-For` behaviour.** Correct as-is given
  `ForwardLimit = 1`.
