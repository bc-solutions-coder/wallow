# Turborepo Adoption — Measurement Record

**status: completed**

> Companion to `1206-turborepo-implementation.md` (Task 1 writes the baseline, Task 4 the
> post-turbo figures, Task 5 the cache-correctness verdicts). Kept separate from the plan so the
> plan does not rewrite itself under the reader mid-execution.

## Baseline

Measured before any turbo wiring existed, on a clean tree at commit `0b054317`, after
`rm -rf packages/*/dist apps/*/.output apps/examples/*/.output`.

| Command          | Wall clock | Notes                                                    |
| ---------------- | ---------- | -------------------------------------------------------- |
| `pnpm check`     | **64 s**   | format:check → lint → lint:tests → lint:manifests → lint:deps → build → typecheck → test → check:exports |
| `pnpm build`     | **6 s**    | cold, 14 members with a `build` script                    |
| `pnpm typecheck` | **5 s**    | run against the `dist/` the preceding build emitted       |
| `pnpm test`      | **42 s**   | 15 members; browser-mode suites drive real Chromium       |

All four exited 0.

**Concurrency.** These were taken at pnpm's default `workspace-concurrency` of **4** —
`workspace-concurrency` is unset in both `.npmrc` and the user config, confirmed with
`pnpm config get workspace-concurrency` → `undefined`. Turbo defaults to `concurrency: "10"`, so
the Task 4 cold figures are **not** like-for-like with these. Only the warm (cache-hit) figures are
directly comparable.

**Machine.** Apple M4 Pro, 14 cores (10 performance / 4 efficiency), 24 GB RAM, macOS 15.7.2,
Node 24.11.1, pnpm 10.20.0.

### Reading these numbers

The plan anticipated "several minutes" for a cold `pnpm check`; on this machine it is roughly one.
That does not invalidate the change — the case for turbo rests on the warm path and on CI, where
`ubuntu-latest` is a 2-core runner with none of this hardware — but it does set expectations for
the local payoff honestly. The headline to beat is **42 s of `test`**, which dominates the gate;
`build` and `typecheck` together are 11 s and have little left to give locally.

## After turbo (Task 4)

Same machine, same tree, measured at commit `cecd82e6` with the root scripts rewired.

| Run                                | Wall clock | vs baseline    |
| ---------------------------------- | ---------- | -------------- |
| `pnpm check`, cold (cache cleared)  | **55.7 s** | 64 s → −13 %   |
| `pnpm check`, warm (full cache hit) | **13.3 s** | 64 s → **4.8×** |

Cold was cleared with `rm -rf packages/*/dist apps/*/.output .turbo node_modules/.cache/turbo`.
Both runs exited 0.

The cold figure is the *less* interesting one and, as the concurrency note above warns, is not
like-for-like — turbo runs 10 wide against pnpm's 4. A 13 % gain there is within the range
concurrency alone could explain.

### Where the warm 13 s actually goes

| Component                                  | Warm  | Cacheable by turbo |
| ------------------------------------------ | ----- | ------------------ |
| `turbo run build typecheck test` (45 tasks) | 0.4 s | yes — all 45 hit   |
| `check:exports`                             | 9.1 s | no (design §1)     |
| format:check + lint + lint:tests + lint:manifests + lint:deps | 4.8 s | no — root tasks    |

This is the result worth recording: **the entire build/typecheck/test DAG collapses from ~53 s to
0.4 s**, and what remains is the part deliberately left outside turbo. `check:exports` is now the
single largest item in a warm gate. It stays outside because it needs every `dist/` present at once
and a root task cannot express "after all packages built" — but it is the obvious next target if
warm-gate latency ever matters again.

The 45 breaks down as 14 `build` + 15 `test` + 16 `typecheck`. Not all 16 members run all three:
`config` has neither a `build` nor a `test` script and `lint` has no `build`, so those three are
transit nodes that turbo threads dependencies through without executing anything.

## Cache-correctness verification

Six adversarial checks, run at commit `8ca18523`. The question each answers is not "is turbo
fast" but "can a stale cache produce a false pass". All six passed; every edit was reverted and
`git status` is clean.

| # | Check | Probe | Expected | Observed |
| - | ----- | ----- | -------- | -------- |
| 1 | A dependency's source change invalidates its dependents' tests | append to `packages/utils/src/string.ts`, then `test --filter wallow-web` | miss | **miss**, `57ec9129` — 10 of 12 cached, the app's tests re-ran |
| 2 | A failing test is never cached as a pass | break one assertion in `packages/testing/src/console-guard.test.ts`, run twice | miss both times | **miss both**, identical hash `50e3d0d1`, 1 failed / 81 passed each run |
| 3a | A `globalDependencies` entry busts everything | add a real key to `tsconfig.base.json` | full miss | **0 of 27 cached** |
| 3b | A non-`globalDependencies` config does *not* | add a comment to `.oxlintrc.json` | full hit | **27 of 27 cached**, FULL TURBO |
| 4 | A transit node's source folds into consumers' hashes | append to `packages/config/src/vite/library.ts` | broad miss | **0 of 14 cached** |
| 5 | `AUTH_BASE_PATH` participates in the hash (run in Task 3) | build wallow-auth set vs unset | different hashes | **`9a401928` vs `297fd917`**, 178 files differ |

Notes on two probes that are easy to get wrong:

- **Checks 3 and 4 use real content edits, not `touch`.** Turbo hashes content, so an mtime bump
  reports a hit — which reads as a failure of `globalDependencies` when it is really a failure of
  the test.
- **Check 5's probe is not the plan's.** The plan suggested `grep -rl '/auth/' .output`, but that
  matches backend API route strings (`/v1/identity/auth/passwordless/...`) that are present in
  *both* builds, so it can never go empty and would "pass" vacuously. The marker that actually
  distinguishes them is the emitted asset prefix: `"/auth/assets/*"` based against `"/assets/*"`
  unbased. Check 3b is the one that makes 3a meaningful — a probe that busts everything proves
  nothing unless something comparable is shown *not* to.

Check 1 is the load-bearing one. A hit there would mean `^build` is not wired and every downstream
suite could replay a pass against code that changed underneath it.
