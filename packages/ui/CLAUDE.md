# packages/ui — @bc-solutions-coder/ui Agent Guide

The shared **browser-only** React component library: 47 component folders under
`src/components/`, each a **Base UI** (`@base-ui/react` ^1.6.0) headless part wrapped in a
**CVA** recipe built from `@bc-solutions-coder/styles` semantic tokens. Private (never
published), consumed by `apps/wallow-auth` and `apps/wallow-web` as `workspace:*`.

Four of those folders are app-wiring rather than visual primitives — `ReadyIndicator`,
`FocusOnNavigate`, `DocumentStyles`, `ForkAttribution` — and are the only ones without a
story.

## Layering — three layers, one direction

| Layer | Path                     | Rule                                                                                                    |
| ----- | ------------------------ | ------------------------------------------------------------------------------------------------------- |
| 0     | `src/core/`              | `cn.ts` (tailwind-merge wrapper) plus the package's own scaffold guards. Imports **nothing internal**.  |
| 1     | `src/components/<name>/` | One folder per component. May import `core/`; may import a sibling component only for deliberate reuse. |
| 2     | `src/index.ts`           | The root barrel — the only file that imports every component folder.                                    |

`core` never imports `components`. Cross-component imports are for **deliberate reuse**, not
convenience: `Label` is literally `Field.Label`, `label/index.ts` re-exports Field's recipe,
and `AlertDialog`/`ContextMenu`/`Autocomplete` reuse the already-wrapped parts of
`Dialog`/`Menu`/`Combobox` (Base UI re-exports those runtimes verbatim — re-wrapping them
would fork the behaviour).

## Component folder anatomy

| File                 | Holds                                                                                         |
| -------------------- | --------------------------------------------------------------------------------------------- |
| `<name>.tsx`         | The parts. Each wraps a real Base UI part: `cn(recipe({...}), className)` + full passthrough. |
| `<name>.styles.ts`   | **CVA recipes only** — no JSX, no React import, so a recipe reads and diffs on its own.       |
| `<name>.stories.tsx` | Storybook stories = this component's render/interaction coverage (see below).                 |
| `<name>.test.tsx`    | Behavioural edges a story cannot express.                                                     |
| `index.ts`           | The folder barrel: the `<name>.tsx` export block, then the `<name>.styles.ts` one.            |

- **Multi-part components export ONE namespace object** whose keys mirror Base UI's part
  names 1:1 (`export const Dialog = { Root, Trigger, Portal, Backdrop, Popup, ... }`), so a
  caller who knows the Base UI docs already knows this API. Unstyled parts pass through
  unwrapped. Verify the real anatomy against the installed package's
  `<component>/index.parts.d.ts` (`node_modules/.pnpm/@base-ui+react@1.6.0.../` — Base UI is
  not symlinked at the repo root), never from memory or a design doc.
- **`className` is narrowed back to `string`** on every part: Base UI widens it to a state
  callback, which `cn()` cannot merge. Catalog-wide, a caller's `className` therefore always
  means "utilities merged over the recipe, last one wins".
- Import each part from its own subpath (`@base-ui/react/dialog`), never the package root.

## Recipes and design tokens

- Recipes reference **only semantic token utilities already defined by
  `@bc-solutions-coder/styles`** (`bg-primary`, `text-muted-foreground`, `border-border`,
  `rounded-md`, …). Never a raw colour. A missing token is added to
  `packages/styles/styles.css`'s `@theme` (plus `api/branding.json`) **first**.
- Style state off Base UI's `data-*` attributes (`data-[disabled]`, `data-[open]`), not the
  `:disabled`/`:open` pseudo-classes, so the recipe still applies when a caller composes the
  part onto another element via `render`.
- **Tailwind `@source` must scan `*.tsx` AND `*.styles.ts`.** Since the CVA rebuild most
  class names live in the recipe file; a `.tsx`-only scan emits no CSS for them, and the
  component renders correct class _attributes_ with nothing behind them — it looks right in
  the DOM and in `classList` specs while rendering unstyled in the app (this bit `Switch`).
  Both `source.css` (governs consuming apps) and `.storybook/preview.css` (governs the
  preview) carry both globs. Verify by grepping a built app's CSS for a utility only one
  recipe uses.
- `@bc-solutions-coder/styles` is a **devDependency only** (for the Storybook preview);
  `src/` must never import it.

## Exports — barrel _and_ subpath

`dist/` mirrors `src/` 1:1 (Vite lib mode, `preserveModules`, `sideEffects: false`), so the
folder structure **is** the subpath structure: `.` → `dist/index.js`, `./*` →
`dist/components/*/index.js`, plus the `./source.css` passthrough.

- The **root barrel carries components and their prop types only.** A component's CVA recipe
  (`fooRecipe` + `FooRecipeProps`) stays reachable through `@bc-solutions-coder/ui/<name>`
  alone, so styling internals do not widen the headline surface. Mechanically: the barrel's
  block for a folder is exactly that folder's `index.ts` block from `<name>.tsx`, never the
  one from `<name>.styles.ts`.
- **Three files encode the catalog as one exact set and must move together in one commit**
  whenever a component is added: `COMPONENT_FOLDERS` in `src/core/package-scaffold.test.ts`,
  `src/index.ts`, and `PUBLIC_RUNTIME_EXPORTS` + the `PublicTypeExports` tuple in
  `src/index.test.ts`. Growing one alone turns another red.
- `src/core/dist-structure.test.ts` is catalog-derived (reads `src/components` off disk), so
  adding a component never edits it — but it asserts the **built** artifact, so a stale
  `dist/` from a smaller catalog fails it. `pnpm check` runs test before build; rebuild after
  adding a component.

## Storybook and the test model

`pnpm --filter @bc-solutions-coder/ui test` runs **three Vitest projects**: `node` (pure-logic
`*.test.ts`), `browser` (component `*.test.tsx` in headless Chromium), and `storybook`
(`@storybook/addon-vitest` running every story as a test case in that same Chromium, with the
real Tailwind pipeline and the fork's real theme attached).

- **Stories ARE the render/interaction coverage** — all variants and states, `play()` for
  interactive ones. `*.test.tsx` is reserved for edges a story cannot express (data-attribute
  state, className-override-wins, keyboard interaction). Do not duplicate story coverage into
  a test file.
- **No mocking.** ui's own tests mock nothing — real Base UI parts, real Chromium, real
  tokens. The only permitted double is a userland callback spy (`onClick={fn()}`). Apps must
  never mock `@bc-solutions-coder/ui` either (see `.claude/rules/TESTING.md`).
- Every `@base-ui/react/<part>` subpath a component imports **must** be appended to
  `baseUiSubpaths` in `vitest.config.ts` (alphabetically, as the component lands). It feeds
  `optimizeDeps.include` for both browser projects — Storybook runs its own Vite server with
  its own dep cache, so the list is repeated there, not shared. Without it Vite pre-bundles
  the subpath with its own React copy and specs die on `Cannot read properties of null
(reading 'useRef')`.
- **The `browser` project loads no Tailwind**, so a component whose box comes only from its
  recipe and has no text (switch, checkbox, radio, slider, toggle) measures 0×0 and
  Playwright's actionability check makes `userEvent.click` hang until timeout. Activate with
  the DOM's own `element.click()`, or `element.focus()` + `userEvent.keyboard(" ")`. Story
  play functions are unaffected (`storybook/test`'s userEvent is testing-library's, synthetic
  and visibility-blind), so pointer interaction belongs in stories.
- **Popup unmount is animation-frame-deferred** for the whole Base UI popup family. Assert
  closure with `await expect.poll(() => ...).toBeNull()`, never a bare synchronous `expect`.
- In the `browser` project the **mouse position persists across specs in a file**. Open
  hover-driven components with `element.focus()`; if a spec must hover, make it the last one
  in the file.

## Gotchas

- **`ReadyIndicator` stamps the E2E hydration marker** (`READY_ATTRIBUTE` → `data-app-ready`)
  that every Playwright suite waits on. Changing it breaks E2E readiness across both apps.
- This package ships a **prebuilt `dist/`**, so `import.meta.env.DEV` inside a component
  bakes in the _library's_ build env, not the consuming app's. Anything that must reflect the
  app's build (e.g. `DocumentStyles`' stylesheet href) is decided in the app shell and passed
  in as a prop.
- React, `react-dom`, and `@tanstack/react-router` are **peer** dependencies — keep them out
  of `dependencies`.
- `.oxlintrc.json` here turns off `react/jsx-props-no-spreading` (passthrough is the pattern).

## Scripts

```bash
pnpm --filter @bc-solutions-coder/ui build       # vite build (lib mode) && tsc -p tsconfig.build.json
pnpm --filter @bc-solutions-coder/ui test        # vitest run — node | browser | storybook
pnpm --filter @bc-solutions-coder/ui typecheck   # tsc --noEmit
pnpm --filter @bc-solutions-coder/ui storybook   # explorer on :6006
pnpm --filter @bc-solutions-coder/ui build-storybook
```

Consumer-facing docs (how an app imports components, how to add one) live in
`docs/development/component-library.md`.
