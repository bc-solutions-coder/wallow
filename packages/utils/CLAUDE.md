# packages/utils — @bc-solutions-coder/utils Agent Guide

The **bottom of the dependency graph**: pure functions over plain values, depending on
nothing and reaching no host API. Anything that needs React, a store, a network client or
another workspace package belongs in that package, not here.

## Entries

Subpath-only — there is deliberately **no `.` barrel**, so an import names the module it
depends on and a bundler can drop the rest.

| Entry      | What it holds                                                              |
| ---------- | -------------------------------------------------------------------------- |
| `./format` | Locale-**pinned** display formatting (`formatLongDate`).                   |
| `./guards` | `unknown` → narrowed value, never throwing (`asString`, `scalarToString`). |
| `./string` | String shaping (`toSlug`).                                                 |

A declared-public subpath with no consumer is API invented rather than extracted — add a new
subpath only when a call site needs it.

## The charter, machine-enforced

Each point fails a **build or lint**, not a spec:

1. **It depends on nothing.** `dependencies` and `peerDependencies` are both `{}`.
2. **It compiles against no host API.** `tsconfig.json` sets `"lib": ["ESNext"]` and
   `"types": []`, so `window`, `document`, `process` or a `node:*` import does not compile.
   `lib` restates the whole list rather than subtracting, because the base config adds DOM.
3. **It imports nothing.** A root `.oxlintrc.json` override keyed on
   `packages/utils/src/**/*.ts` bans `react`, `react-dom`, `zustand` and
   `@bc-solutions-coder/*` — and restates every root-level ban, because an oxlint override
   REPLACES the rule's options rather than merging them.
4. **Every module on disk is reachable, and nothing else is.** The `exports` map,
   `publishConfig.exports`, `vite.config.ts`'s lib entries and `tsconfig.build.json`'s
   `include` must agree. `wallow/module-lists-in-sync` diffs the four lists at lint time, and
   `pnpm check:exports` checks the packed artifact behind them.

## Two tsconfigs, on purpose

`tsconfig.json` is the strict one above and covers shipped source only. `tsconfig.node.json`
covers the specs and build configs — `vite.config.ts` reaches `@bc-solutions-coder/config`,
which imports `node:url`. `pnpm typecheck` runs **both**; neither file is in `exports`, so
letting them see Node costs the charter nothing.

## Adding a helper

- **Extract, do not invent.** Add a helper from app code that already exists and rewire the
  call sites in the same commit; a helper with no consumer is dead API.
- A new module is a new subpath: `src/<name>.ts`, an `exports` and `publishConfig.exports`
  entry, a `vite.config.ts` lib entry and a `tsconfig.build.json` include — all four, every
  time (`wallow/module-lists-in-sync` fails `pnpm lint` naming the missing list).
- Keep every function **total** — answer `undefined` rather than throwing. Callers are
  reached by junk links and malformed payloads, where the required behaviour is a usable
  screen.

Scripts: `pnpm --filter @bc-solutions-coder/utils build` (Vite lib mode +
`tsc -p tsconfig.build.json`), `test`, `test:watch`, `typecheck`.
