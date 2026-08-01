/**
 * `@bc-solutions-coder/navigation` — the application shell: a collapsible
 * desktop rail, a mobile overlay drawer, the controls that drive them, and the
 * store the two halves share.
 *
 * ONE entry, not a per-component catalog like `packages/ui`: this is a single
 * cohesive frame, and `useNavStore` must resolve to exactly one module instance
 * (see its header). Exporting the store from a second subpath would be a second
 * store.
 */
export { navRowClassName } from "./app-nav";
export { AppShell, type AppShellProps } from "./app-shell";
export type { NavDestination, NavRequirement } from "./destinations";
export {
  defaultNavControlIcons,
  defaultNavControlLabels,
  type NavControlIcons,
  type NavIconComponent,
} from "./nav-icons";
export { useNavStore, type NavState } from "./nav-store";
export { useIsDesktop } from "./use-is-desktop";
