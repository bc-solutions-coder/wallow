**status: superseded**

# Shared Packages and App Zones Implementation Plan

> **SUPERSEDED — do not execute this file.** It was split in two, because its halves want
> different execution models. Nothing was dropped; the two files below are this file's content
> plus per-skill framing.
>
> - **Slice 0** (the three-zone restructure) → `docs/plans/2026-07-30/1346-slice-0-app-zones-restructure.md`,
>   run with `superpowers:executing-plans`. It adds a batching table, because the default
>   three-tasks-per-batch cadence checkpoints inside the interval where an app is deliberately in
>   pieces.
> - **Slices 1–5** (the package extractions) → `docs/plans/2026-07-30/1347-shared-packages-extraction.md`,
>   run with `/team-build`. It adds the bead decomposition, the dependency graph, the
>   already-decided list, and the open blockers.
>
> Kept for provenance only — the verified-facts list and the reasoning behind each task live in
> the Slice 0 file now.

**Goal:** Restructure both React apps into three zones (`app/` / `features/` / `shared/`) behind
path aliases and a spec-enforced import DAG, then extract four shared packages (`utils`, `config`,
`logger`, `navigation`) so a fork inherits them instead of re-deriving them.

**Architecture:** Slice 0 lands the structural change across both apps in **one branch, one PR** —
aliases, zone folders, per-feature public barrels, and the zone DAG. Slices 1–5 then run vertically,
one package each: extract, rehome its helpers, migrate both apps, document, test. Every
hand-maintained mirror (the alias map's tsconfig copy) is pinned by a spec, the
`docker-workspace-copies.test.ts` idiom this repo already uses.

**Tech Stack:** pnpm workspace, Node 24, TanStack Start + Vite 8 (Rolldown), vitest two-project
split (node + real headless Chromium), oxlint 1.74, oxfmt.

**Design doc:** `docs/plans/2026-07-30/1201-shared-packages-and-app-zones-design.md`

---

## Revision note — what changed after review, and why

Three reviewers went over the first draft. Items 1–4 are the corrections that changed the plan's
*shape* rather than its details; item 5 is a later scope addition, not a review finding.

1. **Enforcement moved from oxlint to a spec.** See "Why the DAG is a spec" below. This reverses the
   earlier "re-declare in oxlint overrides" decision; the evidence for the reversal arrived after
   that call was made. `.oxlintrc.json` now needs **no change at all** in Slice 0.
2. **`tanstackStart` needs `srcDirectory`, not `router.routesDirectory`.** The original Task 0.4
   could not build. See Facts 1–3.
3. **`bff.ts` goes to `app/lib/`, not `shared/lib/`** — the design was right and the draft was
   wrong. Server-only code must not sit at the bottom of the DAG where every zone may legally reach
   it.
4. **The fork-merge cost is descoped — there are no forks yet.** An earlier revision priced this
   heavily (pure-rename commits, a two-step `test/` merge, a fork upgrade runbook, `feat!:`). All of
   that protects forks that diverged *before* this lands; a fork created afterward clones the new
   shape and never migrates. With none in existence, that work has no audience. The moves are still
   split from the import rewrites — but for **diff reviewability**, not rename detection, and the
   ceremony around it is gone. Slice 0 is a `refactor:`, not a `feat!:`: nothing published changes,
   since the restructure is confined to `apps/`.
5. **pnpm catalogs land in Slice 0, as Task 0.0.** Four TanStack packages are exact-pinned by hand
   across three app manifests (plus `packages/testing`), which `apps/CLAUDE.md` mandates and which
   makes every patch bump a lockstep manual edit. Doing it first means `packages/navigation` can
   declare `zustand: catalog:react` from birth in Slice 4 rather than being migrated afterward.

---

## Facts verified while writing and revising this plan — do not re-derive

1. **`@tanstack/start-plugin-core@1.171.24` resolves router paths relative to `srcDirectory`.**
   `dist/esm/schema.js:48-49` is `path.resolve(root, srcDirectory, rawRouterOptions.routesDirectory ?? "routes")`,
   with `srcDirectory` defaulting to `"src"` (`schema.js:141`). Passing `routesDirectory: "src/app/routes"`
   yields `<app>/src/src/app/routes`.
2. **The router entry is `required: true`** (`dist/esm/planning.js:68`) and is resolved from
   `srcDirectory` via `dist/esm/resolve-entries.js:19-32`, which throws
   `Could not resolve entry for router entry: ./router in <dir>`. Moving `src/router.tsx` without
   telling the plugin is a hard build failure, not a warning.
3. **Import protection's importer scope IS `srcDirectory` when no `include` is given.**
   `dist/esm/import-protection/adapterUtils.js:23`. The plugin also hard-codes
   `` `${config.srcDirectory}/routes` `` for graph-entry seeding (`vite/import-protection-plugin/plugin.js:608,855`).
   Both facts constrain how `srcDirectory` may be set.
4. **oxlint 1.74 ships 33 `import/*` rules and `import/no-restricted-paths` is NOT one of them.**
   Verified against `node_modules/oxlint/configuration_schema.json`. There is no rule that resolves
   a specifier before judging it.
5. **Intra-zone `../../` imports are normal and legitimate.** Zones have internal depth:
   `routes/dashboard/route.test.tsx` imports `../../router`, `routes/bff/$.ts` does
   `await import("../../lib/bff")`. Eight wallow-web modules do this today. A `"../../**"` glob ban
   would reject every one of them. **This is why the DAG cannot be a lint rule** — see below.
6. **An oxlint `overrides` entry REPLACES the root rule's options rather than merging them**, and
   **later overrides win**. Both verified empirically. Still true, still worth knowing — it is why
   the abandoned lint approach would have cost six near-verbatim copies of a 65-line block.
7. **`@features/*/**` is the barrel-only glob** — it matches `@features/apps/api` but not
   `@features/apps`, because `*` does not cross `/`. Retained for the alias-shape assertions in the
   DAG spec.
8. **Screenshot baselines do not churn.** `__screenshots__/` is gitignored (`.gitignore:49`) and
   untracked, and each directory is a sibling of its spec — moving a directory wholesale preserves
   every path. Do not plan a regeneration step.
9. **`apps/*/tsconfig.json` contains `//` comments.** Any spec parsing it must strip comments before
   `JSON.parse`.
10. **`.lintstagedrc.mjs:67` runs a repo-wide `pnpm typecheck` on every commit touching `*.{ts,tsx}`.**
    Intermediate commits inside Slice 0 leave the tree un-typecheckable by construction, so they
    **must** use `git commit --no-verify`. The task steps below say so where it applies; the final
    task runs the full gate.
11. **`apps/wallow-auth/vite.config.ts:47-70` already has `resolve.alias` as an ARRAY** with the two
    anchored `use-sync-external-store/shim` regexes, exactly like wallow-web. Append to it; replacing
    it with an object reintroduces the double-React SSR bug those regexes exist to prevent.
12. **`pnpm-workspace.yaml` has `overrides` but no `catalog`/`catalogs` key today**, so Task 0.0 adds
    the mechanism rather than extending it. **`.github/workflows/sdk-publish.yml:71` is
    `pnpm publish --no-git-checks`**, which resolves `catalog:` at pack time — this is what makes the
    protocol publish-safe, and it is a standing constraint on that workflow, not a one-time check.
13. **`zustand` has exactly one importer in the workspace** (`apps/wallow-web`). **`packages/auth`
    peers `react@^19.0.0` while dev-depending `^19.2.7`, and `packages/ui` peers
    `@tanstack/react-router@^1.170.18` where the apps pin `1.170.18` exactly.** Both are correct
    library practice — a wider peer floor than the dev pin — **not drift**, and Task 0.0 must not
    "fix" them into uniformity.
14. **`features-api-seam.test.ts`'s `moduleSpecifiers()` misses dynamic imports.** It is two regexes
    — `/\bfrom\s+"([^"]+)"/gu` and `/^\s*import\s+"([^"]+)"/gmu` — and `await import("…")` matches
    neither. Its doc comment ("bare side-effect imports alike") reads as if it did. Task 0.13 Step 1
    extends it for the DAG spec; Task 0.11 edit 4 back-ports the extension.
15. **A CO-MOVE leaves its specifier untouched.** `routes/`, `lib/bff.ts`, `styles.css`,
    `router.tsx` and `routeTree.gen.ts` all land under `src/app/` together, so every relative
    specifier *between* them keeps its exact hop count. Rewrite only when the two ends move by
    different amounts. An earlier revision "corrected" `../lib/bff` → `../../lib/bff` and
    `../styles.css` → `./styles.css`; both resolve to paths that do not exist.
16. **The vitest two-project split is keyed on file EXTENSION, not on `nodeTsxSpecs`.**
    `packages/testing/src/vitest-projects.ts`: node includes `["src/**/*.test.ts", ...nodeTsxSpecs]`,
    browser includes `["src/**/*.test.tsx"]` minus `nodeTsxSpecs`. A `.test.ts` spec can therefore
    never run in Chromium — `nodeTsxSpecs` only pushes `.tsx` specs toward node, never the reverse.
    A spec needing the browser must be named `.test.tsx`, and then cannot use `node:fs`.

---

## Why the DAG is a spec, not a lint rule

The rule we want is *"a relative specifier may not resolve outside its own zone."* That is a
question about where a path **resolves**, and oxlint's `no-restricted-imports` globs the specifier
**string**. The two are not the same, and the gap is not academic:

- `../../router` from `src/app/routes/dashboard/route.test.tsx` resolves to `src/app/router` —
  **inside** the zone. A `"../../**"` ban rejects it.
- `../../../vite.config` from a config-guard spec resolves **outside `src/` entirely** and is
  legitimate.
- 19 wallow-auth feature specs import `../../../routes/<name>` and mount the real route to exercise
  a component against its actual `validateSearch` contract. That is the right test, and it is a
  genuine `features → app` edge.

Every one of these would need its own exemption, and — because overrides *replace* rather than merge
— each exemption costs another full copy of the root's package-ban block. Fact 4 says there is no
oxlint rule that resolves paths, so there is no lint-side fix.

A spec resolves the specifier with `path.resolve`, classifies the resulting zone, and judges the
edge exactly. It also carries *why* each exemption exists in a comment, which a JSON config cannot.
This repo already has the idiom: `apps/wallow-auth/src/features-api-seam.test.ts` parses module
specifiers (`moduleSpecifiers()` at :329-336 — static and bare side-effect; **dynamic imports are a
gap, see Task 0.13 Step 1**), scopes by directory (`boundaryScope()` at :339-344), and is exactly
this shape of guard.

**Net effect on `.oxlintrc.json` in Slice 0: no change.** It keeps doing what it is good at —
banning *package* specifiers — and stops growing.

---

## Slice 0 — three-zone restructure, both apps

### Target layout (identical in both apps)

```
apps/<app>/
  aliases.ts                     <- NEW: the alias map, plain data
  src/
    app/                         <- everything the host runtime owns
      routes/
      router.tsx
      start.ts
      routeTree.gen.ts
      styles.css                 <- MOVED here (see below)
      lib/bff.ts                 <- wallow-web only; server-only code lives in app/
    features/                    <- unchanged in place; each gains index.ts
      <feature>/index.ts         <- NEW: public contract barrel
    shared/
      components/                <- from src/components/
      lib/                       <- from src/lib/  (minus bff.ts)
      stores/                    <- from src/stores/       (wallow-web only)
      testing/                   <- from src/testing/ AND src/test/ (merged; wallow-web)
    vite-env.d.ts                <- stays
    *.test.ts(x)                 <- app-wide policy specs stay at src/ root
```

**Only three FOLDERS at `src/` root.** Root-level *files* are `vite-env.d.ts` and the app-wide policy
specs, which are guards over the whole app rather than modules of any zone.

Three deliberate departures from the design doc, each with a reason:

- **`styles.css` moves into `src/app/`.** Both `__root.tsx` files do a bare side-effect
  `import "../styles.css"`; from `src/app/routes/` that would have to climb out of the zone. The
  stylesheet is the app shell's, not shared. This also falls out of `srcDirectory: "src/app"`.
- **`bff.ts` moves to `src/app/lib/bff.ts`, NOT `shared/lib/`.** It imports `redis` (line 30). Putting
  server-only code at the bottom of the DAG makes it legally importable by every feature and every
  shared module — the DAG would sanction the one import that must never happen. Its only consumers
  are three server routes (`routes/health.ts`, `routes/bff/$.ts`, `routes/api/$.ts`), all via
  `await import(…)`, all in the `app` zone already. **General rule to add to the design: server-only
  modules live in `app/`, never in `shared/`.**
- **Two config-guard specs are promoted to `src/` root**: `lib/brand-assets.test.ts` (both apps) and
  `lib/base-path-wiring.test.ts` (wallow-auth). Neither tests a `shared/lib` module's behaviour —
  both assert that the app's `vite.config.ts` / `nitro` / `router` wiring carries a particular
  setting, which is precisely the app-wide-policy-spec category. From `src/` root their
  `../../vite.config` becomes `../vite.config`, one level, and they sit beside `styling.test.ts` and
  `docker-workspace-copies.test.ts` where they belong.

### Barrel contract

The design said barrels export "entry points and types". The actual route surface is three
categories:

1. **route-mounted components**,
2. **the loader query options routes prefetch** (re-exported from `./api`) — six wallow-web routes
   and one wallow-auth route need this,
3. **the public values a route's *configuration* needs** — types, search-param schemas, predicates,
   constants. (`routes/login.tsx:11` imports `isPasswordResetMessage` and `PASSWORD_RESET_MESSAGE`
   and uses the predicate inside `validateSearch` at :113. That is a function, not a type — which is
   why "types and constants" was too narrow a name for this category.)

Everything else — internal components, `api.ts` itself, helpers — stays unexported. Route guards are
**not** a fourth category: `ensureCurrentUser` / `requireAuth` / `isAdmin` all come from
`@bc-solutions-coder/auth`. There is no `loaderDeps` in either app.

### Task ordering

`0.0` (pnpm catalogs), `0.1 → 0.6` (wallow-web structural), `0.7 → 0.12` (wallow-auth structural),
then `0.13` (the DAG spec), `0.14` (docs), `0.15` (full gate).

**Task 0.0 goes first and touches no source file.** It is a manifest-only change, independent of the
restructure, and landing it at the head of the branch keeps it a readable standalone commit instead
of a rider on a 130-file rename. It also means `packages/navigation` can declare `zustand: catalog:`
from birth in Slice 4.

**The DAG spec lands LAST, deliberately.** The first draft landed enforcement seven tasks before the
imports it governs were finished, which meant `pnpm lint` was red through the whole middle of the
slice — exactly when you most want it green to catch a mistyped path.

---

### Task 0.0: adopt pnpm catalogs for the shared version pins

**Files:**
- Modify: `pnpm-workspace.yaml` (has `overrides` today, no `catalog:` entry)
- Modify: `apps/wallow-web/package.json`, `apps/wallow-auth/package.json`,
  `apps/examples/minimal-app/package.json`, `packages/testing/package.json`,
  `packages/forms/package.json`, `packages/ui/package.json`, `packages/auth/package.json`,
  `packages/query/package.json`, `packages/sdk/package.json`
- Modify: `apps/CLAUDE.md` (the "pins … exactly, with no `^`" sentence)

**Why.** `apps/CLAUDE.md:29-31` requires three TanStack packages be pinned exactly in every app. That
is enforced by hand today across three app manifests plus `packages/testing`, so a single patch bump
is a four-file lockstep edit that nothing verifies. A catalog makes the pin one line and makes drift
a lockfile error rather than a code review miss.

**Two named catalogs, because the two groups have genuinely different semantics** — one exact-pinned
for app-side runtime singletons, one caret-ranged for library peer declarations. Do not collapse
them: `packages/ui` peering `@tanstack/react-router@^1.170.18` while the apps pin `1.170.18` exactly
is **correct library practice, not drift**, and a single catalog would erase that distinction.

**Step 1: Add the catalogs to `pnpm-workspace.yaml`**

```yaml
catalogs:
  # Exact pins. App-side runtime singletons: two copies at runtime is a broken app,
  # not a slow one. Bump these together, deliberately, in one commit.
  start:
    "@tanstack/react-start": 1.168.32
    "@tanstack/react-router": 1.170.18
    "@tanstack/react-router-ssr-query": 1.167.1
  # Ranges. What packages/* declare as peers and use in devDependencies.
  react:
    react: ^19.2.7
    react-dom: ^19.2.7
    "@tanstack/react-form": ^1.33.2
    "@tanstack/react-query": ^5.101.2
    zustand: ^5.0.8
```

`zustand` has exactly one importer today (`apps/wallow-web`). It is listed anyway so Slice 4's
`packages/navigation` declares `zustand: catalog:react` from birth — that package is the reason a
second importer appears at all, and a zustand store's identity depends on resolution the same way a
`QueryClient`'s does.

**Step 2: Replace the literals with `catalog:` references**

`"@tanstack/react-start": "1.168.32"` → `"@tanstack/react-start": "catalog:start"`, and so on. Leave
`workspace:*` alone. Leave `packages/auth`'s `react: ^19.0.0` **peer** alone — a deliberately wider
floor than its devDependency, which is what a library peer range is for; its devDependency becomes
`catalog:react`.

**Step 3: Reinstall and confirm nothing resolved differently**

```bash
pnpm install
git diff --stat pnpm-lock.yaml
```

Expected: the lockfile records the catalog indirection, and **no package's resolved version
changes**. If a resolved version moves, a manifest was drifted before this task and you have just
silently upgraded it — stop and surface which one rather than absorbing it into this commit.

**Step 4: Update the stale instruction in `apps/CLAUDE.md`**

The sentence "pins `@tanstack/react-start`/`react-router`/`react-router-ssr-query` exactly, with no
`^`" is now wrong in its mechanism. Replace with: pins come from the `start` catalog in
`pnpm-workspace.yaml`; app manifests say `catalog:start`, and the exact version is edited in one
place.

**Step 5: Verify the published-package path still works**

`catalog:` is a workspace protocol: it must be resolved at pack time or consumers get an
uninstallable manifest. `.github/workflows/sdk-publish.yml:71` runs `pnpm publish --no-git-checks`,
which resolves it — same as `workspace:*`, which the SDK already relies on. **This is a live
constraint on that workflow, not a one-time check:** switching it to `npm publish` would break both
protocols. Record it as a comment in the workflow next to the publish step.

```bash
pnpm --filter @bc-solutions-coder/sdk build && pnpm check:exports
```

**Step 6: Commit**

```bash
git add pnpm-workspace.yaml pnpm-lock.yaml apps/*/package.json apps/examples/*/package.json \
        packages/*/package.json apps/CLAUDE.md .github/workflows/sdk-publish.yml
git commit -m "build: pin shared frontend versions through pnpm catalogs"
```

---

### Task 0.1: the alias map and its pin test (wallow-web)

**Files:**
- Create: `apps/wallow-web/aliases.ts`
- Create: `apps/wallow-web/src/alias-map.test.ts`
- Modify: `apps/wallow-web/tsconfig.json`

**Step 1: Write the failing test**

Create `apps/wallow-web/src/alias-map.test.ts`:

```ts
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { aliasDirs, resolveAlias } from "../aliases";

/**
 * `aliases.ts` is the single source for this app's three zone aliases, and
 * `vite.config.ts` / `vitest.config.ts` both import it — so those two cannot
 * drift. `tsconfig.json` is JSON and cannot import anything, so it is a
 * hand-maintained mirror, and this spec is its lock.
 *
 * Same idiom as `docker-workspace-copies.test.ts`: read both artifacts off disk
 * and assert the mirror, rather than abstracting the duplication into shared
 * build machinery (which would couple every app to a build package).
 *
 * Node project: reads files, mounts nothing.
 */
const appDir: string = resolve(dirname(fileURLToPath(import.meta.url)), "..");

/** `tsconfig.json` carries `//` comments — strip them before parsing. */
function readTsconfigPaths(): Record<string, string[]> {
  const text: string = readFileSync(resolve(appDir, "tsconfig.json"), "utf8").replaceAll(
    /^\s*\/\/.*$/gmu,
    "",
  );
  const config = JSON.parse(text) as { compilerOptions?: { paths?: Record<string, string[]> } };

  return config.compilerOptions?.paths ?? {};
}

/**
 * The two build configs must actually CONSUME the map, or it is decoration and
 * the mirror it pins is meaningless.
 */
function readsAliasModule(file: string): boolean {
  return /from\s+"\.\/aliases"/u.test(readFileSync(resolve(appDir, file), "utf8"));
}

describe("the zone alias map", () => {
  it("declares exactly the three zones", () => {
    expect(Object.keys(aliasDirs).toSorted()).toEqual(["@app", "@features", "@shared"]);
  });

  it("is mirrored entry-for-entry by tsconfig.json paths", () => {
    const paths: Record<string, string[]> = readTsconfigPaths();

    expect(Object.keys(paths).toSorted()).toEqual(
      Object.keys(aliasDirs)
        .map((key): string => `${key}/*`)
        .toSorted(),
    );

    for (const [key, dir] of Object.entries(aliasDirs)) {
      expect(paths[`${key}/*`], `tsconfig paths has no ${key}/*`).toEqual([`./${dir}/*`]);
    }
  });

  it("resolves each alias to an absolute directory inside this app", () => {
    for (const [key, dir] of Object.entries(aliasDirs)) {
      expect(resolveAlias[`${key}/`]).toBe(`${resolve(appDir, dir)}/`);
    }
  });

  it.each(["vite.config.ts", "vitest.config.ts"])("%s imports the alias map", (file: string) => {
    expect(readsAliasModule(file)).toBe(true);
  });
});
```

**Step 2: Run it and watch it fail**

```bash
pnpm --filter ./apps/wallow-web exec vitest run src/alias-map.test.ts
```

Expected: FAIL — `Cannot find module '../aliases'`.

**Step 3: Write `apps/wallow-web/aliases.ts`**

```ts
import { fileURLToPath } from "node:url";

/**
 * The three zone aliases, as plain data.
 *
 * This app's `vite.config.ts` and `vitest.config.ts` both import this module and
 * derive `resolve.alias` from it, so the runtime and the test runner cannot
 * disagree. `tsconfig.json` cannot import it — JSON — so `src/alias-map.test.ts`
 * pins its `compilerOptions.paths` to this map instead.
 *
 * Deliberately NOT a shared build-config package: a preset would mean deep-rooted
 * build files coupling every app to a package, for three lines of data.
 *
 * `@app` maps to `src/app`, not `src`. An `@app/* -> src/*` entry would overlap
 * the other two and give two spellings for the same module.
 */
export const aliasDirs = {
  "@app": "src/app",
  "@features": "src/features",
  "@shared": "src/shared",
} as const;

/**
 * Vite/vitest `resolve.alias`, keyed WITH the trailing slash.
 *
 * Vite's object-form alias matches by prefix, so a bare `@app` key would also
 * swallow a future `@application`. `@app/` cannot.
 */
export const resolveAlias: Record<string, string> = Object.fromEntries(
  Object.entries(aliasDirs).map(([key, dir]): [string, string] => [
    `${key}/`,
    `${fileURLToPath(new URL(dir, import.meta.url))}/`,
  ]),
);
```

**Step 4: Add the `paths` block to `apps/wallow-web/tsconfig.json`**

Inside `compilerOptions`, after `"types": ["node"]`:

```jsonc
    // Mirrored from `aliases.ts` — JSON cannot import it, so `src/alias-map.test.ts`
    // pins the two together. `moduleResolution: "Bundler"` resolves these relative
    // to this file, so no `baseUrl` is needed.
    "paths": {
      "@app/*": ["./src/app/*"],
      "@features/*": ["./src/features/*"],
      "@shared/*": ["./src/shared/*"]
    }
```

Also extend `include` to pick up the new root module:

```jsonc
  "include": ["src/**/*.ts", "src/**/*.tsx", "aliases.ts", "vite.config.ts", "vitest.config.ts"]
```

**Step 5: Run the test — expect PASS** (the last two cases fail until Task 0.2; that is fine, they
are the next task's red.)

**Step 6: Commit**

```bash
git add apps/wallow-web/aliases.ts apps/wallow-web/src/alias-map.test.ts apps/wallow-web/tsconfig.json
git commit --no-verify -m "feat(web): add the zone alias map and its tsconfig pin test"
```

---

### Task 0.2: wire the alias map into Vite and vitest (wallow-web)

**Files:**
- Modify: `apps/wallow-web/vite.config.ts`
- Modify: `apps/wallow-web/vitest.config.ts`

**Step 1: Extend `vite.config.ts`**

`resolve.alias` there is an ARRAY (two anchored-regex entries for the
`use-sync-external-store/shim` hazard). Vite accepts array form only, or object form only — so
convert the map to array entries and **append**, preserving the regexes and their order. Add at the
top:

```ts
import { resolveAlias } from "./aliases";
```

and inside `resolve.alias`, after the two existing regex entries:

```ts
      // The three zone aliases, from the app-local map that `vitest.config.ts`
      // and `tsconfig.json` also mirror. Appended AFTER the shim regexes so the
      // anchored rewrites still match first.
      ...Object.entries(resolveAlias).map(([find, replacement]) => ({ find, replacement })),
```

**Step 2: Extend `vitest.config.ts`**

`createVitestProjects` returns `{ node, browser }`; wallow-web already spreads `browser` to add a
`resolve.alias`. Spread both:

```ts
import { resolveAlias } from "./aliases";

// …

export default defineConfig({
  ssr: {
    noExternal: ["@bc-solutions-coder/query", "@bc-solutions-coder/auth"],
  },
  test: {
    projects: [
      { ...node, resolve: { alias: resolveAlias } },
      {
        ...browser,
        resolve: { alias: { ...resolveAlias, "node:async_hooks": nodeAsyncHooksShim } },
      },
    ],
  },
});
```

**Step 3: Prove the wiring resolves, before any file moves**

Create a throwaway module, import it through the alias, run it, then delete **the whole scratch
tree**. A wiring change only exercised after the big move gives you two failure sources at once.

```bash
cd apps/wallow-web
mkdir -p src/shared/lib
printf 'export const ALIAS_WIRED = true;\n' > src/shared/lib/alias-probe.ts
printf 'import { expect, it } from "vitest";\nimport { ALIAS_WIRED } from "@shared/lib/alias-probe";\nit("resolves", () => { expect(ALIAS_WIRED).toBe(true); });\n' \
  > src/alias-probe.test.ts
pnpm --filter ./apps/wallow-web exec vitest run src/alias-probe.test.ts
```

Expected: PASS on the node project.

**Then clean up completely:**

```bash
rm src/alias-probe.test.ts
rm -r src/shared            # NOT rmdir — the probe left src/shared/lib behind
test -e src/shared && echo "FAIL: src/shared still exists" || echo "clean"
```

> **This cleanup is load-bearing.** `git mv src/lib src/shared/lib` with `src/shared/lib` already
> present lands the files at `src/shared/lib/lib/` and **exits 0** — a silent failure. Verified in a
> scratch repo.

**Step 4: Run the alias-map spec — now fully green — and commit**

```bash
pnpm --filter ./apps/wallow-web exec vitest run src/alias-map.test.ts
git add apps/wallow-web/vite.config.ts apps/wallow-web/vitest.config.ts
git commit --no-verify -m "feat(web): resolve the zone aliases in vite and vitest"
```

---

### Task 0.3: move directories into `src/shared/` and `src/app/` (wallow-web)

> **Moves land separately from import rewrites (Task 0.5) — for reviewability.** A 130-file diff
> that is *only* renames is skimmable; the same diff with rewrites folded in is not, and it hides
> a stray content edit inside the noise. An earlier revision demanded a *strictly* pure-rename
> commit for fork rename-detection; that rationale is retired (no forks exist), so a stray edit
> here is untidy rather than a defect. Keep them separate anyway — it costs one commit.

**Step 1: Move**

```bash
cd apps/wallow-web
mkdir -p src/shared src/app
git mv src/components     src/shared/components
git mv src/lib            src/shared/lib
git mv src/stores         src/shared/stores
git mv src/testing        src/shared/testing
# src/test/ merges straight into shared/testing/ — one destination, four files
git mv src/test/catalog-select.ts  src/shared/testing/catalog-select.ts
git mv src/test/harness-routes.ts  src/shared/testing/harness-routes.ts
git mv src/test/invalidation.ts    src/shared/testing/invalidation.ts
git mv src/test/style-contract.ts  src/shared/testing/style-contract.ts
rmdir src/test
git mv src/routes         src/app/routes
git mv src/router.tsx     src/app/router.tsx
git mv src/start.ts       src/app/start.ts
git mv src/routeTree.gen.ts src/app/routeTree.gen.ts
git mv src/styles.css     src/app/styles.css
mkdir -p src/app/lib
git mv src/shared/lib/bff.ts src/app/lib/bff.ts
# Config-guard specs are app-wide policy, not shared modules
git mv src/shared/lib/brand-assets.test.ts src/brand-assets.test.ts
```

> **`src/test/` and `src/testing/` merge into one directory here.** They are the same thing under
> two names — the split is historical. An earlier revision deferred this merge to a separate task
> because git's directory-rename detection declines to infer a rename when two sources converge on
> one destination, which would have handed a diverged fork a silent duplicate tree. No fork exists,
> so that cost is theoretical and the merge happens up front.
>
> `rmdir` is deliberate over `rm -r`: it **fails** if `src/test/` holds a file the four `git mv`
> lines above missed, rather than deleting it. If it errors, list what is left and move that too.
> Note the filename collision risk is nil — `src/testing/` has no file of any of these four names.

**Step 2: Verify nothing was left behind**

```bash
ls -1 src                                   # expect: app  features  shared  vite-env.d.ts  *.test.ts(x)
find src -type d -name __screenshots__ | sort   # every path starts src/shared/ or src/features/
find src -maxdepth 1 -type d | sort
```

**Step 3: Commit — renames only**

```bash
git add -A apps/wallow-web/src
# Informational, not a gate: anything not R-status is an unintended content edit. Look at it.
git status --short apps/wallow-web/src | grep -v '^R' || echo "renames only"
git commit --no-verify -m "refactor(web): move src into the app/features/shared zones"
```

The tree does not typecheck at this point. That is expected and is why `--no-verify` is here.

---

### Task 0.4: reconfigure the Start plugin for the new source root (wallow-web)

**Files:** `apps/wallow-web/vite.config.ts`

**This is the step that makes the app build again.** The first draft of this plan got it wrong;
see Facts 1–3.

**Step 1: Set `srcDirectory` — do NOT set `router.routesDirectory`**

```ts
    tanstackStart({
      // The three-zone layout puts everything the host runtime owns under
      // `src/app/`: routes, router.tsx, start.ts, the generated route tree and
      // styles.css. `srcDirectory` is the ONE knob that relocates all of them —
      // the plugin resolves `router.routesDirectory`, `router.generatedRouteTree`
      // and every entry (router, start, client, server) RELATIVE to it
      // (start-plugin-core schema.js:48-49, planning.js:54-95). Setting
      // `routesDirectory: "src/app/routes"` instead would resolve to
      // `src/src/app/routes`, and the router entry — which is `required: true` —
      // would still not be found, so the build would hard-fail.
      srcDirectory: "src/app",

      // MANDATORY alongside the line above, not optional. With no `include`, the
      // import-protection plugin uses `srcDirectory` itself as the importer scope
      // (import-protection/adapterUtils.js:23). Narrowing srcDirectory to
      // `src/app` would therefore silently stop enforcing the server-only /
      // client-bundle boundary for everything under `src/features/**` and
      // `src/shared/**` — i.e. this restructure would quietly disable the
      // protection that stops `app/lib/bff.ts`'s `redis` import from being pulled
      // into a client bundle.
      importProtection: { include: ["src/**"] },

      // Specs are co-located with the code they cover, so a spec under
      // `src/app/routes/` would otherwise be codegen'd in as a route.
      router: { routeFileIgnorePattern: String.raw`\.(test|spec)\.(ts|tsx)$` },
    }),
```

**Step 2: Pin both settings in the config-guard spec**

Add to `apps/wallow-web/src/brand-assets.test.ts` (which already imports `../vite.config`), or a new
`src/start-plugin-wiring.test.ts` if that file's charter feels wrong:

```ts
it("keeps import protection covering every zone, not just src/app", () => {
  // Regression guard for the srcDirectory narrowing above: dropping `include`
  // silently disables env-boundary enforcement for features/ and shared/.
  const text: string = readFileSync(resolve(appDir, "vite.config.ts"), "utf8");

  expect(text).toMatch(/srcDirectory:\s*"src\/app"/u);
  expect(text).toMatch(/importProtection:\s*\{\s*include:\s*\["src\/\*\*"\]\s*\}/u);
});
```

**Step 3: Update the other hard-coded path consumers**

```bash
cd apps/wallow-web
grep -rn 'src/routes\|src/router\|src/start\|src/testing\|src/test/\|routeTree.gen\|src/styles.css' \
  package.json Dockerfile vite.config.ts vitest.config.ts playwright*.config.ts
grep -rn 'apps/wallow-\|routeTree.gen' ../../.github/workflows/route-tree-drift.yml
```

Known hits that **must** be fixed:

| File | What | New value |
| --- | --- | --- |
| `vitest.config.ts` `nodeTsxSpecs` | `"src/routes/index.test.tsx"`, `"src/routes/dashboard/route.test.tsx"` | prefix with `src/app/` |
| `vitest.config.ts:~76` | `new URL("src/testing/node-async-hooks-browser-shim.ts", import.meta.url)` | `src/shared/testing/…` — **a string, not an import specifier; no import-shaped grep finds it** |
| `.github/workflows/route-tree-drift.yml:19-22,31-34,83-85` | `apps/wallow-*/src/routes/**` and `src/routeTree.gen.ts` | `apps/wallow-*/src/app/routes/**`, `src/app/routeTree.gen.ts` |

`.oxlintrc.json`'s `ignorePatterns` uses `**/routeTree.gen.ts` — path-independent, no change. The
`routeTree.gen.ts` exclusions in `query-facade.test.ts` and `shared-auth.test.ts` use
`.endsWith("routeTree.gen.ts")` and also survive the move.

**Step 4: Do NOT run `vite build` yet.** Imports are still broken until Task 0.5. Codegen is
verified in Task 0.5 Step 5.

**Step 5: Commit**

```bash
git add apps/wallow-web/vite.config.ts apps/wallow-web/vitest.config.ts apps/wallow-web/src \
        .github/workflows/route-tree-drift.yml
git commit --no-verify -m "build(web): point the start plugin at the src/app source root"
```

---

### Task 0.5: write the feature barrels and rewrite every import (wallow-web)

This is the task the first draft under-scoped by roughly 4×. **wallow-web alone has 131 relative
`../` specifiers under `routes/`, `features/`, `components/`, `lib/`, `stores/` and `test*/`.** It is
not a 2–5 minute step; budget accordingly and commit in the middle if it helps.

**Step 1: Write the barrel-coverage spec first**

Create `apps/wallow-web/src/feature-barrels.test.ts`:

```ts
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Every feature is a bounded context with ONE public entry: its `index.ts`.
 * The zone DAG bans deep imports into a feature from outside it, so a feature
 * without a barrel is a feature nothing can mount.
 *
 * The barrel is also why a feature module may not import `@features/<own name>`:
 * that is a cycle. Reach your own modules relatively.
 *
 * Node project: reads files, mounts nothing.
 */
const srcDir: string = dirname(fileURLToPath(import.meta.url));
const featuresDir: string = resolve(srcDir, "features");

function featureDirs(): readonly string[] {
  return readdirSync(featuresDir, { withFileTypes: true })
    .filter((entry): boolean => entry.isDirectory())
    .map((entry): string => entry.name)
    .toSorted();
}

describe("feature public contracts", () => {
  it("finds the features it is meant to cover", () => {
    expect(featureDirs().length).toBeGreaterThan(0);
  });

  it.each(featureDirs())("features/%s exposes an index.ts barrel", (feature: string) => {
    expect(existsSync(join(featuresDir, feature, "index.ts"))).toBe(true);
  });

  it.each(featureDirs())("features/%s's barrel is re-exports only", (feature: string) => {
    // A barrel with logic in it is a module pretending to be a contract. It also
    // makes the barrel un-tree-shakeable, which matters: every route that mounts
    // one component from a feature pulls the barrel's whole graph into its chunk.
    const code: string = readFileSync(join(featuresDir, feature, "index.ts"), "utf8")
      .replaceAll(/\/\*[\s\S]*?\*\//gu, "")
      .replaceAll(/^\s*\/\/.*$/gmu, "");

    expect(code, `features/${feature}/index.ts imports something`).not.toMatch(/^\s*import\s/mu);
    expect(code, `features/${feature}/index.ts uses export *`).not.toMatch(/export\s*\*/u);
  });

  it.each(featureDirs())("features/%s's barrel reaches only its own modules", (feature: string) => {
    const code: string = readFileSync(join(featuresDir, feature, "index.ts"), "utf8");
    const specifiers: readonly string[] = [...code.matchAll(/\bfrom\s+"([^"]+)"/gu)].map(
      (match): string => match[1] as string,
    );

    expect(specifiers.length, `features/${feature}/index.ts exports nothing`).toBeGreaterThan(0);

    for (const specifier of specifiers) {
      expect(specifier, `features/${feature}/index.ts reaches outside itself`).toMatch(/^\.\//u);
    }
  });
});
```

**Step 1b: the resolve case is a SECOND file, in the browser project**

Shape checks prove a barrel is a contract; only actually loading it proves it is a *true* one. A
barrel re-exporting a name its source does not export is a typecheck error — but only once something
imports it, which for a name no route happens to use yet may be never. That case has to load React
components, so it belongs in Chromium.

**It cannot live in the file above, and adjusting `nodeTsxSpecs` cannot move it.** The preset's
projects are keyed on extension (`packages/testing/src/vitest-projects.ts`): node is
`["src/**/*.test.ts", ...nodeTsxSpecs]` and browser is `["src/**/*.test.tsx"]` minus `nodeTsxSpecs`.
A `.test.ts` file is therefore **always** on node and can never reach the browser project —
`nodeTsxSpecs` only ever pushes `.tsx` specs the other way. Left as one file, the resolve case would
evaluate the whole feature graph (Base UI, `lucide-react`, the `@bc-solutions-coder/ui` subpaths)
under `environment: "node"`.

So split it. Create `apps/wallow-web/src/feature-barrels.browser.test.tsx`:

```tsx
import { describe, expect, it } from "vitest";

/**
 * The other half of `feature-barrels.test.ts`. That file does the SHAPE checks
 * off disk on the node project; this one loads each barrel for real, in
 * Chromium, because the modules behind it are React components.
 *
 * `import.meta.glob` rather than `readdirSync` + a dynamic template specifier:
 * this project has no `node:fs`, and a glob is statically analysable, so Vite
 * pre-bundles the barrels instead of discovering them mid-run (the reload that
 * flakes the browser project).
 */
const barrels: Record<string, () => Promise<Record<string, unknown>>> = import.meta.glob(
  "./features/*/index.ts",
);

describe("feature barrels load", () => {
  it("finds the barrels the node-side spec asserts exist", () => {
    // Without this, a glob that matched nothing would make every case below
    // vacuously absent and the file would pass green having tested nothing.
    expect(Object.keys(barrels).length).toBeGreaterThan(0);
  });

  it.each(Object.keys(barrels).toSorted())("%s resolves every name it exports", async (path) => {
    const module: Record<string, unknown> = await (
      barrels[path] as () => Promise<Record<string, unknown>>
    )();

    expect(Object.keys(module).length).toBeGreaterThan(0);
  });
});
```

Leave it out of `nodeTsxSpecs` — the default routing is already correct. The node-side count
assertion (`featureDirs().length > 0`) and the browser-side one above are deliberately duplicated:
each file must fail loudly on its own if its discovery mechanism finds nothing.

Task 0.11 reuses **both** files verbatim for wallow-auth; both discover features from disk, so
neither carries an app-specific list.

**Step 2: Run both — expect six failures on the missing barrels**

```bash
pnpm --filter ./apps/wallow-web exec vitest run src/feature-barrels
```

Confirm the runner reports **two** projects here (`node` and `browser`). One project means the
`.tsx` file landed on the wrong side and the resolve case is not doing what Step 1b describes.

**Step 3: Write the six barrels**

Export lists verified against every route import in the app; a second reviewer re-verified all 11
component exports and every `{op}Options` against the corresponding `api.ts`.

```ts
// src/features/apps/index.ts
/**
 * The apps feature's public contract.
 *
 * Three categories and no more: the components routes mount, the query options
 * their loaders prefetch, and the public values a route's CONFIGURATION needs.
 * `api.ts` itself stays internal — the seam is the feature's business, not its
 * consumers'.
 */
export { appsGetUserAppsOptions } from "./api";
export { AppList } from "./components/AppList";
export { RegisterAppForm } from "./components/RegisterAppForm";
```

```ts
// src/features/inquiries/index.ts
export {
  inquiriesGetAllOptions,
  inquiriesGetByIdOptions,
  inquiriesGetCommentsOptions,
} from "./api";
export { CreateInquiryForm } from "./components/CreateInquiryForm";
export { InquiryDetail } from "./components/InquiryDetail";
export { InquiryList } from "./components/InquiryList";
```

```ts
// src/features/landing/index.ts
export { LandingPage } from "./components/LandingPage";
```

```ts
// src/features/mfa/index.ts
// `MfaEnrollFlow` is deliberately absent: no route mounts it, only
// `MfaSettingsSection` does, so it stays internal.
export { mfaGetStatusOptions } from "./api";
export { MfaSettingsSection } from "./components/MfaSettingsSection";
```

```ts
// src/features/organizations/index.ts
export {
  organizationsGetAllOptions,
  organizationsGetByIdOptions,
  organizationsGetMembersOptions,
} from "./api";
export { CreateOrganizationForm } from "./components/CreateOrganizationForm";
export { OrganizationDetail } from "./components/OrganizationDetail";
export { OrganizationList } from "./components/OrganizationList";
```

```ts
// src/features/settings/index.ts
export { usersGetCurrentUserOptions } from "./api";
export { ProfileSection } from "./components/ProfileSection";
```

**Step 4: Rewrite every import — work the categories, not a sed**

Enumerate with a pattern that catches **all four** specifier forms. The first draft's grep used
`from "` only and missed bare side-effect imports and dynamic imports:

```bash
cd apps/wallow-web
grep -rEn '(from|import\()\s*"(\.\.?/[^"]*)"|^\s*import\s+"(\.\.?/[^"]*)"' src | wc -l   # ~131
```

| Category | Old | New |
| --- | --- | --- |
| route → feature component | `../features/landing/components/LandingPage` | `@features/landing` |
| route → feature api + component (×8 route files) | `../../features/mfa/api` **+** `.../MfaSettingsSection` | **one** `@features/mfa` import |
| route → shared component | `../components/PublicLayout`, `../components/ready-indicator`, `../../components/DashboardLayout` | `@shared/components/…` |
| feature → shared component | `../../../components/SelectControl` (×2) | `@shared/components/SelectControl` |
| feature → shared lib | `../../../lib/site-links`, `../../../lib/error-text` | `@shared/lib/…` |
| **spec → router (8 files)** | `../../router`, `../../../router` | `@app/router` |
| **spec → test helpers (55 imports)** | `../../test/harness-routes`, `../../../test/style-contract`, … | `@shared/testing/…` — note the directory merged in Task 0.3, so `test/` → `testing/` |
| **`./`-prefixed at old src root** | `start.ts:4` `./lib/request-origin`; `router.tsx:7` `./routeTree.gen` | `@shared/lib/request-origin`; `./routeTree.gen` (unchanged — both ends moved together) |
| **server route → bff (dynamic)** | `routes/health.ts:23` `await import("../lib/bff")`; `routes/bff/$.ts:22` + `routes/api/$.ts:23` `await import("../../lib/bff")` | **UNCHANGED — do not touch.** Stays relative (intra-`app/`) *and* stays at the same depth |
| **root `__root.tsx` stylesheet** | `routes/__root.tsx:28` `import "../styles.css"` | **UNCHANGED — do not touch.** |

The `../../router` and `../../test/*` rows are **not** in the first draft's table at all; they are 63
of the 131 specifiers.

> **The last two rows are the trap in this table: a CO-MOVE changes nothing.** `routes/`,
> `lib/bff.ts` and `styles.css` all land under `src/app/` together, so the number of `../` hops
> between them is exactly what it was. An earlier revision of this plan "corrected" both rows by
> adding a level — `../../lib/bff`, `./styles.css` — which resolves to `src/lib/bff` and
> `src/app/routes/styles.css`, neither of which exists. Rewrite a specifier only when the two ends
> move by *different* amounts. The same applies to `router.tsx`'s `./routeTree.gen` (row above) and
> to every intra-`features/` relative import, none of which move at all.

Collapsing paired `api` + `components/X` imports into ONE `@features/<name>` import is the point of
the barrel — do not leave two statements naming the same specifier (`no-duplicate-imports` warns, and
it reads as if the two came from different places).

**Step 5: Verify the app is whole**

```bash
cd apps/wallow-web
grep -rEn '(from|import\()\s*"\.\./\.\./' src | grep -v '/routes/' ; # survivors outside app/routes deserve a look
pnpm --filter @bc-solutions-coder/sdk build
pnpm --filter ./apps/wallow-web typecheck
pnpm --filter ./apps/wallow-web test
rm src/app/routeTree.gen.ts
pnpm --filter ./apps/wallow-web build 2>&1 | tail -20
test -f src/app/routeTree.gen.ts && echo "route tree regenerated in src/app/"
test -f src/routeTree.gen.ts && echo "FAIL: stale route tree at old path"
```

All must pass; the first `echo` fires and the second does not. **This is the first point at which
the app is whole again**, and the first commit that can go through the pre-commit hook normally.

**Step 6: Commit**

```bash
git add -A apps/wallow-web
git commit -m "refactor(web): route every cross-zone import through a zone alias"
```

---

### Task 0.6: fix wallow-web's app-wide policy specs

Eight root-level specs hard-code paths that just moved. **Several fail *silently*** — they assert
`existsSync(…) === false` for a forbidden module, and a moved path makes that vacuously true.

| File | Lines |
| --- | --- |
| `src/shared-auth.test.ts` | 45, 51, 55 |
| `src/query-facade.test.ts` | 91, 101, 108 |
| `src/styling.test.ts` | 27, 31 |
| `src/brand-assets.test.ts` (moved in 0.3) | `../../vite.config` → `../vite.config` |
| `src/shared/lib/request-origin.test.ts` | 127, 129, 151-152 |

**Step 1:** For each, read the assertion and decide whether the path is *supposed* to move. Where a
spec asserts a file does **not** exist, add a companion positive assertion that the directory it
scans is non-empty — that is what turns a silent pass back into a real check.

**Step 2:** `pnpm --filter ./apps/wallow-web test` — all green. **Step 3:** commit.

---

### Tasks 0.7–0.12: repeat for wallow-auth

Same sequence, same order (`aliases` → wire → move → start-plugin config → barrels + imports →
policy specs), with these app-specific differences:

**0.7 — alias map + pin test + tsconfig.** Identical to 0.1, path-substituted. `apps/wallow-auth`
has no `src/stores/` and no `src/testing/`, so `shared/` gets `components/`, `lib/`, and `testing/`
— the last a straight `git mv src/test src/shared/testing`, with no existing `testing/` to merge
into and so no per-file moves.

**0.8 — Vite/vitest wiring.** **`apps/wallow-auth/vite.config.ts:47-70` ALREADY HAS
`resolve.alias` as an array** with the same two anchored `use-sync-external-store/shim` regexes.
Append to it exactly as in Task 0.2 — do **not** replace it with an object, which would drop the
regexes and reintroduce the double-React SSR bug. `vitest.config.ts` currently spreads neither
project; spread both. `nodeTsxSpecs` is `["src/routes/index.test.tsx"]` → `["src/app/routes/index.test.tsx"]`.

**0.9 — the move**, including `git mv src/lib/base-path-wiring.test.ts src/base-path-wiring.test.ts`
and `git mv src/lib/brand-assets.test.ts src/brand-assets.test.ts` (config guards → app-wide policy).
`styles.css` → `src/app/styles.css`. wallow-auth has no `bff.ts`.

**0.10 — start plugin.** Same `srcDirectory: "src/app"` + `importProtection: { include: ["src/**"] }`.
**Additionally: `apps/wallow-auth/vite.config.ts:7` imports `./src/lib/base-path`** — a config file
reaching into `src/`, outside every grep the import-rewrite step runs. It becomes
`./src/shared/lib/base-path`.

**0.11 — fifteen feature barrels.** Reuse **both** spec files verbatim —
`feature-barrels.test.ts` (node, shape) and `feature-barrels.browser.test.tsx` (Chromium, resolve).
Neither carries an app-specific list: one discovers features from the directory listing, the other
from `import.meta.glob`. `src/features/ui-catalog-sweep.test.ts` sits directly under `features/` and
is not a feature — the `withFileTypes` + `isDirectory()` filter skips it, and the
`./features/*/index.ts` glob never matches it; confirm both.

| feature | barrel exports |
| --- | --- |
| accept-terms | `AcceptTermsScreen` |
| consent | `ConsentScreen` |
| error | `ErrorPage` |
| forgot-password | `ForgotPasswordForm` |
| invitation | `InvitationLoading`, `InvitationScreen` |
| login | `LoginScreen`, `PASSWORD_RESET_MESSAGE`, `isPasswordResetMessage`, `clientBrandingGetBrandingOptions` |
| logout | `LogoutScreen` |
| mfa-challenge | `MfaChallengeForm` |
| mfa-enroll | `MfaEnrollForm` |
| not-found | `NotFoundPage` |
| privacy | `PrivacyPage` |
| register | `RegisterForm` |
| reset-password | `ResetPasswordForm` |
| terms | `TermsPage` |
| verify-email | `VerifyEmailConfirm`, `VerifyEmailNotice` |

**0.11 — import rewrite.** Corrected counts and hazards:

- **16** route modules import `AuthLayout` from `../components/auth-layout` → `@shared/components/auth-layout`.
  (The first draft said 22. `grep -rl 'components/auth-layout' src/routes | wc -l` → 16.)
- 16 feature modules import `BASE_PATH`/`toAppHref` from `../../../lib/base-path` → `@shared/lib/base-path`
  — **except `verify-email/sign-in-href.ts:3`, which uses `../../lib/base-path` (two levels, not
  three). A blanket sed misses it.**
- Three passthrough routes import `../../lib/api-passthrough` → `@shared/lib/api-passthrough`.
- Two route specs import `../test/harness` → `@shared/testing/harness`.
- `src/start.ts:4-5` and `src/router.tsx:7` use **`./`-prefixed** `./lib/base-path`,
  `./lib/request-origin` → `@shared/lib/…`. Both files themselves move into `app/`, so both ends
  move; `./routeTree.gen` (router.tsx:8) is unchanged.
- `routes/login.tsx:11` imports `isPasswordResetMessage` + `PASSWORD_RESET_MESSAGE` from
  `../features/login/auth-result` and `clientBrandingGetBrandingOptions` from `../features/login/api`
  → collapse both into one `@features/login` import.
- `routes/__root.tsx:20` `import "../styles.css"` — **UNCHANGED, do not touch.** `routes/` and
  `styles.css` both move into `src/app/`, so the hop count is identical. See the co-move warning
  under Task 0.5 Step 4; wallow-auth has no `bff.ts`, so that row has no analogue here.

**`features-api-seam.test.ts` needs exactly four edits** — no more:

1. `boundaryScope()` (:339-344) — `path.startsWith("routes/")` → `path.startsWith("app/routes/")`.
2. `DATA_CONSUMERS` (:212-216) — the `"routes/login.tsx"` key → `"app/routes/login.tsx"`, and its
   `seam` → `"@features/login"`.
3. The seam-join assertion (:425) does `join(dirname(path), consumer.seam)`, meaningless for an
   alias. Branch: an alias seam must equal `@features/${consumer.owner}`; a relative seam keeps the
   existing `join`.
4. `moduleSpecifiers()` (:329-336) gains the dynamic-import pattern
   `/\bimport\(\s*"([^"]+)"\s*\)/gu` — the back-port called for in Task 0.13 Step 1. This edit is
   **not** caused by the restructure; the blind spot predates it. It rides along because this task
   is already in the file and because Task 0.13 is about to build a second guard on the same helper.
   Fix the doc comment above it too: "bare side-effect imports alike" reads as if dynamic imports
   were already covered, which is how the gap survived this long.

**Do NOT change `SEAM_FILE` (:254).** Its regex is `/^features\/[^/]+\/api(?:\.test)?\.ts$/u` and
`features/` does not move, so it still matches. (The first draft said to update it; that was wrong.)
`appSources()`'s `routeTree.gen.ts` exclusion (:275) is `.endsWith(…)` and also survives.

Run it before and after to confirm the count of scanned files did not silently drop:

```bash
pnpm --filter ./apps/wallow-auth exec vitest run src/features-api-seam.test.ts
```

Edit 4 widens what the spec *sees*, so it can legitimately surface a pre-existing violation. It
should not: `boundaryScope()` covers `features/**` and `app/routes/**`, and wallow-auth's only
dynamic relative import is `lib/base-path-wiring.test.ts:53`, outside that scope. If something does
light up, it is a real finding — record it and escalate rather than narrowing the regex back.

**0.12 — policy specs.** Seven root-level specs hard-code moved paths:

| File | Lines |
| --- | --- |
| `src/sdk-test-seam.test.ts` | 41 |
| `src/shared-current-user.test.ts` | 54 |
| `src/generated-mutations.test.ts` | 308 |
| `src/query-facade.test.ts` | 81, 82 |
| `src/base-path-wiring.test.ts` (moved in 0.9) | 34, 53, 113, 119 — including the **dynamic** `await import("../../vite.config")` at :53, now `../vite.config` |

Same silent-pass hazard as Task 0.6: add a positive companion assertion wherever a spec asserts
non-existence.

---

### Task 0.13: the zone DAG spec (both apps)

**Files:** create `apps/wallow-web/src/zone-dag.test.ts` and `apps/wallow-auth/src/zone-dag.test.ts`
(identical but for the exemption list).

**This replaces the oxlint zone overrides entirely.** `.oxlintrc.json` is **not modified** in
Slice 0. See "Why the DAG is a spec" above for the reasoning; the short version is that oxlint globs
specifier *strings* and the rule we need is about where a path *resolves*.

**Step 1: Write the spec**

Model it on `apps/wallow-auth/src/features-api-seam.test.ts` — its directory-scoping shape
(:339-344) transfers directly, and its `moduleSpecifiers()` (:329-336) is the right starting point
**but must be extended before use**.

> **`moduleSpecifiers()` does NOT capture dynamic imports today — it has exactly two regexes:**
>
> ```ts
> [...code.matchAll(/\bfrom\s+"([^"]+)"/gu)]        // import … from / export … from
> [...code.matchAll(/^\s*import\s+"([^"]+)"/gmu)]   // bare side-effect import
> ```
>
> `await import("…")` matches neither: there is no `from`, and `import(` is neither line-anchored
> nor followed by whitespace. Its doc comment says "bare side-effect imports alike", which reads as
> if it covered dynamic imports; it does not.
>
> **This gap lands on the exact edge `app/lib/bff.ts` exists to defend.** `bff.ts`'s only consumers
> reach it via `await import(…)`, so a `shared/` or `features/` module doing
> `await import("../../app/lib/bff")` would pass a DAG spec built on the unextended helper —
> silently sanctioning the one import the whole `server-only code lives in app/` rule was written to
> stop. Task 0.5 Step 4's own enumeration grep already includes `import\(`; the guard must match it.
>
> So the DAG spec's copy adds a third pattern:
>
> ```ts
> ...[...code.matchAll(/\bimport\(\s*"([^"]+)"\s*\)/gu)].map((match): string => match[1] as string),
> ```
>
> Template-literal and variable specifiers (`import(\`./${name}\`)`) stay out of scope — they cannot
> be judged statically, and neither app has one. If one appears, it is an escalation, not a
> widening.
>
> **Back-port the same pattern to `features-api-seam.test.ts`** while Task 0.11 is editing that file
> anyway: it has the identical blind spot for the same reason, and leaving one guard half-blind
> after discovering why is how the next person inherits the bug.

The spec must:

1. Walk every `.ts`/`.tsx` under `src/`, skipping `routeTree.gen.ts`.
2. Classify the importing file's zone from its path: `app` / `features/<name>` / `shared` /
   `root` (a policy spec directly under `src/`).
3. For each specifier:
   - **relative** → `path.resolve(dirname(file), specifier)`, relativize against `src/`, classify
     the *target* zone, and judge the edge;
   - **`@app/…` / `@features/…` / `@shared/…`** → classify from the alias, and additionally require
     `@features/<name>` to have **no further path segments** (barrel-only — Fact 7);
   - **anything else** (a package) → allowed, that is `no-restricted-imports`' job.
4. Judge against this table:

| importer zone | may reach |
| --- | --- |
| `app` | `app` (relative, any depth), `@features/<name>` (barrel only), `@shared/*`, packages |
| `features/<x>` | its **own** feature relatively, `@shared/*`, packages |
| `shared` | `shared` (relative, any depth), packages |
| `root` policy spec | anything, including paths outside `src/` |

5. Encode exactly two exemptions, **each with a comment saying why**:

```ts
/**
 * The DAG constrains the PRODUCT graph, not the test graph. A spec may import
 * anything its subject is composed with: 19 wallow-auth feature specs import
 * `@app/routes/<name>` and mount the real route, because the component's contract
 * IS the route's `validateSearch` schema — testing it against a hand-rolled stub
 * would test the stub. Product modules get no such licence.
 *
 * Long-term exit: once a feature's search schema is barrel-exported (barrel
 * category 3), a spec can build its own route from it and this exemption shrinks.
 */
const SPEC_MAY_REACH_APP = /\.test\.tsx?$/u;

/**
 * A relative specifier that resolves OUTSIDE `src/` is legal only from a
 * root-level policy spec — those exist precisely to assert things about
 * `vite.config.ts` and the app manifest.
 */
```

**Step 2: Run it — expect it to be green immediately**, because Tasks 0.5 and 0.11 already did the
rewriting. If it is red, the violation is real: fix the import, do not widen the table.

**Step 3: Prove the spec actually bites**

A guard nothing violates is indistinguishable from a guard that does not work. Add each violation,
confirm the spec fails, revert:

```bash
cd apps/wallow-web
printf '\nimport { AppList } from "@features/apps/components/AppList";\n' >> src/app/routes/health.ts
pnpm --filter ./apps/wallow-web exec vitest run src/zone-dag.test.ts   # expect: barrel-only failure
git checkout -- src/app/routes/health.ts

printf '\nimport { LandingPage } from "@features/landing";\n' >> src/features/apps/api.ts
pnpm --filter ./apps/wallow-web exec vitest run src/zone-dag.test.ts   # expect: features-are-isolated failure
git checkout -- src/features/apps/api.ts

printf '\nimport { handleBffRequest } from "../../app/lib/bff";\n' >> src/shared/lib/error-text.ts
pnpm --filter ./apps/wallow-web exec vitest run src/zone-dag.test.ts   # expect: shared-may-not-reach-app failure
git checkout -- src/shared/lib/error-text.ts

# The SAME violation in DYNAMIC form. This is the probe that proves the third
# regex above is wired up: without it this case passes green, which is precisely
# how `bff.ts` — reached only ever via `await import(…)` — would slip the guard.
printf '\nexport const probe = async () => import("../../app/lib/bff");\n' >> src/shared/lib/error-text.ts
pnpm --filter ./apps/wallow-web exec vitest run src/zone-dag.test.ts   # expect: the SAME failure
git checkout -- src/shared/lib/error-text.ts
```

> Each of these files is committed and clean at this point, so `git checkout --` is safe **here
> specifically**. Never run it against a file carrying uncommitted work.

**A green run on the fourth probe is not a pass — it is the finding.** It means the spec is reading
static imports only, and the whole `server-only code lives in app/` rule is unenforced.

**Step 4: Add the `shared/` subdirectory allowlist**

In the same spec — one assertion, and the thing that actually prevents `shared/` becoming a dumping
ground. You cannot dump if there is nowhere to dump *to*:

```ts
/**
 * Promotion into shared/ is a decision, not a reflex. A new top-level directory
 * here is a design change and should fail until it is one.
 *
 * `stores` is on this list only until Slice 4: wallow-web's `ui-store` is entirely
 * navigation state and moves into `packages/navigation` as `useNavStore`, at which
 * point `shared/stores/` is deleted and MUST come off this list too — an allowlist
 * that keeps naming a directory nothing uses drifts in the permissive direction.
 *
 * There is no `test` entry: `src/test/` merged into `shared/testing/` in Task 0.3.
 */
const SHARED_SUBDIRS: readonly string[] = ["components", "hooks", "lib", "stores", "testing", "types"];

it("keeps shared/ to its sanctioned subdirectories", () => {
  const present: readonly string[] = readdirSync(join(srcDir, "shared"), { withFileTypes: true })
    .filter((entry): boolean => entry.isDirectory())
    .map((entry): string => entry.name)
    .toSorted();

  expect(present.filter((name): boolean => !SHARED_SUBDIRS.includes(name))).toEqual([]);
});
```

**Step 5: Commit**

```bash
git add apps/wallow-web/src/zone-dag.test.ts apps/wallow-auth/src/zone-dag.test.ts
git commit -m "test: enforce the app zone import DAG"
```

---


### Task 0.14: documentation

> **No fork upgrade runbook.** An earlier revision made one Step 1 here
> (`docs/getting-started/fork-upgrade-zones.md`: the `git mv` sequence a diverged fork runs on its
> own branch before merging upstream, so the merge reduces to content). It is cut. Its entire
> audience is forks that diverged *before* this lands, and there are none — a fork created
> afterward clones the new shape and never migrates. Writing it now means writing, reviewing, and
> then maintaining a migration guide with zero readers.
>
> This is "not yet", not "never". Wallow is a fork-first base platform by charter, so if a fork
> exists when a restructure of this width next comes up, that runbook is the right move then.

> **Nothing to do about `merge=ours` either.** The review claimed the driver is inert because forks
> may not have run `git config merge.ours.driver true`, and an earlier revision planned a doc fix
> for it. **That premise is false.** `docs/getting-started/fork-guide.md:58-61` already gives the
> exact command as a numbered setup step, :63-72 tables every pattern it covers, :74 states that
> anything outside the list — explicitly including `.claude/**` — merges normally, and :998 repeats
> activation in the fork checklist. The trap is already documented. Do not "fix" it.

**Step 1: Update the layout documentation** — `docs/development/frontend-setup.md`,
`apps/wallow-web/README.md`, `apps/CLAUDE.md`, `docs/development/testing-e2e.md`,
`docs/integrations/*.md`, `packages/sdk/README.md`. Grep for `src/routes`, `src/components`,
`src/lib` across `docs/` and both READMEs.

**Step 2: Amend the design doc** with the four decisions this slice settled:
- server-only modules live in `app/`, never `shared/`;
- `styles.css` and `start.ts` move into `src/app/` (the design says top-level files stay put);
- the DAG constrains the product graph, not the test graph;
- the `shared/` promotion ladder — see below.

**Step 3: Write down the `shared/` promotion rules** (design doc, ~10 lines). The design currently
says "if two features need it, it goes to `shared/`" and stops, which is a rule with no brake:

- **Compose at the route first.** When two features need the same *behaviour*, the first answer is
  that the route composes both features — not a shared component. Only genuinely presentational,
  feature-agnostic pieces go to `shared/components/`. Without this rule every "two features need X"
  resolves to promotion and the allowlist is the only thing holding the line.
- **The trigger is not a count.** Two consumers **and** the module has no feature-specific types in
  its signature. If promoting it means widening a prop from `LoginSubmitState` to `unknown`, do not
  promote it — duplication is cheaper than a bad abstraction.
- **Name the de-typing cost out loud**, so the ladder has teeth.
- **The subdirectory allowlist is pinned by `zone-dag.test.ts`** (Task 0.13 Step 4).

Also record the real barrel hazard, so the next person recognises it in seconds instead of
rediscovering the comment atop `DashboardNav.tsx:1-14`: *a barrel is safe as long as no consumer
stubs a module that a sibling export imports at module scope.* The risk scales with how aggressively
specs stub router internals, **not** with barrel size — which is what the design currently claims.

---

### Task 0.15: full gate

```bash
pnpm --filter @bc-solutions-coder/sdk build
pnpm check                                    # format:check + lint + typecheck + test + build + check:exports
pnpm --filter ./apps/wallow-web test:e2e
pnpm --filter ./apps/wallow-auth test:e2e
./scripts/e2e.sh                              # all three Playwright suites, containerised stack
```

E2E is not optional: the restructure moved both route trees, and `routes.spec.ts` is the only thing
that proves every route still renders. **No reviewer verified the E2E steps by execution** — they
were checked by config inspection only, so treat the first real run as discovery.

Slice 0 ships as **one PR**, entirely `refactor:` / `test:` / `build:` — **no `feat!:`**. Nothing
published changes shape: the restructure is confined to `apps/`, and neither app is a published
package. An earlier revision marked it breaking to flag the fork-facing break, which no longer
exists. Under release-please that means Slice 0 cuts no release, which is the honest outcome for a
file move.

---

### Slice 0 — decisions the executor must escalate rather than resolve

- A relative import that resolves outside `src/` from anywhere other than a root-level policy spec.
  The DAG assumes none exist beyond the two config guards; a third is a signal the table is wrong.
- `apps/examples/minimal-app` is deliberately **out of scope** — whether it adopts the zones or stays
  flat as a minimal reference is an open item in the design doc.
- Any feature whose barrel would need to export more than a handful of names. A fat barrel pulls the
  feature's whole graph into every route chunk that touches it; that is a signal the feature should
  split, and worth raising before writing it.

### A note the design should absorb

In `wallow-auth`, features and routes are **1:1** — login, terms, consent, invitation, register,
logout, mfa-*, verify-email, forgot/reset-password, accept-terms, privacy, error. A "feature" there
is a route body. The three-zone split buys real isolation in `wallow-web`, where features fan into a
dashboard; in `wallow-auth` it mostly buys symmetry. Applying it to both is still right — one layout
across both apps is worth something to forks — but the design should say that plainly rather than
implying equal payoff. The 19 upward spec edges are the cost of pretending otherwise.

---

## Slices 1–5 — task-level outlines

Expand each into step-level TDD detail once Slice 0 has landed and its lessons are known. Every
slice is vertical: extract the package, rehome the helpers it absorbs, migrate both apps, document
it, and gate it.

### Slice 1 — `packages/utils`

The bottom of the dependency graph and zero-risk, which is why it goes first: it proves the
new-package pipeline (manifest, exports map, build, `check:exports`, workspace link, Dockerfile COPY
lines) before a slice with real behaviour rides on it.

1. Scaffold `@bc-solutions-coder/utils` from `packages/query`'s manifest shape (smallest existing
   package). Five thinly-populated subpaths. **Use the design doc's names — `./format`, `./string`,
   `./array`, `./result`, `./guards`** — the first draft of this plan renamed three of them, and
   subpaths are public API where a rename is breaking for forks.
2. **Machine-enforce the charter**, which is the whole reason this package is allowed to be generic:
   - a spec asserting `dependencies` and `peerDependencies` are empty,
   - `"lib": ["ESNext"]` and `"types": []` in its tsconfig, so a DOM or Node API will not compile,
   - an oxlint override banning `react`, `react-dom`, `zustand` and `@bc-solutions-coder/*` under
     `packages/utils/**` — and, per Fact 6, re-declaring the root's `no-restricted-imports` bans,
   - an export-coverage spec diffing the exports map against the source tree.
3. Seed each subpath from something that already exists in the apps, not from imagination.
4. Add to both apps' `package.json`, both Dockerfiles (two COPY lines each — the
   `docker-workspace-copies.test.ts` spec will tell you if you miss one), and both
   `extraBrowserOptimizeDeps` lists if any app-side spec mounts a consumer.
5. Docs: a new `docs/development/` guide + `docs/toc.yml` entry + the repo-map table in `CLAUDE.md`
   and `apps/CLAUDE.md`.

### Slice 2 — `packages/config`

Env/config validation. Depends on `utils` only.

1. Scaffold; define the schema-validated env contract.
2. Migrate `apps/wallow-auth/src/shared/lib/base-path.ts`'s `BASE_PATH` derivation and both apps'
   `WALLOW_API_INTERNAL_URL` reads onto it.
3. Fail loudly at boot on a missing/invalid var — the point of the package is that a fork learns
   about a misconfiguration at startup, not at the first request.
4. Same manifest/Dockerfile/docs checklist as Slice 1.

### Slice 3 — `packages/logger`

Depends on `utils`. Transport is **through the BFF**, per the design.

1. Scaffold: a browser entry that buffers and posts, a server entry that writes structured records.
2. **CSRF on the terminal flush is already resolved in the design — do not reopen it.** The token
   rides in the **body** on the `sendBeacon` path and `handleLogIngest` accepts it from either place.
   The first draft of this plan reopened this and floated dropping the check on the beacon path;
   that is a security regression, since `sendBeacon` is exactly the unauthenticated,
   cross-origin-postable surface double-submit CSRF exists to protect.
3. **`wallow-auth` has no BFF.** It mounts `createApiPassthrough`, not `createWallowBffServer`, so a
   `/bff/logs` route assumes infrastructure that app does not have. The package needs a
   passthrough-compatible ingest path or wallow-auth cannot use it. Resolve this before scaffolding.
4. Wire the existing `x-request-id` correlation contract from the SDK through log records.
5. Replace the five `console.*` call sites the audit found.
6. Point it at the `grafana/otel-lgtm` stack already in `docker/`.
7. Same manifest/Dockerfile/docs checklist.

### Slice 4 — `packages/navigation`

The largest slice. Depends on `ui`, `styles`, `utils` and `zustand`; deliberately **no `auth` and no
`sdk` edge** — the visibility predicate is an app-supplied prop and the logout control is a footer
slot.

1. **`ui-store.ts` moves into the package as a navigation store — DECIDED, not open.** Navigation
   owns its own state; an app-level store that happens to hold nav flags is the thing that couples
   every fork's app shell to the nav implementation.

   **The whole store moves, and that is a finding, not an assumption.** All five members
   (`isNavCollapsed`, `toggleNavCollapsed`, `isMobileNavOpen`, `openMobileNav`, `closeMobileNav`) are
   navigation state, and every consumer is `DashboardNav`, `DashboardLayout`, or one of their nine
   specs. `ui-store` was named aspirationally; there is no non-navigation part to leave behind today.

   - `packages/navigation` exports **`useNavStore`** (renamed from `useUiStore` — the name should say
     what it owns) with the same five members and the same two-axis contract. **Carry the
     `TWO AXES, NOT ONE` comment across verbatim**: `isNavCollapsed` is the desktop rail, `isMobileNavOpen`
     is the mobile drawer, and neither may be derived from the other. That comment records a real
     regression (Wallow-0byr.1), not a preference.
   - **`apps/wallow-web/src/shared/stores/` is deleted in this slice**, since it becomes empty. Do not
     leave a pass-through re-export or an empty store behind — re-adding an app-level `ui-store` when
     the app actually has non-nav global UI state is a two-line change, and a shim that exists only to
     preserve an import path is exactly the kind of thing a fork inherits and never removes.
     Remember to drop `stores` from the `shared/` subdirectory allowlist in `zone-dag.test.ts` at the
     same time, or that assertion goes stale in the permissive direction.
   - **The store is a module-global singleton, and moving it into a package puts that singleton's
     identity at the mercy of resolution.** This is the same hazard class `@bc-solutions-coder/query`
     exists to solve for `QueryClient` — two copies of the package means two stores and a nav that
     silently desyncs. Declare `"zustand": "catalog:react"` as a **peerDependency** (Task 0.0 seeded
     the catalog entry for exactly this), so the app supplies the one copy rather than the package
     bundling a second; keep it in `devDependencies` too, at the same `catalog:react`, so the
     package's own specs resolve it. Export the store from exactly one entry — never duplicated
     across subpaths, which reintroduces the two-instances problem inside a single install. **Move
     `ui-store.test.ts:146-150`'s re-import identity case with it**, strengthened to assert identity
     across a *package* import rather than a relative one.
   - The nine consuming specs move with the components in step 2 and switch to `useNavStore`.
2. Move `DashboardNav.tsx`, `DashboardLayout.tsx`, `nav-icons.ts` and their specs.
   **Preserve the module-graph hazard documented atop `DashboardNav.tsx:1-14`**: it imports
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

### Slice 5 — remaining rehomes

The tail the other slices did not absorb: helpers that belong in `sdk`, `styles` or `forms` rather
than in an app's `shared/`. Includes the design's open item on whether `site-links.ts` belongs to
`navigation` or `styles` (branding-adjacent) — decide it here with the packages in front of you.

---

## Issue tracking

File these as beads before starting, one per task, so progress survives a session boundary:

```bash
bd create "slice 0: pnpm catalogs for shared frontend pins" --type task
bd create "slice 0: wallow-web three-zone restructure" --type task
bd create "slice 0: wallow-auth three-zone restructure" --type task
bd create "slice 0: zone DAG spec + layout docs" --type task
bd create "slice 1: packages/utils" --type task
# … etc
```

Claim with `bd update <id> --status in_progress`, record findings on the bead with `bd note <id>`,
and close with `bd close <id>`. Per `CLAUDE.md`, work is not complete until `git push` succeeds.

## Reminders

- `docs/plans/` is gitignored. Never `git add` this file.
- Run `pnpm --filter @bc-solutions-coder/sdk build` before typechecking an app — apps typecheck
  against `dist/`.
- Intermediate Slice 0 commits need `--no-verify` (Fact 10). The final gate does not.
- Commit messages: Conventional Commits, lowercase, imperative, no trailing period, first line < 72
  chars, module or app name as scope. Slice 0 carries **no `!`** — the move commits are `refactor:`,
  the catalog commit is `build:`, the DAG spec is `test:`.
- There are **no forks of this repo yet**, and several decisions here depend on that: the `test/`
  merge happening up front (Task 0.3), the absence of a fork upgrade runbook (Task 0.14), and the
  non-breaking commit types. If that changes before this lands, revisit those three — not the rest.
