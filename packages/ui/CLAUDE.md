# packages/ui — @bc-solutions-coder/ui Agent Guide

The shared **browser-only** React component library: primitives (`Button`, `Input`, `Label`,
`Field`, `Card`, `ErrorBanner`, `MutedText`), layouts (`CenteredCardLayout`), and app-wiring
components (`FocusOnNavigate`, `ReadyIndicator`, `DocumentStyles`, `ForkAttribution`).
Everything public is re-exported from `src/index.ts`; there is no node-only subpath.

- **`ReadyIndicator` stamps the E2E hydration marker** (`READY_ATTRIBUTE` → `data-app-ready`)
  that every Playwright suite waits on. Changing it breaks E2E readiness across both apps.
- **`./source.css` is a Tailwind `@source` declaration only** — Tailwind v4 skips
  `node_modules`, so each consuming app must `@import "@bc-solutions-coder/ui/source.css"`
  from its CSS entry for these components' utilities to be emitted. It imports no theme:
  ui depends on `@bc-solutions-coder/styles` conceptually, never the reverse.
- **Tests run in real headless Chromium** (Vitest browser mode via `@bc-solutions-coder/testing`).
  Every component gets a co-located `*.test.tsx`. NEVER jsdom or happy-dom — see
  `.claude/rules/TESTING.md`.
- React and `@tanstack/react-router` are **peer** dependencies; keep them out of `dependencies`.
- Scripts: `pnpm --filter @bc-solutions-coder/ui build` (Vite lib mode + `tsc -p tsconfig.build.json`),
  `test`, `test:watch`, `typecheck`. `.oxlintrc.json` here turns off `react/jsx-props-no-spreading`.
