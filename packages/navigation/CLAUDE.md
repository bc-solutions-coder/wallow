# packages/navigation — @bc-solutions-coder/navigation Agent Guide

The application shell: desktop rail, mobile drawer, their controls, and the shared store.

## One entry, deliberately

`useNavStore` is a module-global zustand singleton, so a second subpath would be a second
store — a rail and drawer disagreeing about open state fail silently. Hence one export-map
entry, and `zustand`/`react`/`react-dom`/`@tanstack/react-router` as **peerDependencies**.
`@tanstack/react-router` peers a literal caret range, not `catalog:start` — deliberate; do
not "fix" it into the catalog.

## Dependency edges it does NOT have (each absence is a decision)

- **No `@bc-solutions-coder/sdk`** — sign-out is a footer slot the app supplies.
- **No auth edge** — visibility is the app-supplied `can()` predicate; `requires` is inert here.
- **No `@bc-solutions-coder/styles`** — tokens arrive as CSS through the consuming app's stylesheet.
- **No `@bc-solutions-coder/utils`.**

Import `@bc-solutions-coder/ui` by **per-component subpath**, never the root barrel: the
barrel drags in `FocusOnNavigate` → `useRouterState`, and the specs here stub
`@tanstack/react-router` down to `Link` alone — the barrel fails to link in a dev/test graph.

## The SSR hazard a fork must configure

`use-sync-external-store/shim` is CJS whose `require("react")` survives an SSR build as a
runtime `__require`, loading a **second React** — every zustand-backed component then throws
"Invalid hook call" during SSR and the app silently falls back to client-only rendering with
an empty document. `wallowAppConfig` already carries both aliases; a fork hand-rolling its
`vite.config.ts` needs:

```ts
resolve: {
  alias: [
    // Anchored regexes, NOT bare strings — a string alias matches by PREFIX and would
    // rewrite `…/shim/with-selector` to the nonexistent `react/with-selector`.
    { find: /^use-sync-external-store\/shim$/u, replacement: "react" },
    { find: /^use-sync-external-store\/shim\/index\.js$/u, replacement: "react" },
  ],
}
```

## Testids are a contract

Every testid derives from `testIdPrefix` (default `"dashboard"`) plus the destination `id`
(derivation in `src/app-shell.tsx`). wallow-web's Playwright suites select on these strings —
changing the derivation breaks E2E in another package.

## Testing

- The **browser project here loads real Tailwind and the fork theme**, unlike `packages/ui`'s
  plain browser project — that is why this package has no Storybook.
- `app-shell.ssr-flash.test.tsx` deliberately does NOT match the `.ssr.` suffix: it measures
  visible chrome in real Chromium, so it must stay on the browser project.
- `class-variance-authority` and `tailwind-merge` are devDependencies solely so browser
  pre-bundling can resolve them (reached through `ui`'s `*.styles.ts`).

Lint: `app-shell.tsx`'s `BACKDROP_SCRIM` is the alpha spelling `no-sidebar-inversion`
deliberately allows; a bare `bg-foreground` fails `pnpm lint`.

Not in scope: `PublicLayout` (wallow-web) and `auth-layout.tsx` (wallow-auth) stay in their apps.
