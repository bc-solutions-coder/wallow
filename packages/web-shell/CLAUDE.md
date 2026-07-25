# packages/web-shell — @bc-solutions-coder/web-shell Agent Guide

The **host runtime + build presets** every Wallow frontend boots from: the standalone h3
host, the dev-server, and the shared Vite client/SSR config factories. An app's
`server.ts`, `dev-server.ts`, and `vite*.config.ts` are thin wrappers over these.

## Two entries — the split is load-bearing

| Entry | Runs in | What it is |
|-------|---------|-----------|
| `.` (`src/index.ts`) | Browser | `createQueryClient` — the shared TanStack Query client factory. Must stay free of Node APIs; it is imported from client bundles. |
| `./server` (`src/server/`) | Node | `createStandaloneHost` (+ `ShellConfig`), `createDevServer`, `createClientViteConfig` / `createSsrViteConfig`, and `createStaticAssetReader` for the built client. |

- **Never move a Node-only symbol onto the root entry** — it would be pulled into every
  consuming app's client bundle. New host/build code goes behind `./server`.
- Depends on `@bc-solutions-coder/styles` (`wallowStyles()` is folded into the Vite presets),
  `@tanstack/router-plugin`, and `@vitejs/plugin-react` — so apps do not wire those themselves.
- Scripts: `pnpm --filter @bc-solutions-coder/web-shell build` (Vite lib mode +
  `tsc -p tsconfig.build.json`), `test`, `test:watch`, `typecheck`. Tests are node-environment
  vitest co-located in `src/`; `route-tree-drift-workflow.test.ts` guards CI wiring — update it
  when changing the route-tree generation workflow.
