**status: active**

# Path aliases → Vite 8 native `resolve.tsconfigPaths` — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make each app's `tsconfig.json` `paths` the single declaration site for its zone
aliases, so adding a zone is a one-file edit instead of an eight-file ritual.

**Architecture:** Vite 8.1.4 ships `resolve.tsconfigPaths` natively — it reads the nearest
`tsconfig.json` `paths` and resolves against it, no plugin. Turning it on lets `apps/*/aliases.ts`,
the four `resolve.alias` zone splices, and the entire `alias-map.test.ts` mirror-lock disappear,
because there is no longer a second copy to keep in sync. `zone-dag.test.ts` then *reads*
`tsconfig.json` `paths` instead of hard-coding three prefixes, so a new zone is policed the
moment it is declared. **Zero source churn** — every `@app/*` / `@features/*` / `@shared/*`
import statement in `src/` is untouched.

**Tech Stack:** Vite 8.1.4, TypeScript 7.0.2 (`moduleResolution: "Bundler"`), Vitest 4
two-project split, TanStack Start 1.168.32 + `nitro/vite`, pnpm 10.20.0 workspace.

**Evidence base:** `docs/plans/2026-07-30/1640-alias-architecture-decision.md` and the four
reports it synthesises. Every claim below traces there. Measured diff on wallow-web:
**9 insertions / 118 deletions across 5 files.**

---

## Before you start

**Build the whole workspace first.** `pnpm --filter @bc-solutions-coder/sdk build` alone is NOT
enough — apps will show 30+ spurious `TS2307`s and you will misdiagnose them as alias breakage.

```bash
pnpm install
pnpm build
```

Expected: all 10 projects build, no errors.

**Three landmines, stated up front so you don't rediscover them:**

1. `resolve.tsconfigPaths` at the ROOT of `vitest.config.ts` is **not inherited** by
   `test.projects`. It must go inside each project entry. Task 3 proves this.
2. Do **not** hoist `paths` into `tsconfig.base.json`. `paths` resolve relative to the file
   that declares them, so a parent-directory base silently points at `../../src/app/*`.
3. `resolve.tsconfigPaths` is marked `@experimental` in Vite 8.1.4. Measured cost is small
   (typecheck 2.7s → 3.1s, build 1.7s → 1.8s, `.output/server/index.mjs` byte-identical), but
   it can move in a minor. That is the accepted risk; it is TanStack Start's officially
   documented approach.

**Two aliases must STAY in `resolve.alias`** — `tsconfigPaths` cannot express either:

- the anchored `use-sync-external-store/shim` regexes (both apps' `vite.config.ts`)
- the `node:async_hooks` browser shim (wallow-web's vitest browser project)

---

## Task 0: File the `importProtection` finding as its own bead — do NOT fix it here

The audit surfaced a bug that is **more serious than anything in this plan and completely
independent of it**: `importProtection` does not reject `redis` from a client module, and
`redis` actually ships in the client bundle. The real TanStack default rule is `**/*.server.*`
files, not the `app/lib/bff.ts` protection the code comment claims.

Fixing it inside this refactor would tangle two unrelated changes in one diff. File it, link the
evidence, move on.

**Step 1: Create the bead**

```bash
bd create "importProtection does not keep redis out of the client bundle" \
  -d "apps/wallow-web/vite.config.ts:83-91 claims importProtection stops app/lib/bff.ts's redis import from reaching a client bundle. It does not: the baseline build exits 0 and redis is present in the client output. TanStack's default importProtection ruleset only covers **/*.server.* files. The srcDirectory: 'src/app' + importProtection: { include: ['src/**'] } pairing IS load-bearing for SCOPE (start-plugin-core adapterUtils.ts:86-87), but the comment overstates what the rule catches. Decide: adopt a *.server.* naming convention for server-only modules, add explicit importProtection rules, or correct the comment to match reality. Evidence: docs/plans/2026-07-30/1640-alias-architecture-decision.md 'Separate finding'."
```

**Step 2: Record the bead id in this file**

Replace this line with the id: `Bead: ______`

**Step 3: Commit**

Nothing to commit — beads are tracked separately. Move to Task 1.

---

## Task 1: Capture the green baseline

You need to know these suites were green *before* you touched anything, or a pre-existing
failure will read as a regression.

**Files:** none modified.

**Step 1: Run both apps' suites and typecheck**

```bash
pnpm --filter ./apps/wallow-web test 2>&1 | tail -20
pnpm --filter ./apps/wallow-auth test 2>&1 | tail -20
pnpm --filter ./apps/wallow-web typecheck && pnpm --filter ./apps/wallow-auth typecheck
```

Expected: all green. `alias-map.test.ts` reports 6 passing tests per app (they are about to be
deleted). Write down the wallow-web and wallow-auth test counts — Task 5 and Task 6 compare
against them minus 6.

> **Never pipe a gate through `tail` and read the exit code.** `pnpm check 2>&1 | tail -80`
> reports `tail`'s status, not the gate's. If you need both, write to a file and check `$?`
> before touching the pipe: `pnpm check > /tmp/check.log 2>&1; echo "EXIT=$?"; tail -80 /tmp/check.log`.

**Step 2: Capture the production bundle fingerprint**

```bash
pnpm --filter ./apps/wallow-web build
wc -c apps/wallow-web/.output/server/index.mjs
```

Expected: a byte count you will re-check in Task 5. The spike measured **18071 bytes**; yours may
differ if the app has moved on. What matters is that it does not change.

---

## Task 2: wallow-web — turn on native `tsconfigPaths` in the Vite config

**Files:**
- Modify: `apps/wallow-web/vite.config.ts:7` (remove import), `:53-57` (replace splice)
- Modify: `apps/wallow-web/tsconfig.json:10-12` (comment), `:19` (include)

**Step 1: Remove the alias-module import**

In `apps/wallow-web/vite.config.ts`, delete line 7 and the blank line after it:

```ts
import { resolveAlias } from "./aliases";
```

**Step 2: Replace the zone splice with the native option**

Replace lines 53-57 (the three comment lines, the spread, and the closing `],`) with:

```ts
    ],
    // The zone aliases (`@app/*`, `@features/*`, `@shared/*`) come from
    // `tsconfig.json` `paths` — Vite 8 reads it natively, so tsconfig is the ONE
    // place a zone is declared. The anchored regexes above stay in `alias`
    // because `paths` cannot express a regex, and they are evaluated first.
    tsconfigPaths: true,
```

The result — `resolve` in full:

```ts
  resolve: {
    alias: [
      // ...the use-sync-external-store comment block, unchanged...
      { find: /^use-sync-external-store\/shim$/u, replacement: "react" },
      { find: /^use-sync-external-store\/shim\/index\.js$/u, replacement: "react" },
    ],
    // The zone aliases (`@app/*`, `@features/*`, `@shared/*`) come from
    // `tsconfig.json` `paths` — Vite 8 reads it natively, so tsconfig is the ONE
    // place a zone is declared. The anchored regexes above stay in `alias`
    // because `paths` cannot express a regex, and they are evaluated first.
    tsconfigPaths: true,
    // One React in the graph, from any resolution path.
    dedupe: ["react", "react-dom"],
  },
```

**Step 3: Promote tsconfig to sole source of truth**

In `apps/wallow-web/tsconfig.json`, replace the mirror comment on lines 10-12 with:

```jsonc
    // The SINGLE declaration site for this app's zone aliases. Vite reads it via
    // `resolve.tsconfigPaths` (vite.config.ts), vitest reads it per-project
    // (vitest.config.ts), and `src/zone-dag.test.ts` reads it to derive which
    // prefixes it polices. Adding a zone is this edit and nothing else.
    // `moduleResolution: "Bundler"` resolves these relative to THIS file, so no
    // `baseUrl` — and `paths` must NOT move to `tsconfig.base.json`, which would
    // resolve them against the repo root.
```

And on line 19 drop `"aliases.ts"` from `include`:

```jsonc
  "include": ["src/**/*.ts", "src/**/*.tsx", "vite.config.ts", "vitest.config.ts"]
```

**Step 4: Verify the build still resolves every alias**

```bash
pnpm --filter ./apps/wallow-web build
```

Expected: build succeeds. A failure here would be `Failed to resolve import "@shared/..."` — if
you see that, `tsconfigPaths: true` is misspelled or landed outside `resolve`.

**Step 5: Commit**

```bash
git add apps/wallow-web/vite.config.ts apps/wallow-web/tsconfig.json
git commit -m "refactor(web): resolve zone aliases from tsconfig paths natively"
```

---

## Task 3: wallow-web — move vitest onto `tsconfigPaths`, per project

**Files:**
- Modify: `apps/wallow-web/vitest.config.ts:6` (remove import), `:94-102` (projects)

**Step 1: Prove the landmine before you avoid it** *(2 min, worth it)*

Temporarily add `resolve: { tsconfigPaths: true }` at the ROOT of the exported config (sibling of
`test:`), and change the `node` project entry to plain `node` with no `resolve`:

```ts
export default defineConfig({
  resolve: { tsconfigPaths: true },   // TEMPORARY — proving this does not inherit
  ssr: { noExternal: ["@bc-solutions-coder/query", "@bc-solutions-coder/auth"] },
  test: { projects: [node, /* ...browser unchanged... */] },
});
```

Run:

```bash
pnpm --filter ./apps/wallow-web exec vitest run --project node 2>&1 | tail -20
```

Expected: **FAIL** with `Failed to resolve import "@shared/..."` (or `@app/...`). This is the
whole reason the next step puts the option inside each project. Now revert this experiment.

**Step 2: Remove the alias-module import**

In `apps/wallow-web/vitest.config.ts`, delete line 6 and the blank line after it:

```ts
import { resolveAlias } from "./aliases";
```

**Step 3: Swap each project's `resolve`**

Replace the `projects` array (lines 95-101) with:

```ts
    // `resolve` is PER PROJECT — a root-level `resolve` is NOT inherited by
    // `test.projects`, so `tsconfigPaths` has to be repeated here. Both entries
    // read the same `tsconfig.json` `paths` the app builds against.
    projects: [
      { ...node, resolve: { tsconfigPaths: true } },
      {
        ...browser,
        resolve: { tsconfigPaths: true, alias: { "node:async_hooks": nodeAsyncHooksShim } },
      },
    ],
```

The `node:async_hooks` shim stays — `tsconfigPaths` cannot express it, and without it every spec
importing `src/app/router.tsx` dies at import in the browser project.

**Step 4: Run both projects**

```bash
pnpm --filter ./apps/wallow-web test 2>&1 | tail -20
```

Expected: same pass count as Task 1's baseline. `alias-map.test.ts` still passes at this point —
it is deleted in Task 4.

**Step 5: Commit**

```bash
git add apps/wallow-web/vitest.config.ts
git commit -m "test(web): resolve vitest zone aliases from tsconfig paths"
```

---

## Task 4: wallow-web — delete the alias module and its mirror-lock spec

Both artifacts exist *only* to keep a duplicate in sync with `tsconfig.json`. There is no longer
a duplicate.

**Files:**
- Delete: `apps/wallow-web/aliases.ts`
- Delete: `apps/wallow-web/src/alias-map.test.ts`

**Step 1: Confirm nothing still imports them**

```bash
grep -rn "resolveAlias\|aliasDirs\|\"\./aliases\"\|alias-map" apps/wallow-web --include="*.ts" --include="*.tsx" --include="*.json" | grep -v node_modules
```

Expected: only hits inside the two files about to be deleted. Anything else — stop and fix it
first.

**Step 2: Delete**

```bash
git rm apps/wallow-web/aliases.ts apps/wallow-web/src/alias-map.test.ts
```

**Step 3: Verify**

```bash
pnpm --filter ./apps/wallow-web typecheck
pnpm --filter ./apps/wallow-web test 2>&1 | tail -20
```

Expected: typecheck clean. Test count = baseline **minus 6** (the four `alias-map` cases, one of
which is `it.each` over two files, so six results), and one fewer test *file*.

**Step 4: Commit**

```bash
git commit -m "refactor(web): drop the alias map and its mirror-lock spec"
```

---

## Task 5: wallow-web — full verification, including a booted production bundle

A Vite-only alias change breaking the Nitro server bundle was the one risk that could kill this.
The spike proved it does not exist (Nitro's `config` hook returns `resolve.alias` back into Vite,
`nitro/dist/vite.mjs:313-325`, so there is one merged resolver). Confirm it on your tree anyway —
build success alone is not proof.

**Step 1: Build and compare the fingerprint**

```bash
pnpm --filter ./apps/wallow-web build
wc -c apps/wallow-web/.output/server/index.mjs
```

Expected: **byte-identical to the Task 1 baseline.**

**Step 2: Prove no unresolved specifier leaked into the output**

```bash
grep -c '"@app/\|"@shared/\|"@features/' apps/wallow-web/.output/server/index.mjs || echo "0 — clean"
grep -rl '"@app/\|"@shared/\|"@features/' apps/wallow-web/.output/public 2>/dev/null || echo "public clean"
```

Expected: `0 — clean` and `public clean`. A bare zone specifier surviving into `.output/` means
the server would try to resolve it at runtime and crash.

**Step 3: Boot it and serve a page**

```bash
(cd apps/wallow-web && node .output/server/index.mjs &) && sleep 3 && \
  curl -s -o /dev/null -w "%{http_code} %{size_download}\n" http://localhost:3000/bff-demo
```

Expected: `200` and a non-trivial byte count (the spike measured 7213 bytes). Kill the server
afterwards: `pkill -f ".output/server/index.mjs"`.

`/bff-demo` is chosen deliberately — it is the only wallow-web route that renders without the
backend. Every other dashboard route redirects to OIDC.

**Step 4: Commit**

Nothing to commit — verification only.

---

## Task 6: Mirror the whole change onto wallow-auth

wallow-auth's `aliases.ts`, `alias-map.test.ts` and `zone-dag.test.ts` are **byte-identical** to
wallow-web's; its `tsconfig.json` differs only in a comment. Its vite/vitest configs differ in
app-specific knobs but carry the identical alias wiring. The spikes ran on wallow-web only, so
wallow-auth is genuinely unverified — run its gates, don't assume.

**Files:**
- Modify: `apps/wallow-auth/vite.config.ts:7` (import), `:68-72` (splice)
- Modify: `apps/wallow-auth/tsconfig.json:11-13` (comment), `:20` (include)
- Modify: `apps/wallow-auth/vitest.config.ts:4` (import), `:51-60` (projects)
- Delete: `apps/wallow-auth/aliases.ts`, `apps/wallow-auth/src/alias-map.test.ts`

**Step 1: `vite.config.ts`** — delete line 7 (`import { resolveAlias } from "./aliases";`) and its
blank line; replace lines 68-72 exactly as in Task 2 Step 2. wallow-auth has the same two anchored
`use-sync-external-store` regexes — keep them, keep `dedupe`, keep `base: VITE_BASE`.

**Step 2: `tsconfig.json`** — same comment replacement as Task 2 Step 3, and drop `"aliases.ts"`
from `include` on line 20.

**Step 3: `vitest.config.ts`** — delete line 4 and its blank line; replace lines 51-60 with:

```ts
  test: {
    // `resolve` is PER PROJECT — a root-level `resolve` is NOT inherited by
    // `test.projects`. Both entries read `tsconfig.json` `paths`, the same
    // declaration the app builds against.
    projects: [
      { ...node, resolve: { tsconfigPaths: true } },
      { ...browser, resolve: { tsconfigPaths: true } },
    ],
  },
```

wallow-auth needs no `node:async_hooks` shim — it has none today, so its browser project carries
no `alias` key at all.

**Step 4: Confirm nothing else references the module, then delete**

```bash
grep -rn "resolveAlias\|aliasDirs\|\"\./aliases\"\|alias-map" apps/wallow-auth --include="*.ts" --include="*.tsx" --include="*.json" | grep -v node_modules
git rm apps/wallow-auth/aliases.ts apps/wallow-auth/src/alias-map.test.ts
```

**Step 5: Verify — build, boot, serve**

```bash
pnpm --filter ./apps/wallow-auth typecheck
pnpm --filter ./apps/wallow-auth test 2>&1 | tail -20
pnpm --filter ./apps/wallow-auth build
grep -c '"@app/\|"@shared/\|"@features/' apps/wallow-auth/.output/server/index.mjs || echo "0 — clean"
(cd apps/wallow-auth && PORT=3002 node .output/server/index.mjs &) && sleep 3 && \
  curl -s -o /dev/null -w "%{http_code} %{size_download}\n" http://localhost:3002/
pkill -f ".output/server/index.mjs"
```

Expected: typecheck clean; test count = wallow-auth baseline minus 6; build succeeds; `0 — clean`;
HTTP `200`.

wallow-auth's `/` renders without a backend (the login form posts on submit), so this is a valid
backend-free smoke.

**Step 6: Commit**

```bash
git add apps/wallow-auth
git commit -m "refactor(auth): resolve zone aliases from tsconfig paths natively"
```

---

## Task 7: Make `zone-dag.test.ts` derive its zones from `tsconfig.json`

This is the task that actually delivers "scales with features and doesn't require so much upkeep."

`targetOf` (`src/zone-dag.test.ts:162-195`) hard-codes `@app/`, `@shared/` and `@features/`.
A fourth zone — say `@entities/*` — falls through line 185 to `{ kind: "package" }` and is
**silently unpoliced**: the DAG reports clean while an entire zone crosses boundaries unchecked.
That is a guard failing open, which is the worst way for a guard to fail.

The `alias` clause itself is **correct and stays.** Under this design, cross-zone edges are still
spelled `@zone/...`, and requiring that spelling is what makes a boundary crossing visible in the
import block. What changes is *where the list of zone prefixes comes from*.

Do wallow-web first, then copy the identical file to wallow-auth (they are byte-identical today
and must stay so).

**Files:**
- Test: `apps/wallow-web/src/zone-dag.test.ts` (this file IS the test)
- Create (temporary): `apps/wallow-web/src/entities/probe-entity.ts`

**Step 1: Write the failing probe — a fourth zone that violates the DAG**

Create `apps/wallow-web/src/entities/probe-entity.ts`:

```ts
/** TEMPORARY fourth-zone probe — deleted at the end of this task. */
import { LOGIN_MARKER } from "@features/mfa";

export const ENTITY_MARKER = LOGIN_MARKER;
```

Add the zone to `apps/wallow-web/tsconfig.json` `paths`:

```jsonc
    "paths": {
      "@app/*": ["./src/app/*"],
      "@features/*": ["./src/features/*"],
      "@entities/*": ["./src/entities/*"],
      "@shared/*": ["./src/shared/*"]
    }
```

Have some module reach it so the walk sees an importer in the zone — append to
`apps/wallow-web/src/shared/components/ready-indicator.tsx`'s import block:

```tsx
import { ENTITY_MARKER } from "@entities/probe-entity";
```

and `void ENTITY_MARKER;` as the first statement of `ReadyIndicator`.

> If `@features/mfa` has no `LOGIN_MARKER` export, use any real export from any feature barrel —
> the point is only that `entities/` reaches sideways into a feature it should not.

**Step 2: Run the DAG spec and watch it pass when it should fail**

```bash
pnpm --filter ./apps/wallow-web exec vitest run src/zone-dag.test.ts 2>&1 | tail -30
```

Expected: **the DAG rules PASS** (that's the bug — `@entities/` fell through to `kind: "package"`),
and only `finds importers in all three zones` fails, because the hard-coded
`["app", "features", "root", "shared"]` list did not expect `entities`. Two failures of the same
root cause: the spec does not know what the zones are.

**Step 3: Derive the zone prefixes from `tsconfig.json`**

Add this above `zoneOf` in `apps/wallow-web/src/zone-dag.test.ts` (and the two imports it needs to
the top of the file: `readFileSync` is already imported; add `resolve` — also already imported):

```ts
/**
 * The zone aliases, read from the app's `tsconfig.json` `paths` — the ONE place a
 * zone is declared, and the same file Vite and vitest resolve against.
 *
 * Deriving rather than hard-coding is the point: a fourth zone added to `paths`
 * is policed by every rule below from the moment it exists. The previous version
 * of this spec listed `@app/`, `@features/` and `@shared/` inline, so a new zone
 * fell through `targetOf` to `kind: "package"` and crossed every boundary unwatched
 * while the suite reported a clean DAG. A guard that fails open is worse than none.
 *
 * `tsconfig.json` carries `//` comments — strip them before parsing.
 */
function declaredZoneAliases(): readonly string[] {
  const text: string = readFileSync(resolve(srcDir, "..", "tsconfig.json"), "utf8").replaceAll(
    /^\s*\/\/.*$/gmu,
    "",
  );
  const config = JSON.parse(text) as { compilerOptions?: { paths?: Record<string, string[]> } };
  const paths: Record<string, string[]> = config.compilerOptions?.paths ?? {};

  return Object.keys(paths)
    .map((key): string => key.replace(/\/\*$/u, ""))
    .toSorted();
}

/** `["@app", "@entities", "@features", "@shared"]` — alias prefixes, no trailing `/*`. */
const ZONE_ALIASES: readonly string[] = declaredZoneAliases();

/** `["app", "entities", "features", "shared"]` — the zone names those aliases name. */
const ZONE_NAMES: readonly string[] = ZONE_ALIASES.map((alias): string => alias.slice(1));

/**
 * Zones whose members are BARREL-ONLY: `@features/login` is the contract,
 * `@features/login/anything` reaches around it. Every other zone is a flat
 * namespace where a deep path is normal (`@shared/lib/x`, `@app/routes/y`).
 *
 * This is a design decision per zone, not something a path can tell you — so it
 * stays an explicit list, and a new zone defaults to flat.
 */
const BARREL_ZONES: ReadonlySet<string> = new Set(["features"]);
```

**Step 4: Rewrite `targetOf` to use them**

Replace the whole body of `targetOf` (lines 162-195) with:

```ts
/** Classify one specifier as written by `file`. */
function targetOf(file: string, specifier: string): Target {
  const alias: string | undefined = ZONE_ALIASES.find((candidate): boolean =>
    specifier.startsWith(`${candidate}/`),
  );

  if (alias !== undefined) {
    const zoneName: string = alias.slice(1);
    const segments: readonly string[] = specifier.split("/");

    return BARREL_ZONES.has(zoneName)
      ? {
          kind: "zone",
          zone: `${zoneName}/${segments[1] as string}`,
          alias: true,
          deep: segments.length > 2,
        }
      : { kind: "zone", zone: zoneName, alias: true, deep: false };
  }

  if (!specifier.startsWith(".")) {
    return { kind: "package" };
  }

  const importerDir: string = join(srcDir, dirname(file));
  const resolved: string = relative(srcDir, resolve(importerDir, specifier));

  return resolved.startsWith("..")
    ? { kind: "outside" }
    : { kind: "zone", zone: zoneOf(resolved), alias: false, deep: false };
}
```

Also generalise `zoneOf` (lines 148-159) so it does not special-case `features` by name:

```ts
/** The zone a `src/`-relative path belongs to. */
function zoneOf(srcRelativePath: string): Zone {
  const segments: readonly string[] = srcRelativePath.split("/");

  if (segments.length < 2) {
    return "root";
  }

  const top: string = segments[0] as string;

  return BARREL_ZONES.has(top) ? `${top}/${segments[1] as string}` : top;
}
```

**Step 5: Make the walk guard derive its expectation too**

Replace the `finds importers in all three zones` case (lines 224-230) with:

```ts
  it("finds importers in every declared zone", () => {
    const zones: ReadonlySet<Zone> = new Set(
      ALL.map((edge): string => edge.zone.split("/")[0] as string),
    );

    // `root` is not a tsconfig zone — it is the policy specs sitting directly
    // under `src/`, which this file is one of.
    expect([...zones].toSorted()).toEqual(["root", ...ZONE_NAMES].toSorted());
  });
```

Update the file's header docblock: replace the sentence naming
"`@app/*`, `@features/<x>`, `@shared/*`" with "the zone aliases declared in `tsconfig.json`
`paths`", and add a line noting that this spec now reads that file, which is what replaced the
deleted `alias-map.test.ts`.

**Step 6: Run it — the probe must now FAIL**

```bash
pnpm --filter ./apps/wallow-web exec vitest run src/zone-dag.test.ts 2>&1 | tail -30
```

Expected: **`never lets shared/ reach a feature`** now fails, listing
`shared/components/ready-indicator.tsx -> @entities/probe-entity`? No — expect
`keeps each feature out of every other feature` or `reaches a feature only through its barrel`
to fire on `entities/probe-entity.ts -> @features/mfa`. The exact rule depends on which barrel you
imported; what matters is that **a rule fires on the `entities/` edge**, where before the change
none did. `finds importers in every declared zone` now passes.

**Step 7: Delete the probe**

```bash
git checkout -- apps/wallow-web/src/shared/components/ready-indicator.tsx
rm -rf apps/wallow-web/src/entities
```

Then remove the `"@entities/*"` line from `apps/wallow-web/tsconfig.json`.

> **Careful with `git checkout --`.** `ready-indicator.tsx` may carry other uncommitted work in a
> shared checkout. If `git status` shows it dirty for any reason other than your probe edit,
> revert your two lines by hand instead.

**Step 8: Full suite green**

```bash
pnpm --filter ./apps/wallow-web test 2>&1 | tail -20
pnpm --filter ./apps/wallow-web typecheck
```

Expected: green, same counts as Task 4.

**Step 9: Copy the file to wallow-auth and verify there**

The two files must stay byte-identical.

```bash
cp apps/wallow-web/src/zone-dag.test.ts apps/wallow-auth/src/zone-dag.test.ts
diff apps/wallow-web/src/zone-dag.test.ts apps/wallow-auth/src/zone-dag.test.ts && echo IDENTICAL
pnpm --filter ./apps/wallow-auth test 2>&1 | tail -20
```

Expected: `IDENTICAL`, and wallow-auth's suite green. If `SHARED_SUBDIRS` needs a different
allowlist per app, that is a real divergence — stop and reconcile rather than papering over it.

**Step 10: Commit**

```bash
git add apps/wallow-web/src/zone-dag.test.ts apps/wallow-auth/src/zone-dag.test.ts
git commit -m "test: derive the zone DAG's zones from tsconfig paths"
```

---

## Task 8: Update the documentation that describes the deleted machinery

Four documents describe `aliases.ts` and `alias-map.test.ts` as current architecture. Leaving them
is exactly the drift this work exists to remove.

**Files:**
- Modify: `apps/CLAUDE.md:36` (and the surrounding sentence)
- Modify: `apps/wallow-web/README.md:63-64`
- Modify: `docs/development/frontend-setup.md:72`, `:78` (tree), `:127-131`

**Step 1: `apps/CLAUDE.md`** — replace the sentence

> Cross-zone imports are spelled as aliases — `@app/*`, `@features/<name>`, `@shared/*` —
> declared once in the app's `aliases.ts` and mirrored by `vite.config.ts`, `vitest.config.ts` and
> `tsconfig.json`. Relative specifiers stay correct _within_ a zone. Both halves are enforced by
> specs, not convention: `src/alias-map.test.ts` pins the three mirrors in agreement, and
> `src/zone-dag.test.ts` resolves every specifier and judges the edge.

with

> Cross-zone imports are spelled as aliases — `@app/*`, `@features/<name>`, `@shared/*` —
> declared **once**, in the app's `tsconfig.json` `paths`. Vite reads it natively
> (`resolve.tsconfigPaths: true`), vitest reads it per project, and `src/zone-dag.test.ts` reads it
> to derive which prefixes it polices — so adding a zone is that one edit. Relative specifiers stay
> correct _within_ a zone. The DAG itself is enforced by a spec, not convention:
> `src/zone-dag.test.ts` resolves every specifier against its importer's real directory and judges
> the edge.

**Step 2: `apps/wallow-web/README.md`** — replace lines 62-64's

> The alias map lives in `aliases.ts` and is mirrored by `vite.config.ts`, `vitest.config.ts` and
> `tsconfig.json`; `src/alias-map.test.ts` pins the three in agreement.

with

> The alias map lives in `tsconfig.json` `paths` and nowhere else — Vite and vitest both read it
> through `resolve.tsconfigPaths`, and `src/zone-dag.test.ts` reads it to know which zones exist.

**Step 3: `docs/development/frontend-setup.md`** — three edits:

- line 72: delete the `│   ├── alias-map.test.ts           # Pins vite/vitest/tsconfig to aliases.ts` row
- line 78 (`apps/wallow-web/` tree): delete the `├── aliases.ts` row
- lines 127-131: replace

  > The alias map is declared once per app in `aliases.ts` and mirrored by `vite.config.ts`,
  > `vitest.config.ts` and `tsconfig.json`. Both halves are enforced by specs rather than
  > convention: `src/alias-map.test.ts` pins the three mirrors in agreement, and
  > `src/zone-dag.test.ts` resolves every specifier […]

  with

  > The alias map is declared once per app, in `tsconfig.json` `paths`. Vite resolves against it
  > natively (`resolve.tsconfigPaths: true`), vitest repeats that option inside each
  > `test.projects` entry — a root-level `resolve` is not inherited — and `src/zone-dag.test.ts`
  > reads the same file to derive the zone list it polices. Adding a zone is one edit, and the DAG
  > guard picks it up immediately. The DAG is enforced by a spec rather than convention:
  > `src/zone-dag.test.ts` resolves every specifier […]

**Step 4: Verify the docs site still builds**

```bash
docfx docfx.json 2>&1 | tail -20
```

Expected: build succeeds, no broken-link warnings for the rows you deleted.

**Step 5: Commit**

```bash
git add apps/CLAUDE.md apps/wallow-web/README.md docs/development/frontend-setup.md
git commit -m "docs: describe tsconfig paths as the single alias declaration site"
```

---

## Task 9: Full quality gate

**Step 1: Run it without masking the exit code**

```bash
pnpm check > /tmp/check.log 2>&1; echo "EXIT=$?"; tail -60 /tmp/check.log
```

Expected: `EXIT=0`. Read the `EXIT=` line, not the tail — a green-looking tail above a non-zero
exit is the exact trap this spelling exists to avoid.

**Step 2: Fix anything red, then re-run.** `pnpm check` runs format:check, lint, typecheck, test,
build and check:exports.

**Step 3: Commit any formatting the gate produced**

```bash
pnpm format
git status --short
```

Commit only if `oxfmt` actually changed something.

---

## Task 10: E2E — the coverage the spike did not have

The alias spikes ran zero Playwright. A resolution change that survives build and boot can still
break a route at runtime, and E2E is the only thing that would show it.

**Step 1: Backend-free reachability first — fastest signal**

```bash
pnpm --filter ./apps/wallow-auth exec playwright test routes.spec.ts
pnpm --filter ./apps/wallow-web exec playwright test routes.spec.ts
```

Expected: green. These need no backend — every route renders (<400) and reaches hydration
(`[data-app-ready='true']`).

**Step 2: The full backend-dependent runner**

```bash
./scripts/e2e.sh
```

Expected: all three suites green — wallow-auth, wallow-web, and the wallow-web cross-app login
journey. This brings up `docker/docker-compose.test.yml` (infra + API + seeder + wallow-web) and
tears it down.

If the seeder appears to succeed but login fails on a missing `admin@wallow.dev`, that is the
known stale-dev-DB gotcha (Wallow-wd6n): admin bootstrap is skipped when *any* user already
exists. Reset the test DB rather than debugging the alias change.

**Step 3: Commit**

Nothing to commit — verification only.

---

## Task 11: Land it

**Step 1: Review the whole diff**

```bash
git log --oneline main..HEAD
git diff main --stat
```

Expected shape — around **9 insertions / 118 deletions** for the alias swap across 5 files per
app, plus the `zone-dag.test.ts` rewrite and the doc edits. Four files deleted total
(`aliases.ts` ×2, `alias-map.test.ts` ×2).

**Step 2: Sanity-check the outcome against the goal**

Adding a zone used to be 8 file edits (`aliases.ts`, `tsconfig.json`, `alias-map.test.ts:44`, and
`zone-dag.test.ts` in two places — ×2 apps). Confirm it is now 1 per app:

```bash
grep -rn "@app/\|@features/\|@shared/" apps/wallow-web/tsconfig.json apps/wallow-web/vite.config.ts apps/wallow-web/vitest.config.ts apps/wallow-web/src/zone-dag.test.ts
```

Expected: hits **only** in `tsconfig.json`. Any literal zone prefix left in a config or the DAG
spec is a thread this refactor was supposed to cut.

**Step 3: Push**

```bash
git pull --rebase && bd dolt push && git push
git status   # must read "up to date with origin"
```

Work is not complete until `git push` succeeds.

---

## Out of scope — stated so it is not mistaken for done

- **The `importProtection` / `redis` bug** (Task 0) is filed, not fixed. It is independent of and
  more severe than this refactor.
- **The three-zone layout itself** is unchanged. The audit found no authoritative TanStack
  position on large-app structure — every official example declares a single `./src/*` alias, and
  `start-large` is a route-count stress test, not an architecture demo. Upstream neither endorses
  nor contradicts the design, so it stays.
- **`apps/examples/minimal-app`** is deliberately flat, has no zones, and is untouched.
- **`packages/*`** declare no `paths` and are untouched.

## What this deletes, and why that is the whole point

| Artifact | Why it existed | Why it can go |
| --- | --- | --- |
| `apps/*/aliases.ts` | A JS module both build configs could import, so they could not disagree | Vite reads `tsconfig.json` directly; there is no second copy to disagree with |
| `apps/*/src/alias-map.test.ts` | JSON cannot import a module, so the tsconfig mirror needed a lock | There is no mirror |
| 4× `resolve.alias` zone splices | The only way to get the map into Vite/vitest | `resolve.tsconfigPaths: true` |
| Hard-coded prefixes in `targetOf` | Nothing to derive them from | `tsconfig.json` `paths`, read at spec time |

One correction worth recording, because the current code asserts the opposite: `aliases.ts`'s
trailing-slash rationale — that a bare `@app` key would swallow `@application` — is **wrong**.
Vite matches string aliases exact-or-path-segment, not by prefix. Disproved by a real build. The
trailing-slash keying was solving a problem that does not exist, and goes with the file.
