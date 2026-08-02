**status: active**

# Build simplification — consolidated plan

Synthesis of four parallel reviews of how Wallow uses Vite 8, TypeScript 7, Vitest and pnpm.
Source reports (read these for evidence and before/after snippets):

- `tsconfig-review.md` — tsconfigs, `exports`, scripts, pnpm
- `vite-review.md` — app + package `vite.config.ts`
- `vitest-review.md` — `vitest.config.ts` + the `createVitestProjects` preset
- `guard-test-audit.md` — the 54 meta/guard specs, incl. the config→test cross-reference map

## Headline: the premise was half right

The starting hypothesis was that most config complexity is obsolete trial-and-error from the
h3 → TanStack Start migration, now fixed by Vite 8 / TS 7. Measured against the installed
code, that splits cleanly:

| Area | Premise | Reality |
| --- | --- | --- |
| `tsconfig` / `exports` | Real problem | **Confirmed.** Root cause found, fix proven end-to-end |
| Guard tests | Real problem | **Confirmed.** 10 shape-locks obstruct change |
| `vite.config.ts` | Obsolete hacks | **Refuted.** 20 of 25 options still required; comments are accurate |
| `vitest.config.ts` | Non-best-practice | **Split.** The mechanism is required; the hand-maintained list is not |

The dominant win is **consolidation, not deletion**. Four config surfaces each carry a
byte-identical block that belongs in one shared preset.

Three claims were tested and **refuted** — recorded so they are not re-litigated:

1. The `use-sync-external-store` alias is not dead. `@base-ui/react`'s `useIsHydrating`
   imports it live; deleting it collapses `/login` from 9895 → 2621 body chars with
   "Invalid hook call" at `TabsIndicator` in a booted `.output/server`.
2. Vite 8's dep optimizer has **not** made `optimizeDeps.include` unnecessary. Cold, with the
   lists emptied: ~50% flake, and 282s vs 16.4s when it breaks. `@vitest/browser` already sets
   `optimizeDeps.entries` (dist/index.js:1048) and `holdUntilCrawlEnd` already defaults true —
   no generic lever remains.
3. The source-condition `exports` change does **not** obsolete `ssr.noExternal`. It makes it
   *more* necessary. (This corrects an early assumption made before the reviews ran.)

---

## Phase 0 — Independent fixes (no prerequisites)

These are correct regardless of what else is adopted.

**0.1 `pnpm check` ordering is broken.** It runs `typecheck` (3rd) and `test` (4th) before
`build` (5th), so the gate fails on a clean clone. It has a second, quieter consequence: all
six specs in `packages/ui/src/core/dist-structure.test.ts` are `skipIf(distIsMissing)`, so
locally they report green having asserted nothing. The only honest build-output guard in the
repo is disarmed exactly when someone changes the build. Move `build` ahead of
`typecheck`/`test`.

**0.2 File two beads for latent bugs found in passing:**
- `apps/examples/minimal-app` has the same duplicate-QueryClient graph split as the two big
  apps, **unguarded** — two react-query graphs in its build. It survives only because
  `sdk-wiring.test.ts:98` forbids `useQuery` in SSR entries. Phase 3's `wallowApp()` preset
  fixes it as a side effect.
- Even with the alias in place, `use-sync-external-store/shim/with-selector` still emits
  `__require("react")` into the server bundle, called from 3 chunks.

**0.3 Delete the vacuous tests** (they can never fail):
- `expect(deps.typescript).not.toBe("6.0.3")` — `packages/ui/.../package-scaffold.test.ts:117`
  and `packages/forms/.../package-scaffold.test.ts:193`. Both manifests say `catalog:tooling`;
  no manifest can spell that literal since the move to catalogs.
- The three tsup-absence assertions in `packages/sdk/src/build-config.test.ts`.
- `packages/styles/src/branding-ownership.test.ts` — negative regexes about deleted Blazor apps.
- `apps/wallow-web/src/styling.test.ts` — three `toContain("className")` specs.

---

## Phase 1 — Unlock: retire the shape-lock guards

**This must land before Phase 2 or 3.** Ten specs assert config *source text* by regex, so
behaviour-preserving refactors turn them red. `guard-test-audit.md` Part 2 maps each config
option to the test that fails. Ordered by what each unlocks:

1. `packages/testing/src/browser-optimize-deps.test.ts` — delete the 3
   `browserOptimizeDepsBaseline` describes, **keep** the 4 `mergeOptimizeDeps` specs.
2. `packages/forms/src/core/package-scaffold.test.ts` — delete the config source-text specs,
   including `/projects:\s*\[\s*node\s*,\s*browser\s*\]/` (the most brittle assertion found —
   a variable rename or a formatter wrap fails it).
3. `packages/ui/src/core/package-scaffold.test.ts` — delete the build-shape describes and the
   `@base-ui/react ^1.6` pin. **Keep** the component-layering and `source.css` `@source`
   describes; both have measured bugs behind them.
4. `packages/sdk/src/build-config.test.ts` — delete the file. It blocks Phase 3's shared
   lib-mode helper.
5. `packages/sdk/src/oxfmt-config.test.ts` — delete the file. It shells out to `oxfmt --check`
   across the whole workspace, so any unformatted file anywhere turns an SDK unit test red.
   `pnpm format:check` is the correct gate and already runs.
6. `packages/ui/src/core/storybook-setup.test.ts` — keep 2 specs, delete 11. Unblocks a
   Storybook major.
7. `packages/testing/src/vitest-projects.test.ts` — loosen `toEqual` → `toContain` on
   include/exclude; delete the "not the v3 string" provider spec.
8. `packages/styles/src/vite.test.ts` — delete the last two describes only. The first three
   call plugin hooks and are load-bearing.
9. `packages/testing/src/sdk-seam-exports.test.ts` — keep the barrel-purity spec, delete 6.
10. `packages/ui/src/components/list-row/list-row.composition.test.ts` — asserts the literal
    `"@base-ui/react/use-render"`; blocks the Phase 3 glob.

**Never delete:** `apps/*/server-only-naming.test.ts` (the naming convention *is* the build's
import protection), `apps/*/docker-workspace-copies.test.ts`, `apps/*/zone-dag.test.ts`
(derives from `paths` rather than pinning it — model guard),
`packages/forms/src/core/browser-deps.test.ts`, the `copyPublicDir` and `importProtection`
specs in `apps/*/brand-assets.test.ts`, `packages/sdk/src/openapi-regen.test.ts`, and the
binary-run half of `packages/sdk/src/oxlint-guardrails.test.ts`.

---

## Phase 2 — Remove the prebuilt-`dist` requirement

**The root cause of the original complaint:** every package's `exports` points only at `dist/`.

Measured baseline from a zero-`dist` state:
- `pnpm build` — **passes**. `pnpm -r build` is already topologically ordered
  (`query, sdk, styles → auth, testing → ui → forms → apps`). No work needed.
- `pnpm typecheck` — **fails**, 19× `TS2307: Cannot find module '@bc-solutions-coder/query'`.
- Single-app build — **fails**, and not in `tsc`: Node's ESM resolver dies loading the app's
  own `vite.config.ts`, which imports `styles/dist/vite.js`. Project references would never
  have fixed this.

**The fix, proven end-to-end with all 7 `dist/` deleted:** point `exports` at `src/`; add
`publishConfig.exports → dist/` for the only two published packages (`sdk`, `styles` — the
other five are `private: true`); add `--configLoader runner` to the three apps. Result:
`pnpm typecheck` exit 0 across all 10 projects; full wallow-web SSR+Nitro build exit 0.
~14 files, no source changes. Vite's docs name this exact monorepo case.

Rejected: TypeScript project references. TS7 supports `--build`, but it fixes only `tsc` — not
the `vite.config.ts` resolution hop, which is the actual single-app failure.

Also in scope: `packages/sdk/tsconfig.json` not extending `tsconfig.base.json` is **drift, not
deliberate** — it compiles clean under full base semantics. Its `tsconfig.build.json` header
states four facts that are now wrong (claims `^5.6.0` and "no own pin"). The 7
`tsconfig.build.json` `compilerOptions` blocks are byte-identical → extract a base, but
`rootDir` must stay (TS7 `error TS5011`, verified) and the hand-listed `include` is
load-bearing.

---

## Phase 3 — Consolidate the duplicated config

**3.1 `defineLibraryConfig()` for the 7 package `vite.config.ts`.** 25 code lines are
byte-identical across all seven; `query` and `auth` are 100% identical once comments are
stripped. 341 lines → ~40 plus 7 short files. Note `packages/ui`'s `componentEntries()` is
**required** and both simpler forms fail — dropping it stops `dist/components/<name>/index.js`
being emitted, and rolldown 1.1.5 rejects glob entries (`UNRESOLVED_ENTRY`). It stays, as a
parameter to the helper.

**3.2 `wallowApp()` preset for the app `vite.config.ts`**, mirroring the existing
`wallowStyles()` pattern. Hoists the shared SSR block, killing the byte-duplication between
the two big apps and fixing `minimal-app`'s unguarded QueryClient split (0.2). Keep
`ssr.noExternal` — a working alternative is `ssr.external: ["@bc-solutions-coder/query"]`
alone, but it costs facade HMR in dev. Delete `resolve.dedupe`: removing it is byte-identical
output, the one genuinely obsolete option found.

**3.3 `optimizeDeps` — replace the hand list with a glob.** `include: ["@base-ui/react/*"]`
gave 3/3 cold green at 14.8/12.7/13.0s. vite@8.1.4's `expandGlobIds` matches the pattern
against the package's exports-map keys; the dep cache `_metadata.json` shows **45** entries vs
the 39 hand-listed, including the root. This kills the "every component task appends its
subpath" ritual in `packages/ui/CLAUDE.md`.

Also: **9 dead entries** currently log `Failed to resolve dependency` and pre-bundle nothing —
8 of wallow-web's 22 (all 7 `@base-ui/react/*`, since it doesn't declare the package, plus
`@tanstack/react-query`) and `vitest-browser-react` in `packages/ui`, inherited from the shared
baseline. Lift forms' `browser-deps.test.ts` resolvability guard into the preset so a dead
entry fails loudly instead of silently.

**3.4 Absorb the rest into `createVitestProjects`.** Root `resolve` + `extends: true` replaces
the per-project repetition (verified green on wallow-auth: 66 files / 1367 tests — the
"`resolve` is per-project" comment is stale). The 3×-duplicated `wallowStyles()`/`setupFiles`
block folds in. `nodeTsxSpecs` becomes a `*.ssr.test.tsx` naming convention (4 entries, 3
renames; one file already uses it). **Keep** the `node:async_hooks` shim — reproduced exactly
(`AsyncLocalStorage is not a constructor`, `start-storage-context@1.167.17` at module scope).

Line counts: wallow-web 134→~45, ui 123→~55, forms 85→~30, wallow-auth 78→~25.

---

## Phase 4 — Lint split

Files partition exactly: **836 = 437 source + 399 test/story.** Two constraints, both verified
the hard way — get either wrong and oxlint silently lints **zero** files:

- oxlint does **not** expand globs in path arguments.
- `ignorePatterns` has no `!` negation.

So: the source side ignores tests via `--ignore-pattern`; the test side needs explicit paths
via a small `scripts/lint-tests.sh` plus an `.oxlintrc.tests.json` using oxlint's `extends`.
Bonus: `--vitest-plugin` exists and is enabled nowhere today.

---

## Phase 5 — Guard tests → lint rules (independent, do last)

~2,500 lines in the B bucket duplicate what a lint rule does better:
`catalog-adoption.test.ts` (523 lines), both `typography.test.ts`, the four
`query-facade*.test.ts`, `generated-mutations.test.ts` (466), `features-api-seam.test.ts`
(641), the catalog sweeps. `apps/wallow-web/.oxlintrc.json` already carries
`react/forbid-elements` and a custom plugin — extending it to `wallow-auth` retires the two
largest app guards. Independent of the build work; sequence it after.

---

## Methodology caveat

All four reviews ran in parallel against one working tree, alongside an unrelated agent editing
`packages/ui` styles and `wallow-auth` screens. Two agents hit failures caused by another's
in-flight `dist` rebuild and had to re-run. One agent's first `optimizeDeps` result was invalid
because a config-hash change is not a cold start (`rm -rf node_modules/.vite` is required); it
caught and reported this itself. **Apply these phases serially, not in parallel.**
