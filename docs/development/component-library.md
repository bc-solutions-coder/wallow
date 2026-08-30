# Component Library

`@bc-solutions-coder/ui` (`packages/ui`) is the shared, browser-only React component library both
frontends build their screens from. It is a **wrapper layer, not a framework**: every visual part
is a headless [Base UI](https://base-ui.com/react/overview/quick-start) primitive
(`@base-ui/react`) wrapped in a [CVA](https://cva.style/) class recipe written entirely in the
semantic Tailwind tokens `@bc-solutions-coder/styles` emits from `packages/styles/branding.json`. Behaviour and
accessibility come from Base UI; appearance comes from the fork's own theme; the package supplies
the glue and the house style.

The package is private (never published to a registry) and consumed as a `workspace:*` dependency.
Rebranding a fork changes `packages/styles/branding.json` — no component source changes.

## The catalog

60 components, one folder per component under `packages/ui/src/components/`. The folder name is
also the import subpath.

| Group                | Components                                                                                                                                                                   |
| -------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Forms and input      | `Button`, `Input`, `Textarea`, `Field`, `Fieldset`, `Form`, `Label`, `Checkbox`, `CheckboxGroup`, `Radio`, `RadioGroup`, `Select`, `Combobox`, `Autocomplete`, `Switch`, `Slider`, `NumberField`, `OTPField`, `Toggle`, `ToggleGroup`, `SimpleSelect` |
| Overlays and menus   | `Dialog`, `AlertDialog`, `Drawer`, `Popover`, `Tooltip`, `PreviewCard`, `Menu`, `ContextMenu`, `Menubar`, `Toast`                                                             |
| Layout and navigation | `Accordion`, `Collapsible`, `Tabs`, `NavigationMenu`, `Toolbar`, `ScrollArea`, `Separator`, `Card` (with `CardHeader`), `CenteredCardLayout`, `PageContainer`, `PageHeader`, `EmptyState`, `ListCard`, `ListRow`, `QuietLink`  |
| Display and feedback | `Text`, `MutedText`, `Badge`, `Avatar`, `Progress`, `Meter`, `ErrorBanner`, `NoticeBanner`                                                                                    |
| Theming              | `ThemeProvider` (with `ThemeScript` and `useTheme`), `ThemeToggle`                                                                                                            |
| App wiring           | `ReadyIndicator`, `FocusOnNavigate`, `DocumentStyles`, `ForkAttribution`                                                                                                      |

`Text` is the typography primitive the rest of the catalog composes onto: it owns the type scale
(`display`, `title`, `heading`, `subheading`, `body`, `bodySm`, `caption`, `overline`, `code`) and
the semantic colour set (`default`, `muted`, `primary`, `accent`, `destructive`, `success`,
`onSidebar`, `onCard`, `onPrimary`), with `weight` and `align` as independent axes.
`MutedText` is now literally `<Text as="p" variant="bodySm" color="muted" />` — keep using it for
secondary copy, but reach for `Text` whenever you need a scale step or colour it does not name.

Four entries wrap no Base UI part; each names a stack the apps had been rebuilding by hand:

- **`CardHeader`** (`Card`'s folder, not one of its own) — a card's title-and-description pair.
  It owns the `<h2>`, so a screen composing it gets the card-heading step by construction instead
  of spelling out `<Text as="h2" variant="subheading" color="onCard">` and relying on lint to catch
  a mistake. `titleTestId` targets the heading element; `data-testid` lands on the wrapper.
- **`QuietLink`** — the muted secondary link: card footers, back-links, "Forgot password?". A plain
  `<a>`, because these navigate with real hrefs. Distinct from `Button variant="link"`, which is the
  primary-coloured stand-in for an **action**; `QuietLink` recedes.
- **`NoticeBanner`** — the non-destructive banner, `tone="success" | "warning"`. A sibling of
  `ErrorBanner` rather than a tone on it: `ErrorBanner` wraps its children in a styled `<p>`, right
  for a sentence of failure text and wrong for a notice whose body may be a heading plus a link. So
  `NoticeBanner` wraps nothing and you compose `Text` inside it.
- **`PageContainer`** — `PageHeader`'s sibling: the column a page body sits in. It adds width and
  centring and nothing else, so a page writes no `max-w-*` of its own; the rail, main column and
  padding around it belong to the app's layout route.

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
   member imports, the barrel fails to link — `packages/navigation/src/app-nav.tsx`
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

### Composing `Button` onto a link

A navigation styled as a button is composed through Base UI's `render` prop, either onto an
intrinsic anchor or onto a router `Link`:

```tsx
<Button render={<a href="/terms" />}>Terms</Button>
<Button render={<Link to="/dashboard/organizations" />}>Browse organizations</Button>
```

**Do not pass a `role` yourself.** Base UI's `useButton` merges `role="button"` onto every
non-native element it composes onto, which announced these navigations as actions and dropped them
out of a screen reader's links list while the `href` still worked — a WCAG 2.2 SC 4.1.2
Name/Role/Value mismatch. `Button` now measures the element it actually mounted and supplies
`role="link"` as a **default** whenever that element is an anchor carrying a destination. It covers
both shapes above, because a component-typed `render` only reveals its tag once mounted, and it
re-measures every render, so a control whose `href` disappears while a request is in flight stops
being a link for exactly that long.

The role is a default, not an override — it is spread before your props — so a caller who genuinely
needs `role="menuitem"` still wins. A hand-written `role="link"` is now redundant, and
`role={undefined}` deletes the `role="button"` a composed `<div>` depends on. Assert the outcome
with `getByRole("link", { name })` rather than restating the role in the markup.

### `surface` — which palette a component paints from

`variant` says what **kind** of control this is; every one of its arms paints from the page palette.
`surface` says **which palette** it paints from, which is a different question the moment a control
is dropped onto the fork's inverted rail — a `secondary` button there is a light chip on a dark
surface. The axis has two arms, `page` (the default) and `sidebar`:

```tsx
<NavigationMenu.Link surface="sidebar" render={<Link to="/dashboard" />}>Overview</NavigationMenu.Link>
<ThemeToggle surface="sidebar" />
<ErrorBanner surface="sidebar">{message}</ErrorBanner>
```

It is carried today by `buttonRecipe` (and so by `ThemeToggle`, which composes `Button`),
`errorBannerRecipe`, and `navigationMenuLinkRecipe`. `navigationMenuTriggerRecipe` does **not** have
it yet — no app renders a trigger, so it is a known gap rather than a defect.
`packages/navigation/src/app-nav.tsx` is the reference example, passing it at both rail call
sites; the `ErrorBanner` in `apps/wallow-web/src/shared/components/SignOut.tsx` is the third
`surface="sidebar"` in the shell.

Reach for `surface` instead of hand-writing an inversion (`bg-foreground text-background`) in a
`className`: the recipe restates every colour dimension a `variant` arm can set, because
tailwind-merge only drops the classes you actually conflict with and any dimension left unnamed
stays a page colour on the rail.

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

## Theming and dark mode

`packages/styles` emits a `:root`, a `.dark` and a `.light` block from `packages/styles/branding.json`, and the
catalog's three theming exports are what make them reachable. An app wires `ThemeScript` and
`ThemeProvider` once in its root document — see
[Dark Mode](frontend-setup.md#dark-mode) for that wiring — after which any screen can read or change
the theme:

```tsx
import { ThemeToggle, useTheme } from "@bc-solutions-coder/ui";

<ThemeToggle />; // cycles light -> dark -> system

const { mode, preference, setPreference } = useTheme();
```

`preference` is what the visitor asked for (`"light"`, `"dark"` or `"system"`); `mode` is the
scheme currently painted. They are different values on purpose: `"system"` is the default and the
state a control must be able to return to, which is why `ThemeToggle` cycles through three states
rather than toggling two and carries no `aria-pressed`. Its current state is exposed to tests as
`data-theme-preference`. Passing both `preference` and `onPreferenceChange` makes it fully
controlled, which is how a story renders one face of the control without a real `ThemeProvider` or
a `localStorage` round-trip.

> **The mode class must be on `document.documentElement`.** Wrapping a subtree in
> `<div className="dark">` compiles, renders, and paints the **light** palette — see
> [Scoping dark mode](frontend-setup.md#scoping-dark-mode) for why. Anything that needs to render or
> assert against a scheme has to stamp the class on the document element itself and clean up after
> itself, since every story and every spec in a file shares one document. The catalog's reference
> implementation is `packages/ui/.storybook/scheme-decorators.tsx` — a `lightScheme`/`darkScheme`
> decorator pair that stamps the class in a layout effect and removes it on unmount — paired with
> `expectScheme` from `.storybook/scheme-assertions.ts`, which measures that a scheme-scoped story
> paints the palette it claims. Copy that pair rather than inventing a wrapper.

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
7. **Export it**: the folder's `index.ts`, then the two catalog files that must move together in
   the same commit — the root barrel `src/index.ts`, and `PUBLIC_RUNTIME_EXPORTS` +
   `PublicTypeExports` in `src/index.test.ts`. Growing one alone turns the other red.
8. **Test it**: `pnpm --filter @bc-solutions-coder/ui test`. No build first — in-repo the
   `exports` map resolves to `src/`, so app suites see the change immediately and there is no
   stale bundle to test against.

The subpath export needs no manifest edit: `package.json` maps `./*` to `src/components/*/index.ts`
in-repo (and to `dist/components/*/index.js` at publish time, via `publishConfig`), so the folder
you created is already importable as `@bc-solutions-coder/ui/<name>`.

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
