# Turborepo Adoption — Measurement Record

**status: active**

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
