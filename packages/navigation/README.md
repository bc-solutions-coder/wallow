# @bc-solutions-coder/navigation

Responsive application shell with a collapsible desktop rail, a mobile drawer,
and a shared navigation store. This is a private workspace package. The app owns
the destinations, visibility rules, routed content, and footer actions.

## Setup

Render `AppShell` inside TanStack Router. Supply the UI `ThemeProvider` so its
built-in theme toggle can change the preference. Load your app's generated theme
and Tailwind CSS, including these scan entries:

```css
@import "@bc-solutions-coder/ui/source.css";
@import "@bc-solutions-coder/navigation/source.css";
```

These imports register source paths; they do not supply theme token values.
`wallowAppConfig` includes the SSR aliases needed by the Zustand React binding.
For a custom Vite setup, use the anchored shim aliases in [CLAUDE.md](CLAUDE.md).

```tsx
import { AppShell, type NavDestination } from "@bc-solutions-coder/navigation";
import { LayoutGrid } from "lucide-react";
import type { ReactNode } from "react";

const destinations: readonly NavDestination[] = [
  { id: "nav-home", to: "/", label: "Home", icon: LayoutGrid },
];

export function Shell({ children }: { children: ReactNode }) {
  return <AppShell destinations={destinations}>{children}</AppShell>;
}
```

## Exports and options

- `AppShell` and `AppShellProps` accept destinations in render order. `children`
  supplies main content; `header` and `footer` callbacks receive `showLabel`,
  which is false only for the collapsed icon rail.
- `NavDestination` defines `id`, `to`, `label`, `icon`, and optional `requires`.
  `NavRequirement` carries role and permission names for the app's `can` predicate.
  Without `can`, all destinations are visible. Hiding a link does not authorize
  or protect its route.
- `navRowClassName(showLabel)` gives footer controls matching row geometry.
  Supply sidebar colors separately.
- `NavIconComponent`, `NavControlIcons`, `defaultNavControlIcons`, and
  `defaultNavControlLabels` support control customization. The `close` icon slot
  is accepted but is not currently rendered; the backdrop uses the close label.
- `useNavStore` and `NavState` expose independent desktop-collapse and
  mobile-open flags with their actions. The module-global store is shared by
  all shells, is not persisted, and should not be changed during SSR requests.
- `useIsDesktop()` returns true at 48rem and above, false below, and undefined on
  the server and first hydration render. CSS supplies the initial responsive layout.

The drawer closes on a destination click, backdrop click, or Escape. It is not a
modal dialog and does not provide focus trapping. `testIdPrefix` defaults to
`dashboard` and prefixes shell controls and destination IDs; the built-in theme
toggle keeps the fixed `theme-toggle` test ID.
