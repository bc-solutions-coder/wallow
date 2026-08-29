# packages/ui — @bc-solutions-coder/ui Agent Guide

The shared **browser-only** React component library: one folder per component under
`src/components/`, each a **Base UI** (`@base-ui/react`) headless part wrapped in a **CVA**
recipe built from `@bc-solutions-coder/styles` semantic tokens. Private (never published),
consumed as `workspace:*` by all three apps and by `packages/forms` and
`packages/navigation` — a change here reaches library code as well as screens.

The app-wiring folders (`ReadyIndicator`, `FocusOnNavigate`, `DocumentStyles`,
`ForkAttribution`, `theme-provider`) render nothing a story could show and are the only ones
without stories; `ThemeToggle` is an ordinary visual component and has them.

## Layering — three layers, one direction

| Layer | Path                     | Rule                                                                                                    |
| ----- | ------------------------ | ------------------------------------------------------------------------------------------------------- |
| 0     | `src/core/`              | `cn.ts` (tailwind-merge wrapper) and the specs beside it. Imports **nothing internal**.                 |
| 1     | `src/components/<name>/` | One folder per component. May import `core/`; may import a sibling component only for deliberate reuse. |
| 2     | `src/index.ts`           | The root barrel — the only file that imports every component folder.                                    |

`core` never imports `components`. Cross-component imports are for **deliberate reuse**, not
convenience: `Label` re-exports Field's label part and recipe, and
`AlertDialog`/`ContextMenu`/`Autocomplete` reuse the already-wrapped parts of
`Dialog`/`Menu`/`Combobox` (Base UI re-exports those runtimes verbatim — re-wrapping would fork
behaviour). `SimpleSelect` is the whole labelled select as one component; `Select` stays the
composable API for groups, separators, or a non-`Field` trigger. A folder that composes rather
than wraps declares no recipe and has no `.styles.ts`.

## Composition entries (wrap no Base UI part)

- **`CardHeader`** (`card/card-header.tsx`, exported from the `card` folder) — the card's
  title-and-description pair. **It owns the `<h2>`**, so prefer it over spelling
  `<Text as="h2" …>` at a call site. `title` is `Omit`ted from the `HTMLAttributes`
  passthrough (the spread would stamp the heading text on the wrapper as a tooltip);
  `titleTestId` is separate from the wrapper's `data-testid` because `{screen}-heading` ids
  must name the heading ELEMENT.
- **`QuietLink`** — the muted secondary link (card footers, "Forgot password?", back-links). A
  plain `<a>`, since wallow-auth navigates across origins with real hrefs. Distinct from
  `Button variant="link"`, the primary-coloured underlined stand-in for an ACTION: the Button
  when the destination is the thing the screen wants next, `QuietLink` when it is an aside.
- **`NoticeBanner`** — the non-destructive banner. A **sibling of `ErrorBanner`, not a `tone`
  axis on it**: an error banner wraps its children in a styled `<p>`, while a notice body
  ranges from a sentence to a heading plus a link — so `NoticeBanner` wraps nothing and the
  caller composes `Text` inside it. Its `tone` (`success` | `warning`) is destructured, never
  spread, or it lands on the DOM node.

## Component folder anatomy

| File                 | Holds                                                                                         |
| -------------------- | --------------------------------------------------------------------------------------------- |
| `<name>.tsx`         | The parts. Each wraps a real Base UI part: `cn(recipe({...}), className)` + full passthrough. |
| `<name>.styles.ts`   | **CVA recipes only** — no JSX, no React import.                                               |
| `<name>.stories.tsx` | Storybook stories = this component's render/interaction coverage.                             |
| `<name>.test.tsx`    | Behavioural edges a story cannot express.                                                     |
| `index.ts`           | The folder barrel: the `<name>.tsx` export block, then the `<name>.styles.ts` one.            |

- **Multi-part components export ONE namespace object** whose keys mirror Base UI's part names
  1:1 (`export const Dialog = { Root, Trigger, Portal, ... }`). Unstyled parts pass through
  unwrapped. Verify the real anatomy against the installed package's
  `<component>/index.parts.d.ts` (under `node_modules/.pnpm/@base-ui+react@…` — Base UI is not
  symlinked at the repo root), never from memory or a design doc.
- **`className` is narrowed back to `string`** on every part: Base UI widens it to a state
  callback, which `cn()` cannot merge. Catalog-wide, a caller's `className` always means
  "utilities merged over the recipe, last one wins".
- Import each part from its own subpath (`@base-ui/react/dialog`), never the package root.

## Recipes and design tokens

- Recipes reference **only semantic token utilities from `@bc-solutions-coder/styles`**
  (`bg-primary`, `text-muted-foreground`, …). Never a raw colour — a missing token is added to
  `packages/styles/styles.css`'s `@theme` (plus `packages/styles/branding.json`) **first**.
- Style state off Base UI's `data-*` attributes (`data-[disabled]`, `data-[open]`), not
  `:disabled`/`:open` pseudo-classes, so the recipe survives a caller composing the part onto
  another element via `render`.
- **Tailwind `@source` must scan `*.tsx` AND `*.styles.ts`.** Most class names live in recipe
  files; a `.tsx`-only scan emits no CSS for them and the component renders correct class
  attributes with nothing behind them — right in the DOM and in `classList` specs, unstyled in
  the app. Both `source.css` (consuming apps) and `.storybook/preview.css` (the preview) carry
  both globs. Verify by grepping a built app's CSS for a utility only one recipe uses.
- `@bc-solutions-coder/styles` is a **devDependency only** (for the Storybook preview);
  `src/` must never import it.
- **`surface: page | sidebar` is the axis for WHICH PALETTE a control paints from**, separate
  from `variant` (what KIND of control; every `variant` arm paints from the page palette). Two
  rules when adding it to a recipe: declare the axis **LAST** so its utilities land after
  `variant`'s and tailwind-merge collapses the pair in its favour — that ordering IS the
  mechanism — and have the `sidebar` arm restate **every** colour dimension a `variant` arm can
  set (rest surface, rest text, border, hover surface, hover text), since a dimension left
  unnamed stays a page colour on the rail.

## Exports — barrel _and_ subpath

`dist/` mirrors `src/` 1:1 (Vite lib mode, `preserveModules`, `sideEffects: false`), so the
folder structure IS the subpath structure: `.` → `dist/index.js`, `./*` →
`dist/components/*/index.js`, plus the `./source.css` passthrough.

- **The root barrel carries components and their prop types only.** A recipe (`fooRecipe` +
  `FooRecipeProps`) stays reachable through `@bc-solutions-coder/ui/<name>` alone — the
  barrel's block for a folder is that folder's `index.ts` block from `<name>.tsx`, never the
  one from `<name>.styles.ts`.
- **Two files encode the catalog as one exact set and must move together in one commit** when
  a component is added: `src/index.ts`, and `PUBLIC_RUNTIME_EXPORTS` + the
  `PublicTypeExports` tuple in `src/index.test.ts`. Growing one alone turns the other red.
  There is deliberately no `dist/` walk: the package is `private: true` so
  `scripts/check-exports.sh` skips it, and a subpath that fails to build or resolve takes both
  apps' suites down on the next run.

## Storybook and the test model

`pnpm --filter @bc-solutions-coder/ui test` runs **three Vitest projects**: `node`
(pure-logic `*.test.ts`), `browser` (component `*.test.tsx` in headless Chromium), and
`storybook` (`@storybook/addon-vitest` running every story as a test case in that same
Chromium, with the real Tailwind pipeline and fork theme attached).

- **Stories ARE the render/interaction coverage** — all variants and states, `play()` for
  interactive ones. `*.test.tsx` covers only edges a story cannot express (data-attribute
  state, className-override-wins, keyboard interaction). Do not duplicate story coverage into
  a test file.
- **No mocking.** Real Base UI parts, real Chromium, real tokens; the only permitted double is
  a userland callback spy (`onClick={fn()}`). Apps must never mock `@bc-solutions-coder/ui`
  either (`.claude/rules/TESTING.md`).
- **Base UI pre-bundling needs no per-component step.** `vitest.config.ts` feeds
  `optimizeDeps.include` the single glob `@base-ui/react/*`, expanded against Base UI's own
  `exports` keys. The list is repeated for the `storybook` project (its own Vite server, its
  own dep cache), and the mechanism is not optional: without it Vite pre-bundles a subpath
  with its own React copy and specs die on
  `Cannot read properties of null (reading 'useRef')`. `src/core/browser-deps.test.ts` checks
  every entry in both lists resolves — Vite treats an unresolvable one as a warning and
  carries on pre-bundling nothing.
- **The `browser` project loads no Tailwind**, so a component whose box comes only from its
  recipe and has no text (switch, checkbox, radio, slider, toggle) measures 0×0 and
  Playwright's actionability check makes `userEvent.click` hang to timeout. Activate with the
  DOM's own `element.click()`, or `element.focus()` + `userEvent.keyboard(" ")`. Story play
  functions are unaffected (`storybook/test`'s userEvent is testing-library's, synthetic and
  visibility-blind), so pointer interaction belongs in stories.
- **The navigation-escape guard is wired TWICE**, because the two browser projects take it
  through different doors: `vitest.setup.ts` (via `browserSetupFiles`) covers the `browser`
  project; `.storybook/preview.tsx`'s named `beforeEach`/`afterEach` exports cover the
  `storybook` one, which `storybookTest()` assembles itself and which never reads
  `browserSetupFiles`. Both install `@bc-solutions-coder/testing/navigation-escape`.
- **Popup unmount is animation-frame-deferred** for the whole Base UI popup family. Assert
  closure with `await expect.poll(() => ...).toBeNull()`, never a bare synchronous `expect`.
- In the `browser` project the **mouse position persists across specs and across files**. Open
  hover-driven components with `element.focus()`, and name the pointer state at the assertion:
  `await userEvent.unhover(el)` before a rest-state read, `await userEvent.hover(el)` before
  the hover half — never a `beforeEach` that parks the pointer somewhere harmless. A spec
  whose hover changes something the file cannot re-enter (a toast viewport's hover pauses
  auto-dismiss timers) goes last and ends by unhovering.

## Gotchas

- **A `.dark` WRAPPER DIV IS VACUOUS — the class only works on `document.documentElement`.**
  `renderThemeStyle` emits `:root`/`.dark`/`.light` blocks carrying the RAW variables
  (`--sidebar`, `--background`, …), while `styles.css`'s `@theme` declares the TOKEN
  (`--color-sidebar: var(--sidebar, …)`) on `:root` alone. A `var()` inside a custom property
  is substituted at computed-value time on the DECLARING element, so a descendant `.dark`
  rebinds the raw variable while the token above it keeps the light value it already computed.
  A story or spec that must show or assert a scheme stamps `documentElement` and cleans up
  (one shared document — design around leakage). **The reference implementation is
  `.storybook/scheme-decorators.tsx`** (`lightScheme`/`darkScheme`, class added in a layout
  effect, removed on unmount); scheme-scoped stories pair it with `expectScheme` from
  `.storybook/scheme-assertions.ts`, which measures that the palette is the right way up.
  Nothing here may assert a scheme from a class string — the markup is byte-identical either
  way.
- **`Button` supplies `role="link"` itself for composed anchors — never pass a `role`.** Base
  UI's `useButton` merges `role="button"` onto every non-native element it composes onto, so a
  `render`-composed anchor would announce a navigation as an action (WCAG 2.2 SC 4.1.2). The
  component measures the MOUNTED element (a `render={<Link/>}` descriptor resolves to an
  anchor only once rendered) and re-measures every render. The role is spread BEFORE `rest` so
  it stays a caller-overridable default; `role={undefined}` would delete the `role="button"` a
  composed `<div>` depends on. Assert with `getByRole("link")`.
- **`ReadyIndicator` stamps the E2E hydration marker** (`READY_ATTRIBUTE` → `data-app-ready`)
  every Playwright suite waits on. The catalog component
  (`src/components/ready-indicator/ready-indicator.tsx`) is the only place the attribute is
  written; each app mounts it through a thin wrapper of its own. Changing the catalog
  component breaks E2E readiness in all three apps.
- This package **publishes** a prebuilt `dist/`, so for an installed consumer
  `import.meta.env.DEV` inside a component bakes in the _library's_ build env, not the app's
  (in-repo it reads the app's, because `exports` resolves to `src/` — worse, since the value
  then differs between this workspace and a fork). Anything that must reflect the app's build
  (e.g. `DocumentStyles`' stylesheet href) is decided in the app shell and passed as a prop.
- React, `react-dom`, and `@tanstack/react-router` are **peer** dependencies — keep them out
  of `dependencies`.
- **`.oxlintrc.json` here `extends` the root config** — registration/inheritance mechanics,
  the directory-relative glob trap, and this package's one scoped exemption
  (`drawer.styles.ts`) are documented in `packages/lint/CLAUDE.md`; the reasons for each
  test/story-override relaxation are `//` comments in the config itself. Never restate
  `categories` or `plugins` here, and remember the test pass relies on nested-config lookup
  (no `-c`).

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
