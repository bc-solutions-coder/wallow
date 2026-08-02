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

Three subpaths, not five. `./array` and `./result` are named in the epic but unbuilt: nothing
in the workspace needed them, and a declared-public subpath with no consumer is API invented
rather than extracted. Adding one later is additive.

## The charter, and why it is machine-enforced

A generic utility package is only worth having while these hold. `src/charter.test.ts`, which
asserted them by reading the manifest and every module's source off disk, is gone
(`Wallow-xg9t.1`) — what is left is the machinery each point names, which fails a **build**
rather than a spec:

1. **It depends on nothing.** `dependencies` and `peerDependencies` are both `{}`.
2. **It compiles against no host API.** `tsconfig.json` sets `"lib": ["ESNext"]` and
   `"types": []`, so a `window`, a `document`, a `process` or a `node:*` import does not
   compile. `lib` restates the whole list rather than subtracting, because the base config
   adds DOM.
3. **It imports nothing.** A root `.oxlintrc.json` override keyed on `packages/utils/src/**/*.ts`
   bans `react`, `react-dom`, `zustand` and `@bc-solutions-coder/*`. It **restates every ban the
   root rule makes**, because an oxlint `overrides` entry REPLACES the rule's options rather
   than merging them — an override listing only the charter bans silently unbans the rest.
4. **Every module on disk is reachable, and nothing else is.** The `exports` map,
   `publishConfig.exports`, `vite.config.ts`'s lib entries and `tsconfig.build.json`'s `include`
   all have to agree — a subpath with no lib entry resolves, at publish time, to a file that was
   never written. `pnpm check:exports` (publint + `@arethetypeswrong/cli` over the packed
   tarball) is what catches that now, against the real artifact rather than four config files.

## Two tsconfigs, on purpose

`tsconfig.json` is the strict one above and covers the shipped source only. `tsconfig.node.json`
covers the specs and the build configs — `vite.config.ts` reaches
`@bc-solutions-coder/config`, which imports `node:url`. `pnpm typecheck`
runs **both**; neither of those files is in `exports`, so letting them see Node costs the charter
nothing.

## Adding a helper

- **Extract, do not invent.** Every function here came from app code that already existed, and
  the call sites were rewired onto it in the same commit. A helper with no consumer is dead
  API that still has to be maintained.
- A new module is a new subpath: add `src/<name>.ts`, an `exports` and `publishConfig.exports`
  entry, a `vite.config.ts` lib entry and a `tsconfig.build.json` include. All four, every time
  — nothing in the test suite reminds you any more, and a missing lib entry only surfaces at
  `pnpm check:exports`.
- Keep every function **total** — answer `undefined` rather than throwing. The callers are
  reached by junk links and malformed payloads, where the required behaviour is a usable
  screen.

Scripts: `pnpm --filter @bc-solutions-coder/utils build` (Vite lib mode + `tsc -p
tsconfig.build.json`), `test`, `test:watch`, `typecheck`.
