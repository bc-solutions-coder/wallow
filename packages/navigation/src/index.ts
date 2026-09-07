/**
 * Application shell components, destination types, and shared navigation state. Import the
 * JavaScript API from this entry; source.css supplies Tailwind scan paths.
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
