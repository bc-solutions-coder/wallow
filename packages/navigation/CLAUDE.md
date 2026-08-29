# packages/navigation — @bc-solutions-coder/navigation Agent Guide

The **application shell**: a collapsible desktop rail, a mobile overlay drawer, the three
controls that drive them, and the store the two halves share. `apps/wallow-web`'s
`DashboardLayout` is a thin wrapper because everything else lives here.

A consumer supplies the things only an app can answer:

| Prop                | What it answers                                                           |
| ------------------- | ------------------------------------------------------------------------- |
| `destinations`      | WHICH destinations exist, in render order (`NavDestination[]`)            |
| `can`               | WHO may see each one — given a destination's `requires`, return a boolean |
| `header` / `footer` | What sits above and below the destinations; each takes `showLabel`        |
| `children`          | The routed content — an app passes its router's `<Outlet />`              |

`icons`, `labels` and `testIdPrefix` override the defaults. Everything else — the three
modes, the scrim, the aria wiring, the theme toggle — is the package's.

## One entry, deliberately

This is **one cohesive frame with one export map entry** (unlike `packages/ui`'s
per-component catalog), and that is load-bearing: `useNavStore` is a module-global zustand
singleton, so a second subpath would be a second store, and a nav whose rail and drawer
disagree about whether it is open fails silently. `zustand`, `react`, `react-dom` and
`@tanstack/react-router` are therefore **peerDependencies**, never dependencies.

`@tanstack/react-router` peers a literal caret range rather than `catalog:start`. A library
peering a caret range against an app pinning exactly is correct practice, not drift — do not
"fix" it into the catalog.

## The dependency edges it does NOT have

Each absence is a decision — do not add the edge back:

- **No `@bc-solutions-coder/sdk`.** Sign-out is a footer slot; the control lives at
  `apps/wallow-web/src/shared/components/SignOut.tsx`. An SDK edge here makes the package
  non-reusable.
- **No auth edge.** Visibility is the app-supplied `can()` predicate; `requires` is inert
  here and handed straight back.
- **No `@bc-solutions-coder/styles`.** `ui` already depends on it; tokens arrive as CSS
  through the consuming app's stylesheet.
- **No `@bc-solutions-coder/utils`.**

`@bc-solutions-coder/ui` is imported by **per-component subpath** (`/navigation-menu`,
`/button` — for `Button` and `buttonRecipe` — and `/theme-toggle`), never the root barrel.
The barrel drags in `FocusOnNavigate` → `useRouterState`, and the specs here stub
`@tanstack/react-router` down to `Link` alone — a bundler tree-shakes that away; a dev/test
module graph does not, so the barrel fails to link. Keep the comment that says so with the
code.

## What a fork must configure — the SSR hazard

`use-sync-external-store/shim` is CJS whose `require("react")` Rolldown leaves as a runtime
`__require`, so an SSR build loads a **second React** beside the bundled one. Every
zustand-backed component — this whole package — then throws "Invalid hook call" during SSR
and the page falls back to client-only rendering with an **empty document**. The failure is
silent: no error surfaces, the app merely stops server-rendering.

Both aliases are already in `wallowAppConfig` (`packages/config/src/vite/app.ts`), so an app
built on the shared preset inherits them. A fork that hand-rolls its `vite.config.ts` needs:

```ts
resolve: {
  alias: [
    // Anchored regexes, NOT bare strings: a string alias matches by PREFIX and
    // would rewrite `…/shim/with-selector` to the nonexistent `react/with-selector`.
    { find: /^use-sync-external-store\/shim$/u, replacement: "react" },
    { find: /^use-sync-external-store\/shim\/index\.js$/u, replacement: "react" },
  ],
}
```

Two specs guard the SSR path. `use-is-desktop.ssr.test.tsx` `renderToString`s a probe over
the hook and asserts its value is `"undefined"` — not `"true"`, not `"false"` — because the
server knows no viewport and committing to either paints a flash hydration then corrects.
`app-shell.ssr-flash.test.tsx` renders the whole shell the same way and asserts no
viewport-specific chrome is visible in that markup.

## Testids are a contract

Every testid derives from `testIdPrefix` (default `"dashboard"`) plus the destination's
`id`: `dashboard-shell`, `dashboard-nav`, `dashboard-nav-drawer`, `dashboard-nav-toggle`,
`dashboard-nav-mobile-menu`, `dashboard-nav-backdrop`, `dashboard-nav-organizations`.
wallow-web's Playwright suites select on these strings — changing the derivation breaks E2E
in another package.

## Testing

`vitest.config.ts` takes the node/browser split from `@bc-solutions-coder/testing` and adds
`wallowStyles()` plus `./vitest.setup.ts` — so the **browser project here loads real
Tailwind and the fork theme**, which `packages/ui`'s plain browser project does not. That is
why this package has no Storybook: every spec already has the token pipeline.

`vitest.setup.ts` also installs `@bc-solutions-coder/testing`'s navigation-escape guard and
asserts on it after every test, so a cross-document hand-off that reaches the iframe fails
the test that caused it — without the guard the leak presents as an intermittent flake in a
neighbouring file. `navigation-escape.test.tsx` proves the wiring is live.

Two naming rules bite:

- **`*.ssr.test.tsx` routes a spec onto the NODE project.** `use-is-desktop.ssr.test.tsx`
  wants that (it only calls `renderToString`).
- **`app-shell.ssr-flash.test.tsx` deliberately does NOT match that suffix.** It renders
  server markup _inside Chromium_ and measures which chrome is visible at a real viewport,
  so it must stay on the browser project.

Every runtime a spec touches must be in `extraBrowserOptimizeDeps`, including transitive
ones (`class-variance-authority`, `tailwind-merge`, reached through `ui`'s `*.styles.ts`) —
and a package can only pre-bundle what it can **resolve**, which is why those two carry
their own devDependency entries. Left undiscovered they land mid-run, Vite reloads, and the
runner dies.

## Lint

`.oxlintrc.json` here `extends` the root config and enables **all six** `wallow/*` rules —
mechanics in `packages/lint/CLAUDE.md`. `app-shell.tsx`'s `BACKDROP_SCRIM` is the alpha
spelling `no-sidebar-inversion` deliberately allows; a bare `bg-foreground` fails
`pnpm lint`. The standard test/story override turns off four rules
(`no-sidebar-inversion`, `no-tinted-text`, `text-heading-variant`,
`no-hand-rolled-mutation`); `zone-dag` and `no-source-tests` stay on. A control that paints
itself instead of composing `buttonRecipe` is caught on review, not by lint.

## Not in scope

`PublicLayout` stays in `apps/wallow-web` and `auth-layout.tsx` stays in
`apps/wallow-auth`. Folding either in makes this a grab-bag.
