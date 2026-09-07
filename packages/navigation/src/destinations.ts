import type { LinkProps } from "@tanstack/react-router";

import type { NavIconComponent } from "./nav-icons";

/**
 * App-defined role and permission requirements passed unchanged to AppShell.can. This package
 * does not evaluate or authorize them.
 */
export interface NavRequirement {
  /** Role name for the app-supplied visibility predicate to interpret. */
  readonly role?: string;
  /** Permission name for the app-supplied visibility predicate to interpret. */
  readonly permission?: string;
}

/**
 * One navigation link with a stable identity, accessible label, and icon. to uses TanStack Router
 * link typing, including route registration supplied by the app.
 */
export interface NavDestination {
  /**
   * A stable key, and the testid suffix: the rendered row carries
   * `data-testid={`${testIdPrefix}-${id}`}`. `id: "nav-apps"` under the default
   * prefix is `dashboard-nav-apps`.
   */
  readonly id: string;
  /** Route destination interpreted by the surrounding TanStack Router. */
  readonly to: LinkProps["to"];
  /**
   * The accessible name, in all three modes. It is the visible label in the
   * expanded rail and the drawer, and the `aria-label` in the icon rail — never
   * both spelled separately, which is what keeps the modes from drifting.
   */
  readonly label: string;
  /** Decorative SVG component used in the expanded rail, icon rail, and drawer. */
  readonly icon: NavIconComponent;
  /** Handed to `can`; a destination with none is always visible. */
  readonly requires?: NavRequirement;
}
