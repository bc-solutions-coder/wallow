# packages/utils — @bc-solutions-coder/utils Agent Guide

Subpath-only — deliberately **no `.` barrel**, so an import names the module it depends on.

## The charter, machine-enforced

Each point fails a build or lint, not a spec:

1. **It depends on nothing.** `dependencies` and `peerDependencies` are both `{}`.
2. **It compiles against no host API.** `tsconfig.json` sets `"lib": ["ESNext"]` and
   `"types": []`; `lib` restates the whole list rather than subtracting, because the base
   config adds DOM.
3. **It imports nothing.** A root `.oxlintrc.json` override keyed on
   `packages/utils/src/**/*.ts` bans `react`, `react-dom`, `zustand` and
   `@bc-solutions-coder/*` — and restates every root-level ban, because an oxlint override
   REPLACES the rule's options rather than merging them.
4. **Every module on disk is reachable, and nothing else is.** `wallow/module-lists-in-sync`
   diffs the four lists — `exports`, `publishConfig.exports`, `vite.config.ts` lib entries,
   `tsconfig.build.json` include — and `pnpm check:exports` checks the packed artifact.

## Two tsconfigs, on purpose

`tsconfig.json` (the strict one above) covers shipped source only; `tsconfig.node.json`
covers the specs and build configs, which reach Node. `pnpm typecheck` runs both; neither is
in `exports`, so letting them see Node costs the charter nothing.

## Adding a helper

- **Extract, do not invent** — add a helper from app code that already exists and rewire the
  call sites in the same commit; a helper with no consumer is dead API.
- A new module is a new subpath: `src/<name>.ts` plus an entry in all four lists, every time.
- Keep every function **total** — answer `undefined` rather than throwing; callers are
  reached by junk links and malformed payloads, where the required behaviour is a usable
  screen.
