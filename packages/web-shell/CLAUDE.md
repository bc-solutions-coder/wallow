# packages/web-shell — @bc-solutions-coder/web-shell Agent Guide

The **shared browser-safe frontend runtime**: today that is exactly one export,
`createQueryClient`, the TanStack Query client factory every app wires into its router
context and its `__root` `QueryClientProvider`.

## One entry, browser-safe by construction

| Entry                | Runs in | What it is                                                                     |
| -------------------- | ------- | ------------------------------------------------------------------------------ |
| `.` (`src/index.ts`) | Browser | `createQueryClient` — the shared TanStack Query client factory. Must stay free |
|                      |         | of Node APIs; it is imported from client bundles as well as SSR.               |

- **Never add a Node-only symbol here** — everything in this package is pulled into every
  consuming app's client bundle. There is no `./server` subpath to hide it behind any more.
- The package previously shipped a hand-rolled SSR host runtime behind `./server`
  (`createStandaloneHost`, `createDevServer`, `createClientViteConfig` / `createSsrViteConfig`,
  `createStaticAssetReader`). All three apps are on **TanStack Start**, which owns hosting,
  the dev server, and route codegen, so that layer was deleted — it is readable in git
  history if a fork needs it. Do not reintroduce it: host concerns belong in the app's own
  Start server routes and `vite.config.ts`.
- Scripts: `pnpm --filter @bc-solutions-coder/web-shell build` (Vite lib mode +
  `tsc -p tsconfig.build.json`), `test`, `test:watch`, `typecheck`. Tests are node-environment
  vitest co-located in `src/`.
