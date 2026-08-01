import { Menu, PanelLeft, X } from "lucide-react";
import type { ComponentType, SVGProps } from "react";

/**
 * The shell's three CONTROL icons and their accessible names.
 *
 * Destination icons are not here — they come from each entry of the
 * `destinations` manifest, which is what makes "same icon, same name in all
 * three render modes" structural: one entry, three renders. The controls have no
 * destination to hang off, so the package ships lucide defaults and takes an
 * `icons` override.
 *
 * The icons come from a tree-shakeable per-icon React icon library (each icon is
 * its own ES export, never an icon font) so a consumer's bundle only carries the
 * three named here. That library is `lucide-react`: every icon is a named ES
 * export of a plain SVG-props component, it ships no icon font and no runtime
 * dependencies, and it peers on React 19. Swapping it out means editing this
 * module's imports and nothing else.
 */

/**
 * An icon: a React component taking plain SVG props. Deliberately library-
 * agnostic — the shell passes `className` for sizing and nothing else, so
 * swapping icon libraries never reaches past this module.
 */
export type NavIconComponent = ComponentType<SVGProps<SVGSVGElement>>;

/** The three controls a consumer may re-icon. */
export interface NavControlIcons {
  /** Desktop: collapse/expand the persistent rail. */
  readonly navToggle: NavIconComponent;
  /** Mobile: summon the overlay drawer. */
  readonly mobileMenu: NavIconComponent;
  /** Mobile: dismiss the overlay drawer (the backdrop's accessible name). */
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
