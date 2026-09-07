# @bc-solutions-coder/ui

Shared browser components for Wallow applications. The package combines Base UI behavior with semantic Tailwind recipes and includes theme, failure-message, and router integration helpers. It is a private workspace package with React, React DOM, and TanStack Router peer dependencies.

Import components and their prop types from the root. Each component folder also has a subpath export that includes its styling recipes and recipe prop types:

```tsx
import { Button, Card, CardHeader } from "@bc-solutions-coder/ui";
import { buttonRecipe } from "@bc-solutions-coder/ui/button";

<Card>
  <CardHeader title="Projects" description="Manage your workspace projects." />
  <Button width="auto" onClick={() => console.info("Create project requested")}>
    Create project
  </Button>
</Card>;

const actionClasses = buttonRecipe({ variant: "secondary", width: "auto" });
```

## Styles and theme

The app owns the Tailwind stylesheet and theme CSS. Import the shared theme stylesheet and UI source declarations into the app’s CSS entry so Tailwind includes component utilities:

```css
@import "@bc-solutions-coder/styles/styles.css";
@import "@bc-solutions-coder/ui/source.css";
@source "./";
```

`source.css` supplies scan paths; it does not load a theme or compiled CSS. Render `DocumentStyles` in the document head with trusted theme CSS prepared by the app and its compiled stylesheet URL (`null` omits the link). Place `ThemeScript` in the head and wrap app content with `ThemeProvider`, using the same `defaultMode` for both. The script resolves the initial theme before the app renders; the provider updates the root document class and persists the visitor’s preference.

```tsx
import { ThemeProvider, ThemeToggle, useTheme } from "@bc-solutions-coder/ui";

function ThemeControls() {
  const { mode } = useTheme();
  return (
    <div>
      Current theme: {mode} <ThemeToggle />
    </div>
  );
}

<ThemeProvider defaultMode="light">
  <ThemeControls />
</ThemeProvider>;
```

Preferences are `light`, `dark`, or `system`; `mode` is the resolved light or dark scheme. Theme classes belong on `document.documentElement`. See [frontend setup](../../docs/development/frontend-setup.md#dark-mode) for complete document wiring.

## Component groups

| Group                 | Main exports                                                                                                                                                                                          |
| --------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Forms                 | `Button`, `Input`, `Textarea`, `Field`, `Fieldset`, `Form`, `Checkbox`, `Radio`, `Select`, `SimpleSelect`, `Combobox`, `Autocomplete`, `Switch`, `Slider`, `NumberField`, `OTPField`, `Toggle`        |
| Overlays              | `Dialog`, `AlertDialog`, `Drawer`, `Popover`, `Tooltip`, `PreviewCard`, `Menu`, `ContextMenu`, `Menubar`                                                                                              |
| Layout and navigation | `Accordion`, `Collapsible`, `Tabs`, `NavigationMenu`, `Toolbar`, `ScrollArea`, `Card`, `PageContainer`, `PageHeader`, `ListCard`, `ListRow`, `QuietLink`, `EmptyState`                                |
| Display               | `Text`, `MutedText`, `Badge`, `Avatar`, `Progress`, `Meter`, `ErrorBanner`, `NoticeBanner`                                                                                                            |
| App integration       | `ThemeProvider`, `ThemeScript`, `ThemeToggle`, `DocumentStyles`, `FocusOnNavigate`, `ReadyIndicator`, `ForkAttribution`, `FailureMessagesProvider`, `FailureBanner`, `FailureToaster`, `toastFailure` |

Multipart components expose Base UI parts on one namespace. Compose the parts explicitly; use the matching prop types for wrappers. `Field` and `Fieldset` are also callable aliases of their `Root` parts. Standalone `Label` requires `Field` context.

```tsx
import { Dialog } from "@bc-solutions-coder/ui";

<Dialog.Root>
  <Dialog.Trigger>View details</Dialog.Trigger>
  <Dialog.Portal>
    <Dialog.Backdrop />
    <Dialog.Popup>
      <Dialog.Title>Project details</Dialog.Title>
      <Dialog.Description>Review the project before continuing.</Dialog.Description>
      <Dialog.Close>Close</Dialog.Close>
    </Dialog.Popup>
  </Dialog.Portal>
</Dialog.Root>;
```

Styled parts accept string `className` overrides, merged after recipe classes. Prefer recipe variants and semantic token utilities. For application forms, [the forms package](../forms/README.md) supplies controls bound to form state and validation.

## Failure and navigation helpers

Wrap failure surfaces with `FailureMessagesProvider` to resolve domain error codes through an app-owned message registry. Nested providers replace the registry. `FailureBanner` renders nothing for a nullish error and accepts optional retry and sign-in actions. Mount `FailureToaster` once before calling `toastFailure`; it uses the active theme and the shared toast store.

`FocusOnNavigate` belongs inside TanStack Router context. After a pathname change it focuses the first main heading without scrolling; it skips initial render and query-only navigation. `ReadyIndicator` exposes the app-ready DOM marker used by browser checks.

## Catalog and development

Run `pnpm --filter @bc-solutions-coder/ui storybook` for the interactive catalog on port 6006. The [component-library guide](../../docs/development/component-library.md) describes composition, recipes, and contribution conventions. Package tests run with `pnpm --filter @bc-solutions-coder/ui test`: Node checks, browser component checks, and Storybook checks with the real stylesheet.
