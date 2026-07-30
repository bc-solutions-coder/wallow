# Component Library

`@bc-solutions-coder/ui` (`packages/ui`) is the shared, browser-only React component library both
frontends build their screens from. It is a **wrapper layer, not a framework**: every visual part
is a headless [Base UI](https://base-ui.com/react/overview/quick-start) primitive
(`@base-ui/react`) wrapped in a [CVA](https://cva.style/) class recipe written entirely in the
semantic Tailwind tokens `@bc-solutions-coder/styles` emits from `api/branding.json`. Behaviour and
accessibility come from Base UI; appearance comes from the fork's own theme; the package supplies
the glue and the house style.

The package is private (never published to a registry) and consumed as a `workspace:*` dependency.
Rebranding a fork changes `api/branding.json` — no component source changes.

## The catalog

47 components, one folder per component under `packages/ui/src/components/`. The folder name is
also the import subpath.

| Group                | Components                                                                                                                                                                   |
| -------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Forms and input      | `Button`, `Input`, `Field`, `Fieldset`, `Form`, `Label`, `Checkbox`, `CheckboxGroup`, `Radio`, `RadioGroup`, `Select`, `Combobox`, `Autocomplete`, `Switch`, `Slider`, `NumberField`, `OTPField`, `Toggle`, `ToggleGroup` |
| Overlays and menus   | `Dialog`, `AlertDialog`, `Drawer`, `Popover`, `Tooltip`, `PreviewCard`, `Menu`, `ContextMenu`, `Menubar`, `Toast`                                                             |
| Layout and navigation | `Accordion`, `Collapsible`, `Tabs`, `NavigationMenu`, `Toolbar`, `ScrollArea`, `Separator`, `Card`, `CenteredCardLayout`                                                     |
| Display and feedback | `Avatar`, `Progress`, `Meter`, `ErrorBanner`, `MutedText`                                                                                                                     |
| App wiring           | `ReadyIndicator`, `FocusOnNavigate`, `DocumentStyles`, `ForkAttribution`                                                                                                      |

Browse them interactively with Storybook, which renders every component against the fork's real
theme tokens:

```bash
pnpm --filter @bc-solutions-coder/ui storybook   # http://localhost:6006
```

## Consuming components

### Root barrel vs. per-component subpath

The package ships both, from the same build — `dist/` mirrors `src/` one-to-one and the package is
marked `sideEffects: false`:

```ts
import { Button, Card, Dialog } from "@bc-solutions-coder/ui"; // root barrel — the default
import { buttonRecipe } from "@bc-solutions-coder/ui/button"; // per-component subpath
```

**Use the root barrel** for ordinary application code. Bundlers tree-shake it, so importing three
components from the barrel ships three components.

**Use the subpath** in two cases:

1. **You need a component's CVA recipe.** The barrel deliberately exports components and their prop
   types only; a recipe (`buttonRecipe` and its `ButtonRecipeProps`) is reachable through the
   subpath alone, so styling internals never widen the package's headline API.
2. **The module graph is not tree-shaken.** A dev server or a Vitest run links the whole barrel,
   including components you never render. If a spec stubs a dependency that some unrelated barrel
   member imports, the barrel fails to link — `apps/wallow-web/src/shared/components/DashboardNav.tsx`
   imports `@bc-solutions-coder/ui/navigation-menu` for exactly this reason (the barrel also pulls
   in `FocusOnNavigate`, which needs router context its specs do not provide).

### Single-part and multi-part components

Simple components are a single export taking the native element's props plus the recipe's variants:

```tsx
<Button variant="destructive" onClick={onDelete}>Delete project</Button>
```

Multi-part components export **one namespace object whose keys mirror Base UI's part names exactly**,
so the [Base UI documentation](https://base-ui.com/react/components/dialog) for a component is also
the documentation for Wallow's:

```tsx
<Dialog.Root>
  <Dialog.Trigger>Delete project</Dialog.Trigger>
  <Dialog.Portal>
    <Dialog.Backdrop />
    <Dialog.Popup>
      <Dialog.Title>Delete project</Dialog.Title>
      <Dialog.Description>This cannot be undone.</Dialog.Description>
      <Dialog.Close>Cancel</Dialog.Close>
    </Dialog.Popup>
  </Dialog.Portal>
</Dialog.Root>
```

### Overriding styles

Every part merges its recipe with your `className` through `tailwind-merge`, so **the value you pass
last wins** and utilities you do not mention survive:

```tsx
<Button className="w-auto rounded-full" /> // keeps the recipe's colours and typography
```

Reach for a token utility (`bg-muted`, `text-destructive`) rather than a raw colour. If the token you
want does not exist, add it in `packages/styles` first — see
[Adding a New Design Token](frontend-setup.md#adding-a-new-design-token).

### One CSS import is required

Tailwind v4 does not scan `node_modules`, so an app that renders these components must import the
package's `@source` declaration from its CSS entry, or every component renders unstyled:

```css
@import "@bc-solutions-coder/styles/styles.css";
@import "@bc-solutions-coder/ui/source.css";
@source "./";
```

See [Styling and Tailwind Setup](frontend-setup.md#styling-and-tailwind-setup) for the full
rationale.

### Form controls come through `@bc-solutions-coder/forms`

An app rarely renders `Field`, `Input`, `Select` or `Checkbox` directly. `@bc-solutions-coder/forms`
sits one layer above this package and ships those controls pre-bound to TanStack Form state, zod
validation and derived testids — reach for it first, and drop to the raw parts only for a control the
catalog has no field for. See [Forms](forms.md). The dependency runs one way: `forms` imports `ui`,
never the reverse.

### Do not mock it

App specs must never replace `@bc-solutions-coder/ui` with stubs. The components run in the same
real headless Chromium the app's own specs do, and a stub is how a passing spec starts hiding a
broken screen. See `.claude/rules/TESTING.md`.

## Adding a component

Every component folder has the same five files. Copy the nearest existing component — a simple one
like `button`, a multi-part one like `dialog` — rather than starting from scratch.

```
packages/ui/src/components/<name>/
├── <name>.tsx          # the parts: Base UI part + cn(recipe(), className) + full prop passthrough
├── <name>.styles.ts    # CVA recipes ONLY — no JSX, no React import
├── <name>.stories.tsx  # Storybook stories: this component's render/interaction coverage
├── <name>.test.tsx     # behavioural edges a story cannot express
└── index.ts            # folder barrel: the .tsx exports, then the .styles.ts exports
```

The steps:

1. **Check the real anatomy.** Read the installed package's `<component>/index.parts.d.ts` for the
   authoritative part list; do not guess it from a design document.
2. **Write the recipe** in `<name>.styles.ts` using only semantic token utilities
   (`bg-primary`, `text-muted-foreground`, `border-border`, …). Style state off Base UI's `data-*`
   attributes (`data-[disabled]`, `data-[open]`), not CSS pseudo-classes, so the recipe still applies
   when a caller composes the part onto another element with `render`.
3. **Wrap each part** in `<name>.tsx`, importing the Base UI part from its own subpath
   (`@base-ui/react/<name>`). Multi-part components export one namespace object mirroring Base UI's
   part names; parts that need no styling pass through unwrapped.
4. **Register the Base UI subpath** in `baseUiSubpaths` in `packages/ui/vitest.config.ts`. This is
   required, not an optimisation — without it the test run pre-bundles a second copy of React and
   the specs fail.
5. **Write the stories**, covering every variant and state, with a `play()` function for interactive
   components. These are the component's test coverage.
6. **Add `<name>.test.tsx`** only for what a story cannot assert.
7. **Export it**: the folder's `index.ts`, then the three catalog files that must move together in
   the same commit — `COMPONENT_FOLDERS` in `src/core/package-scaffold.test.ts`, the root barrel
   `src/index.ts`, and `PUBLIC_RUNTIME_EXPORTS` + `PublicTypeExports` in `src/index.test.ts`.
   Growing one alone turns another red.
8. **Build, then test**: `pnpm --filter @bc-solutions-coder/ui build && pnpm --filter
   @bc-solutions-coder/ui test`. The build matters — one guard asserts the shipped `dist/` layout, so
   a stale build from a smaller catalog fails it.

The subpath export needs no manifest edit: `package.json` maps `./*` to `dist/components/*/index.js`,
so the folder you created is already importable as `@bc-solutions-coder/ui/<name>`.

`packages/ui/CLAUDE.md` holds the full contributor detail — package layering, the recipe/JSX split,
and the Vitest browser-mode pitfalls specific to headless components.

## How it is tested

`pnpm --filter @bc-solutions-coder/ui test` runs three Vitest projects:

| Project     | Runs                                    | Environment                                                          |
| ----------- | --------------------------------------- | -------------------------------------------------------------------- |
| `node`      | pure-logic `*.test.ts`                  | Node                                                                 |
| `browser`   | component `*.test.tsx`                  | headless Chromium, no stylesheet loaded                              |
| `storybook` | every `*.stories.tsx`, via `@storybook/addon-vitest` | headless Chromium with the real Tailwind build and fork theme |

Stories carry the render and interaction coverage; `*.test.tsx` covers the edges a story cannot
express. Because the `storybook` project compiles real CSS, it is also the only place a spec can
assert that a recipe utility actually paints. Nothing in the package is mocked — real Base UI parts,
a real browser, real design tokens. See `.claude/rules/TESTING.md`.
