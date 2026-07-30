import {
  Building2,
  LayoutGrid,
  LogOut,
  Menu,
  MessageSquare,
  PanelLeft,
  Settings,
  X,
} from "lucide-react";
import type { ComponentType, SVGProps } from "react";

/**
 * The dashboard nav's icon set (Wallow-0byr.1) — one icon and one accessible
 * name per nav destination and per nav control, in a single map.
 *
 * WHY A MAP AND NOT INLINE IMPORTS: the nav renders in three modes (expanded
 * desktop, collapsed icon rail, mobile overlay drawer) and each item must show
 * the SAME icon under the SAME accessible name in all three. Importing icons at
 * each render site lets the three modes drift apart; resolving them through one
 * keyed map makes the shared identity structural.
 *
 * The icons themselves come from a tree-shakeable per-icon React icon library
 * (each icon is its own ES export, never an icon font) so the client bundle only
 * carries the eight icons named here. That library is `lucide-react`: every icon
 * is a named ES export of a plain SVG-props component, it ships no icon font and
 * no runtime dependencies, and it peers on React 19. Swapping it out means
 * editing this module's imports and nothing else.
 */

/** Every nav destination and nav control that owns an icon. */
export type NavIconName =
  | "organizations"
  | "apps"
  | "settings"
  | "inquiries"
  | "signOut"
  | "navToggle"
  | "mobileMenu"
  | "close";

/**
 * An icon: a React component taking plain SVG props. Deliberately library-
 * agnostic — consumers pass `className` for sizing and nothing else, so swapping
 * icon libraries never reaches past this module.
 */
export type NavIconComponent = ComponentType<SVGProps<SVGSVGElement>>;

/**
 * Icon per nav item/control. Icons are decorative: the accessible name comes
 * from `navIconLabels`, so render these with `aria-hidden`.
 */
export const navIcons: Record<NavIconName, NavIconComponent> = {
  organizations: Building2,
  apps: LayoutGrid,
  settings: Settings,
  inquiries: MessageSquare,
  signOut: LogOut,
  // The two menu affordances deliberately differ: the desktop control collapses a
  // rail that stays on screen, the mobile one summons a drawer that is not there.
  navToggle: PanelLeft,
  mobileMenu: Menu,
  close: X,
};

/**
 * Accessible name per nav item/control — the single source for the `aria-label`
 * an icon-only control needs in the collapsed rail and for the visible label the
 * expanded and mobile modes render.
 */
export const navIconLabels: Record<NavIconName, string> = {
  organizations: "Organizations",
  apps: "Apps",
  settings: "Settings",
  inquiries: "Inquiries",
  signOut: "Sign Out",
  navToggle: "Toggle navigation",
  mobileMenu: "Open navigation",
  // Matches the existing `NavBackdrop` aria-label in `DashboardLayout`, the only
  // icon-button naming precedent in the app.
  close: "Close navigation",
};
