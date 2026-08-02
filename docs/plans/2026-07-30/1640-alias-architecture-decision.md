**status: active**

# Path aliases: audit findings and recommendation

Synthesis of four parallel investigations. Each claim below is carried from a
detailed report; read those for the evidence trail.

| Report | Scope |
| --- | --- |
| `1604-alias-audit-current-state.md` | Inventory + git archaeology of what exists today |
| `1611-research-tanstack-nitro-aliases.md` | TanStack Start / Nitro, official docs + plugin source |
| `1614-research-vite8-typescript-aliases.md` | Vite 8 / TypeScript / Node `imports` |
| `1627-spike-alias-candidates.md` | Empirical builds, boots and measurements |

## Recommendation

Adopt **Vite 8's native `resolve.tsconfigPaths: true`**, making `tsconfig.json`
`paths` the single declaration site.

This is the approach TanStack Start officially documents
(`tanstack.com/start/latest/docs/framework/react/guide/path-aliases`) and the one
every official `start-*` example uses. It needs no dependency, and Vite 8 prints a
notice telling you to remove `vite-tsconfig-paths` if you have it.

Measured diff on wallow-web: **9 insertions, 118 deletions across 5 files.**

## Why not the alternatives

The two document-driven researchers independently ranked Node's `package.json`
`imports` (`#app/*`) first, on the reasoning that it is native to TypeScript, Vite,
Vitest and oxlint and needs no mirror and no experimental flag. **The empirical
spike inverted that ranking**, and the reason is specific and worth recording:

No naive `imports` shape satisfies both resolvers at once.

- Bare `"./src/shared/*"` → **85 `TS2307` errors**.
- Extension-first array → `tsc` clean, but **Vite fails 37 test files** (`.tsx` and barrels).
- Only a **bare-first 5-entry array** works, because TypeScript walks the whole
  array while Vite takes entry 1 and infers extensions.

That ordering is load-bearing and undocumented — precisely the kind of quiet thread
this work exists to remove. It also costs a **64-file source rewrite** and breaks two
policy specs that hardcode the `@` spelling (`request-origin.test.ts:133`,
`zone-dag.test.ts`).

| Candidate | Result |
| --- | --- |
| **A2 — native `resolve.tsconfigPaths`** | **PASS 8/8, zero deps, zero workarounds, zero source churn** |
| A — `vite-tsconfig-paths` plugin | PASS 8/8, but Vite 8 tells you to delete it |
| B — Node `imports` (`#app/*`) | PASS 8/8 only with the ordering workaround; 64 files rewritten |
| C — shared build-config package | Not spiked; not needed |

## The disqualifying check passed

A Vite-only alias solution breaking the production Nitro bundle was the one risk
that could have killed this outright. It does not exist.

Under `nitro/vite`, Nitro's `config` hook *returns* `resolve.alias` back into Vite
(`nitro/dist/vite.mjs:313-325`), so there is one merged resolver. Its independent
rollup alias plugin only applies to the standalone `nitro build` path, which this
repo does not use.

Confirmed by boot, not just by build: `.output/server/index.mjs` is **18071 bytes —
byte-identical to baseline** for all three candidates, boots under
`node .output/server/index.mjs`, serves `/bff-demo` at **HTTP 200 / 7213 bytes**, zero
errors, and **0 bare `@app` / `#app` specifiers anywhere in `.output/`**.

## What this removes

Adding one alias to one app currently takes **4 file edits**, ×2 apps = **8**:

1. `aliases.ts`
2. `tsconfig.json` `paths`
3. `src/alias-map.test.ts:44`
4. `src/zone-dag.test.ts` (two places: the prefix list and `targetOf`)

After: **1 file** (`tsconfig.json`). Verified by actually adding `@entities/*`.

Deleted outright:

- `apps/*/aliases.ts` — byte-identical in both apps
- `apps/*/src/alias-map.test.ts` — purely self-referential; it exists only to pin a
  mirror that this change deletes
- the four `resolve.alias` zone splices in the vite/vitest configs

Roughly **430 duplicated lines of alias/DAG machinery per app** collapse.

## What must stay

`tsconfigPaths` cannot express these, and they remain in `resolve.alias`:

- the anchored `use-sync-external-store/shim` regexes (wallow-web `vite.config.ts`) —
  the comment explaining them is **correct** and load-bearing
- the `node:async_hooks` browser shim (vitest browser project)

The spike confirms `resolve.alias` and `resolve.tsconfigPaths` coexist correctly.

## Landmines

1. **`resolve.tsconfigPaths` at the root of `vitest.config.ts` is NOT inherited by
   `test.projects`.** It must go inside each project entry — a drop-in swap for the
   current per-project `resolve: { alias: resolveAlias }`. Verified both ways.
2. **Do not hoist `paths` into `tsconfig.base.json`.** `paths` resolve relative to the
   file containing them, so a parent-dir base hard-fails. It fails loudly, at least.
3. `pnpm --filter sdk build` alone is **not** sufficient — a full `pnpm build` is
   required or apps show 30+ `TS2307`s. This bites anyone reproducing these results.
4. The option is still marked `@experimental` in Vite 8.1.4. Measured cost is small:
   typecheck 2.7s → 3.1s, build 1.7s → 1.8s, `.output` byte-identical.

## Corrections to beliefs encoded in the current code

- **`aliases.ts`'s trailing-slash rationale is wrong.** Vite matches
  exact-or-path-segment, so a bare `@app` key would *not* swallow `@application`.
  Disproved by a real build. The trailing-slash keying can go.
- **The BFF-accretion theory is mostly refuted.** This machinery is 14 commits from a
  single day (`2434359d..1585962c`), built on wallow-web then replayed commit-for-commit
  onto wallow-auth. Only the `node:async_hooks` shim, and partly the
  `use-sync-external-store` shim, trace to auth/BFF work. `ssr.noExternal` and the
  `copyPublicDir` fix are unrelated.
- **`tsconfig.base.json` claimed TypeScript 7 semantics while the workspace resolved
  5.9.3.** Fixed separately; the workspace is now on 7.0.2 (`packages/sdk` pinned to
  6.0.3 for `@hey-api/openapi-ts`).
- **`@app/` has zero product usage in either app.** Every occurrence is a spec importing
  `@app/router` or `@app/routes/*`. The DAG forbids `features`/`shared` from reaching
  `app`, so nothing else ever can. It is structurally a test-only alias.

## Separate finding — higher severity than the alias work

**`importProtection` does not do what the code comment claims.** It does not reject
`redis` or `@app/lib/bff` from a client module: the baseline build exits 0 and
**`redis` actually ships in the client bundle**. The real default rule is
`**/*.server.*` files.

This is independent of the alias decision and should be triaged on its own. The spike
built a probe that does fire (exit 1) and used it as the control, so all candidates
were compared against a rule that actually enforces something.

Related: the `srcDirectory: "src/app"` + `importProtection: { include: ["src/**"] }`
pairing *is* load-bearing (`adapterUtils.ts:86-87`), but it is guarded only by a regex
over config file text (`brand-assets.test.ts:87-96`).

## Guard tests

- `alias-map.test.ts` — **delete**. Self-referential.
- `zone-dag.test.ts` — **keep the invariants, revise the mechanism.** Rules (a)–(f) are
  genuine architecture. Two clauses are artifacts: the "must be spelled as an alias"
  requirement, and the hard-coded three prefixes — today a **fourth zone is silently
  unpoliced**, falling through to `kind:"package"`. Needs a policy edit under either
  candidate, but does not fail.

## Not covered

Stated so it is not mistaken for verified:

- Spikes ran on **wallow-web only**. wallow-auth is untested, though its configs are
  byte-identical.
- **No Playwright E2E run.**
- No authoritative source was found for TanStack's position on large-app layout — every
  official example declares a single alias to `./src/*`, and `start-large` is a
  route-count stress test, not an architecture demo. Upstream neither endorses nor
  contradicts the three-zone design.

## Suggested sequencing

1. Triage the `importProtection` / `redis`-in-client-bundle finding. Independent, and
   more serious than anything else here.
2. Adopt A2 on wallow-web; mirror to wallow-auth.
3. Revise `zone-dag.test.ts`: drop the alias-spelling clause, derive the zone list
   rather than hard-coding it.
4. Re-run E2E, which the spike did not cover.

Branches carrying the experiments (not pushed): `spike/control`,
`spike/a-vite-tsconfig-paths`, `spike/a2-native-vite-tsconfigpaths`,
`spike/a2-fourth-zone`, `spike/b-node-subpath-imports`, `spike/b-fourth-zone`.
