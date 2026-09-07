import { Menu, PanelLeft, X } from "lucide-react";
import type { ComponentType, SVGProps } from "react";

/**
 * Default icons and accessible labels for shell controls. Destination icons are supplied
 * separately on each NavDestination.
 */

/**
 * React SVG component used for navigation icons. Receives className for sizing and aria-hidden
 * because the surrounding control supplies its accessible name.
 */
export type NavIconComponent = ComponentType<SVGProps<SVGSVGElement>>;

/** The three controls a consumer may re-icon. */
export interface NavControlIcons {
  /** Desktop: collapse/expand the persistent rail. */
  readonly navToggle: NavIconComponent;
  /** Mobile: summon the overlay drawer. */
  readonly mobileMenu: NavIconComponent;
  /** Reserved close icon slot. The current backdrop has an accessible label but renders no icon. */
  readonly close: NavIconComponent;
}

/** The lucide defaults, overridable per control through `AppShell`'s `icons`. */
export const defaultNavControlIcons: NavControlIcons = {
  // The two menu affordances deliberately differ: the desktop control collapses a
  // rail that stays on screen, the mobile one summons a drawer that is not there.
  navToggle: PanelLeft,
  mobileMenu: Menu,
  close: X,
};

/**
 * Accessible name per control — the single source for the `aria-label` each
 * icon-only control needs, in every render mode.
 */
export const defaultNavControlLabels: Readonly<Record<keyof NavControlIcons, string>> = {
  navToggle: "Toggle navigation",
  mobileMenu: "Open navigation",
  close: "Close navigation",
};
