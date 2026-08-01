# packages/navigation — @bc-solutions-coder/navigation Agent Guide

The **application shell**: a collapsible desktop rail, a mobile overlay drawer, the three
controls that drive them, and the store the two halves share. `apps/wallow-web`'s
`DashboardLayout` is 36 lines because everything else lives here.

What a consumer supplies is the three things only an app can answer:

| Prop                | What it answers                                                           |
| ------------------- | ------------------------------------------------------------------------- |
| `destinations`      | WHICH destinations exist, in render order (`NavDestination[]`)            |
| `can`               | WHO may see each one — given a destination's `requires`, return a boolean |
| `header` / `footer` | What sits above and below the destinations; each takes `showLabel`        |
| `children`          | The routed content — an app passes its router's `<Outlet />`              |

`icons`, `labels` and `testIdPrefix` override the defaults. Everything else — the three
modes, the scrim, the aria wiring, the theme toggle — is the package's.

## One entry, deliberately

`packages/ui` is a per-component catalog; this is **one cohesive frame with one export map
entry**, and that is load-bearing rather than a convention. `useNavStore` is a module-global
zustand singleton: exporting it from a second subpath is a second store, and a nav whose rail
and drawer disagree about whether it is open fails silently. Same hazard class
`@bc-solutions-coder/query` exists to solve for `QueryClient`. `zustand`, `react`, `react-dom`
and `@tanstack/react-router` are therefore **peerDependencies**, never dependencies.

`@tanstack/react-router` peers a literal `^1.170.18` rather than `catalog:start`. A library
peering a caret range against an app pinning exactly is correct practice, not drift — do not
"fix" it into the catalog.

## The dependency edges it does NOT have

Each absence is a decision, and each is easy to reverse by accident:

- **No `@bc-solutions-coder/sdk`.** The pre-extraction `DashboardNav` imported `logout`. The
  "sign-out is a footer slot" decision is what deleted that import, and the control now lives
  at `apps/wallow-web/src/shared/components/SignOut.tsx`. An implementer who adds an SDK edge
  back has reversed the decision that makes this package reusable.
- **No auth edge.** Visibility is the app-supplied `can()` predicate; `requires` is inert here
  and handed straight back.
- **No `@bc-solutions-coder/styles`.** `ui` already depends on it and the tokens arrive as CSS
  through the consuming app's stylesheet.
- **No `@bc-solutions-coder/utils`.** Nothing calls anything utils-shaped.

`@bc-solutions-coder/ui` is imported by **per-component subpath** (`/button`,
`/error-banner`, `/navigation-menu`, `/theme-toggle`), never the root barrel. The barrel drags
in `FocusOnNavigate` → `useRouterState`, and the specs here stub `@tanstack/react-router` down
to `Link` alone. A bundler tree-shakes that away; a dev/test module graph does not, so the
barrel fails to link. Keep the comment that says so with the code.

## What a fork must configure — the SSR hazard

`use-sync-external-store/shim` is CJS whose `require("react")` Rolldown leaves as a runtime
`__require`, so an SSR build loads a **second React** beside the bundled one. Every
zustand-backed component — which is this whole package — then throws "Invalid hook call"
during SSR and the page falls back to client-only rendering with an **empty document**. The
failure is silent: no error surfaces, the app merely stops server-rendering.

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

Two specs guard the SSR path. `use-is-desktop.ssr.test.tsx` `renderToString`s a probe over the
hook and asserts its value is `"undefined"` — not `"true"`, not `"false"` — because the server
knows no viewport and committing to either paints a flash hydration then corrects.
`app-shell.ssr-flash.test.tsx` renders the whole shell the same way and asserts no
viewport-specific chrome is visible in that markup.

## Testids are a contract

Every testid derives from `testIdPrefix` (default `"dashboard"`) plus the destination's `id`:
`dashboard-shell`, `dashboard-nav`, `dashboard-nav-drawer`, `dashboard-nav-toggle`,
`dashboard-nav-mobile-menu`, `dashboard-nav-backdrop`, `dashboard-nav-organizations`. Those
strings are byte-identical to the pre-extraction app's, which is why wallow-web's Playwright
suites did not churn. Changing the derivation breaks E2E in another package.

## Testing

`vitest.config.ts` takes the node/browser split from `@bc-solutions-coder/testing` and adds
`wallowStyles()` plus `./vitest.setup.ts` — so the **browser project here loads real Tailwind
and the fork theme**, which `packages/ui`'s plain browser project does not. That is why this
package has no Storybook: in `ui`, stories exist because only the `storybook` project loads
the token pipeline; here every spec already has it, and a second Storybook instance would buy
nothing the specs do not measure.

Two naming rules bite:

- **`*.ssr.test.tsx` routes a spec onto the NODE project.** `use-is-desktop.ssr.test.tsx`
  wants that (it only calls `renderToString`).
- **`app-shell.ssr-flash.test.tsx` deliberately does NOT match that suffix.** It renders
  server markup _inside Chromium_ and measures which chrome is visible at a real viewport, so
  it must stay on the browser project.

Every runtime a spec touches must be in `extraBrowserOptimizeDeps`, including transitive ones
(`class-variance-authority`, `tailwind-merge`, reached through `ui`'s `*.styles.ts`) — and a
package can only pre-bundle what it can **resolve**, which is why those two carry their own
devDependency entries. Left undiscovered they land mid-run, Vite reloads, and the runner dies.

`shell-source.test.ts` is the one disk-reading guard: it sweeps `app-nav.tsx` for
`hover:text-*` colours (a hover text colour here exists only to out-merge a catalog recipe)
and `app-shell.tsx` for hand-rolled button utilities the `buttonRecipe` should emit. Both
demonstrate their own matcher on a fixture string first, so a matcher that found nothing
cannot turn the real case into a spec that cannot fail.

## Not in scope

`PublicLayout` stays in `apps/wallow-web` and `auth-layout.tsx` stays in `apps/wallow-auth`.
Folding either in is the over-reach that made the deleted web-shell package a grab-bag.
