# packages/ui — @bc-solutions-coder/ui Agent Guide

The shared **browser-only** React component library: one folder per component under
`src/components/`, each a **Base UI** (`@base-ui/react`) headless part wrapped in a **CVA**
recipe built from `@bc-solutions-coder/styles` semantic tokens. Private, consumed by all
three apps and by `packages/forms` and `packages/navigation`. The app-wiring folders
(`ReadyIndicator`, `FocusOnNavigate`, `DocumentStyles`, `ForkAttribution`, `theme-provider`,
`failure-messages`) render nothing a story could show and are the only ones without stories.

## Layering

`src/core/` (imports nothing internal) → `src/components/<name>/` (may import `core/`) →
`src/index.ts` (the root barrel; the only file importing every folder). One direction.

Cross-component imports are for **deliberate reuse**, not convenience: `Label` re-exports
Field's label part; `AlertDialog`/`ContextMenu`/`Autocomplete` reuse the already-wrapped
parts of `Dialog`/`Menu`/`Combobox` (Base UI re-exports those runtimes verbatim — re-wrapping
would fork behaviour). A folder that composes rather than wraps declares no recipe and has no
`.styles.ts`.

Composition choice rules: `QuietLink` (a plain `<a>`, real cross-origin hrefs) is for asides;
`Button variant="link"` is for the action the screen wants next. `NoticeBanner` is a
**sibling** of `ErrorBanner`, not a `tone` axis on it.

## Failure surfaces

`ErrorBanner` stays the string primitive. The failure model (`CONTEXT.md` § Errors) adds three
folders on top of `@bc-solutions-coder/api-errors`, the package's one non-catalog dependency:

- **`failure-messages`** — `FailureMessagesProvider({ registry })` publishes the app's
  `defineFailureMessages` registry; `useFailureMessage(error, { messages?, fallback? })`
  resolves the sentence through `resolveFailureMessage` and returns `null` for a nullish error.
  Empty-registry default, so the hook answers without a provider. A nested provider
  **replaces**, never merges — per-call-site sentences go through `messages`.
- **`failure-banner`** — `FailureBanner({ error, messages?, fallback?, onRetry?, signInHref?,
children? })` wraps `ErrorBanner`, renders nothing for a nullish `error`, and adds only what
  the status rule allows: "Try again" when `onRetry` is given; a "Sign in" link for the 401
  codes (`Auth.Unauthenticated`, `Bff.SessionMissing`, `Bff.SessionRefreshFailed`) — to
  `/bff/login?returnTo=<current path>` by default, built here rather than imported because `ui`
  must not depend on the SDK, or to `signInHref` for an app without a BFF (wallow-auth); and
  the `Reference <id>` line with "Copy reference" only when api-errors' `failureReference`
  answers (transport and 5xx; trace id, else request id — the rule lives THERE, not here). The
  path comes from `useSyncExternalStore` with a `"/"` server snapshot so SSR never mismatches.
- **`failure-toast`** — **sonner is the documented exception to "every component wraps Base
  UI"**: the Base UI toast wrapper is deleted and must not come back. `FailureToaster` mounts
  sonner's `<Toaster>` bottom-right with a close button, `theme` fed from `useTheme().mode`;
  `toastFailure(message, reference?)` raises `toast.error` with the reference line and a copy
  action that `preventDefault()`s so the toast stays; a referenced toast has no timeout (only
  the close button ends it), an unreferenced one keeps sonner's default. sonner renders inline
  (no portal), its stylesheet is **unlayered**, so every token in `TOAST_CLASSNAMES` carries
  Tailwind's `!` suffix, and its toast store is a **module singleton** — a story or spec must
  `toast.dismiss()` and wait for the exit animation before asserting an empty screen. Its
  accessibility model is sonner's, not Base UI's: one `aria-live="polite"` region for every
  toast, Escape collapses the stack rather than closing, and `FailureToaster` must sit under
  `ThemeProvider` (`useTheme` falls back to light without one). sonner is in both browser
  projects' `optimizeDeps.include` for the same mid-run-reload reason as the recipe runtime.

The query side (`createQueryClient({ onUnhandledFailure })`, `handledFailure`,
`toastedFailure`) lives in `packages/query`; wiring the callback to `toastFailure` is the app's
job, with the registry in scope.

## Parts and recipes

- **Multi-part components export ONE namespace object** whose keys mirror Base UI's part
  names 1:1. Verify the real anatomy against the installed package's
  `<component>/index.parts.d.ts` (under `node_modules/.pnpm/@base-ui+react@…` — Base UI is
  not symlinked at the repo root), never from memory or a design doc.
- **`className` is narrowed back to `string`** on every part: Base UI widens it to a state
  callback, which `cn()` cannot merge. Import each part from its own subpath
  (`@base-ui/react/dialog`), never the package root.
- Recipes reference **only semantic token utilities** (`bg-primary`,
  `text-muted-foreground`, …) — never a raw colour; add a missing token to
  `packages/styles` first.
- Style state off Base UI's `data-*` attributes (`data-[disabled]`, `data-[open]`), not
  pseudo-classes, so the recipe survives a caller composing the part onto another element
  via `render`.
- **Tailwind `@source` must scan `*.tsx` AND `*.styles.ts`.** Most class names live in
  recipe files; a `.tsx`-only scan emits no CSS for them — a silent failure, correct in the
  DOM, unstyled in the app. Both `source.css` and `.storybook/preview.css` carry both globs.
- `@bc-solutions-coder/styles` is a **devDependency only** (Storybook preview); `src/` must
  never import it.
- **`surface: page | sidebar` is the axis for WHICH PALETTE a control paints from**, separate
  from `variant`. Two rules when adding it: declare the axis **LAST** so tailwind-merge
  collapses each pair in its favour — that ordering IS the mechanism — and have the
  `sidebar` arm restate **every** colour dimension a `variant` arm can set (rest/hover
  surface and text, border), since a dimension left unnamed stays a page colour on the rail.

## Exports — barrel _and_ subpath

- `dist/` mirrors `src/` 1:1 (`preserveModules`), so the folder structure IS the subpath
  structure.
- **The root barrel carries components and their prop types only.** A recipe stays reachable
  through `@bc-solutions-coder/ui/<name>` alone — the barrel takes a folder's `<name>.tsx`
  export block, never the `<name>.styles.ts` one.
- **Two files encode the catalog as one exact set and must move together in one commit**:
  `src/index.ts`, and `PUBLIC_RUNTIME_EXPORTS` + the `PublicTypeExports` tuple in
  `src/index.test.ts`. Growing one alone turns the other red.

## Storybook and the test model

`pnpm --filter @bc-solutions-coder/ui test` runs **three Vitest projects**: `node`, `browser`
(headless Chromium), and `storybook` — every story as a test case, with the real Tailwind
pipeline and fork theme attached (explorer: `pnpm --filter @bc-solutions-coder/ui storybook`,
:6006).

- **Stories ARE the render/interaction coverage** — all variants and states, `play()` for
  interactive ones. `*.test.tsx` covers only edges a story cannot express. No mocking — see
  `.claude/rules/TESTING.md` and `packages/testing/CLAUDE.md`.
- **Base UI pre-bundling needs no per-component step**: `optimizeDeps.include` takes the
  single glob `@base-ui/react/*`, repeated for the `storybook` project (its own Vite server,
  its own dep cache). `src/core/browser-deps.test.ts` checks every entry in both lists
  resolves — mechanism in `packages/testing/CLAUDE.md`.
- **The `browser` project loads no Tailwind**, so a component whose box comes only from its
  recipe and has no text (switch, checkbox, radio, slider, toggle) measures 0×0 and
  `userEvent.click` hangs to the actionability timeout. Activate with the DOM's own
  `element.click()`, or `element.focus()` + `userEvent.keyboard(" ")`. Story play functions
  are unaffected (`storybook/test`'s userEvent is synthetic and visibility-blind), so
  pointer interaction belongs in stories.
- **The escape-guard trio is wired TWICE**: `vitest.setup.ts` (via `browserSetupFiles`)
  covers the `browser` project; `.storybook/preview.tsx`'s named `beforeEach`/`afterEach`
  exports cover the `storybook` one, because `storybookTest()` never reads
  `browserSetupFiles`.
- **Popup unmount is animation-frame-deferred** for the whole Base UI popup family — assert
  closure with `await expect.poll(() => ...).toBeNull()`, never a bare synchronous `expect`.

## Gotchas

- **A `.dark` WRAPPER DIV IS VACUOUS — the class only works on `document.documentElement`.**
  `@theme` declares each token (`--color-sidebar: var(--sidebar, …)`) on `:root`, and a
  `var()` inside a custom property substitutes at computed-value time on the DECLARING
  element — a descendant `.dark` rebinds the raw variable while the token above it keeps the
  light value. Reference implementation: `.storybook/scheme-decorators.tsx` paired with
  `expectScheme` (`.storybook/scheme-assertions.ts`), which measures that the palette is the
  right way up — never assert a scheme from a class string; the markup is identical either way.
- **`Button` supplies `role="link"` itself for composed anchors — never pass a `role`.** Base
  UI's `useButton` merges `role="button"` onto every non-native element it composes onto, so
  a `render`-composed anchor would announce a navigation as an action (WCAG 2.2 SC 4.1.2).
  The role is spread BEFORE `rest` so it stays a caller-overridable default;
  `role={undefined}` would delete the `role="button"` a composed `<div>` depends on. Assert
  with `getByRole("link")`.
- **`ReadyIndicator` stamps the E2E hydration marker** (`data-app-ready`) every Playwright
  suite waits on; the catalog component is the only place it is written — changing it breaks
  E2E readiness in all three apps.
- This package **publishes** a prebuilt `dist/`, so `import.meta.env.DEV` inside a component
  bakes in the _library's_ build env for an installed consumer (and differs between this
  workspace and a fork). Anything that must reflect the app's build (e.g. `DocumentStyles`'
  stylesheet href) is decided in the app shell and passed as a prop.
- React, `react-dom`, and `@tanstack/react-router` are **peer** dependencies — keep them out
  of `dependencies`.
- Lint config detail (inheritance, the `drawer.styles.ts` exemption) is in
  `packages/lint/CLAUDE.md` and `//` comments in `.oxlintrc.json` — do not restate it here.

Consumer-facing docs (how an app imports components, how to add one):
`docs/development/component-library.md`.
