# packages/ui — @bc-solutions-coder/ui Agent Guide

The shared **browser-only** React component library: 58 component folders under
`src/components/`, each a **Base UI** (`@base-ui/react` ^1.6.0) headless part wrapped in a
**CVA** recipe built from `@bc-solutions-coder/styles` semantic tokens. Private (never
published), consumed by `apps/wallow-auth` and `apps/wallow-web` as `workspace:*`.

Five folders render nothing a story could show and are the only ones without one: the four
app-wiring folders — `ReadyIndicator`, `FocusOnNavigate`, `DocumentStyles`,
`ForkAttribution` — plus `theme-provider`, which is a context provider and a pre-paint
`<script>`. Its `ThemeToggle` sibling is an ordinary visual component and does have stories.

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
would fork the behaviour). `SimpleSelect` is the same idea one level up: the whole labelled
select — `Field` + `Label` + `Select`'s seven-part portal tree — as one component, because every
app needs the identical tree and spelling it out per call site blows `react/jsx-max-depth`.
`Select` stays the composable API for a call site that needs groups, separators or a trigger
that is not a `Field`; a folder that composes rather than wraps declares no recipe of its own,
which is why it has no `.styles.ts`.

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
  `packages/styles/styles.css`'s `@theme` (plus `packages/styles/branding.json`) **first**.
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
- **`surface: page | sidebar` is the axis for WHICH PALETTE a control paints from**, kept
  separate from `variant` (which says what KIND of control it is, and whose every arm paints
  from the page palette — a `secondary` button dropped on the inverted rail is a light chip on
  a dark surface). It is on `buttonRecipe`, `errorBannerRecipe` and
  `navigationMenuLinkRecipe`; `navigationMenuTriggerRecipe` still lacks it (no app renders a
  trigger, so it is a gap, not a defect). Two rules when adding it to a recipe: declare the
  axis **LAST** so its utilities land after `variant`'s and `cn()`'s tailwind-merge collapses
  the pair in its favour — that ordering IS the mechanism, do not reshuffle it — and have the
  `sidebar` arm restate **every** colour dimension a `variant` arm can set (rest surface, rest
  text, border, hover surface, hover text), since tailwind-merge only drops the class a caller
  conflicts with and a dimension left unnamed stays a page colour on the rail.

## Exports — barrel _and_ subpath

`dist/` mirrors `src/` 1:1 (Vite lib mode, `preserveModules`, `sideEffects: false`), so the
folder structure **is** the subpath structure: `.` → `dist/index.js`, `./*` →
`dist/components/*/index.js`, plus the `./source.css` passthrough.

- The **root barrel carries components and their prop types only.** A component's CVA recipe
  (`fooRecipe` + `FooRecipeProps`) stays reachable through `@bc-solutions-coder/ui/<name>`
  alone, so styling internals do not widen the headline surface. Mechanically: the barrel's
  block for a folder is exactly that folder's `index.ts` block from `<name>.tsx`, never the
  one from `<name>.styles.ts`.
- **Two files encode the catalog as one exact set and must move together in one commit**
  whenever a component is added: `src/index.ts`, and `PUBLIC_RUNTIME_EXPORTS` + the
  `PublicTypeExports` tuple in `src/index.test.ts`. Growing one alone turns the other red.
  `src/core/package-scaffold.test.ts`'s `COMPONENT_FOLDERS` used to be a third; it and
  `src/core/dist-structure.test.ts` are gone with the rest of the source-reading guards
  (`Wallow-xg9t.1`). Nothing replaced their `dist/` walk, and that is deliberate rather than an
  oversight: this package is `private: true` so `scripts/check-exports.sh` skips it, and a
  subpath that fails to build or fails to resolve takes both apps' suites down on the next run.
  `src/index.test.ts` is where the catalog is still pinned exactly.

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
- **Base UI pre-bundling needs no per-component step.** `vitest.config.ts` feeds
  `optimizeDeps.include` the single glob `@base-ui/react/*`, which Vite expands against Base
  UI's own `exports` keys, so a new component's subpath is covered the moment the package
  publishes it. (This used to be a hand-maintained list of 39 that every component task had to
  append to.) The list is still repeated for the `storybook` project — Storybook runs its own
  Vite server with its own dep cache — and the mechanism itself is not optional: without it
  Vite pre-bundles a subpath with its own React copy and specs die on `Cannot read properties
of null (reading 'useRef')`. `src/core/browser-deps.test.ts` checks that every entry in both
  projects' lists actually resolves, because Vite treats one that does not as a warning and
  carries on pre-bundling nothing.
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

- **A `.dark` WRAPPER DIV IS VACUOUS — the class only works on `document.documentElement`.**
  `renderThemeStyle` emits `:root`/`.dark`/`.light` blocks carrying the RAW variables
  (`--sidebar`, `--background`, …), while `styles.css`'s `@theme` declares the TOKEN
  (`--color-sidebar: var(--sidebar, …)`) on `:root` alone. A `var()` inside a custom property
  is substituted at computed-value time on the DECLARING element, so a descendant `.dark`
  rebinds the raw variable while the token above it keeps the light value it already computed
  — and that value is what inherits down. Measured: `bg-sidebar` is `rgb(40,21,12)` under a
  `.dark` wrapper AND under a `.light` one, `rgb(35,17,8)` only with `.dark` on the document
  element. A story or spec that must show or assert a scheme stamps `documentElement` and
  cleans up (one shared document — design around leakage). **The reference implementation is
  `.storybook/scheme-decorators.tsx`**: a `lightScheme`/`darkScheme` decorator pair that adds
  the class in a layout effect and removes it on unmount. The six story files that once
  wrapped instead (`empty-state`, `list-card`, `list-row`, `page-header`, `text`,
  `theme-toggle`) now import that pair and are the pattern to copy — every scheme-scoped story
  among them carries `expectScheme` from `.storybook/scheme-assertions.ts`, which paints
  `--color-background` / `--background` onto a probe and measures that the palette is the right
  way up for the scheme claimed. That measurement is what makes the fix permanent: a wrapper
  coming back, or a decorator that stops cleaning up, turns a story red. Nothing here may
  assert a scheme from a class string — the markup is byte-identical either way.
- **`Button` supplies `role="link"` itself for composed anchors — never pass a `role`.**
  Base UI's `useButton` merges `role="button"` onto every non-native element it composes onto,
  so a `render`-composed anchor announced a navigation as an action (WCAG 2.2 SC 4.1.2). The
  component measures the MOUNTED element, not the `render` descriptor — `render={<Link/>}` is
  shipped and a component's type resolves to an anchor only once rendered — and re-measures
  every render, because a destination can appear and vanish between them. The role is spread
  BEFORE `rest` so it stays a default a caller can still override; `role={undefined}` would
  delete the `role="button"` a composed `<div>` depends on. Assert with `getByRole("link")`.
- **`ReadyIndicator` stamps the E2E hydration marker** (`READY_ATTRIBUTE` → `data-app-ready`)
  that every Playwright suite waits on. Changing it breaks E2E readiness across both apps.
- This package ships a **prebuilt `dist/`**, so `import.meta.env.DEV` inside a component
  bakes in the _library's_ build env, not the consuming app's. Anything that must reflect the
  app's build (e.g. `DocumentStyles`' stylesheet href) is decided in the app shell and passed
  in as a prop.
- React, `react-dom`, and `@tanstack/react-router` are **peer** dependencies — keep them out
  of `dependencies`.
- **`.oxlintrc.json` here registers the `wallow/*` plugin and enables all six rules.** The
  catalog defines the primitives three of them police, which is why they are on here rather than
  left to the consumers: a recipe is the one place a colour decision is written down. Two things
  follow. `no-tinted-text` is why `link`'s hover is the underline alone — the `hover:text-primary/80`
  it used to carry is a colour `branding.json` cannot reach. And `drawer.styles.ts` is the ONE file
  with a scoped `no-sidebar-inversion` exemption, because `drawerIndentBackgroundRecipe` FADES a
  bare `bg-foreground` in (`opacity-0` → `data-[active]:opacity-100`) and a baked-in alpha is not
  something a transition can animate from. The exemption names that one file, not the folder.
- **`.oxlintrc.json` here `extends` the root config, and its override globs must stay
  directory-relative.** oxlint reads the NEAREST config for a file and does not merge upward
  on its own, so dropping `extends` silently replaces the root's plugins, categories and
  every `no-restricted-imports` ban for this whole subtree. The non-obvious half: an override
  glob in this file is matched against the path relative to `packages/ui/`, so a repo-rooted
  prefix copied from the root config (`packages/ui/**/*.tsx`) matches nothing and fails
  silently. Never restate `categories` or `plugins` here either — that detaches the severity
  baseline even with `extends`. `packages/sdk/src/oxlint-guardrails.test.ts` asserts all three.
- Beyond `react/jsx-props-no-spreading` (passthrough is the pattern), the test/story override
  turns off five rules whose advice is wrong for component specs:
  `unicorn/prefer-number-coercion` (these specs read `getComputedStyle` values like `"8px"`,
  where `Number()` is `NaN` and `parseFloat` is the only correct reader),
  `unicorn/prefer-query-selector` (React `useId` values contain `:` and are not valid CSS
  selectors unescaped), `unicorn/error-message` (`new Error("")` is the SUBJECT of the
  messageless-error specs), `func-name-matching` (Storybook's canonical
  `render: function ControlledX()` must stay a named component for hooks and DevTools), and
  `unicorn/prefer-dom-node-dataset` (`getAttribute("data-*")` is the documented way to assert
  component state here). `react/jsx-max-depth` is off for stories/specs only; production
  source still honours it. Those five reach a spec only because the repo's test lint pass
  (`pnpm lint:tests`, `scripts/lint-tests.sh`) passes **no** `-c` — an explicit config flag
  disables oxlint's nested-config lookup, this file would stop being read, and all five would
  come back as errors.

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
