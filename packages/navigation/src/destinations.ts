import type { LinkProps } from "@tanstack/react-router";

import type { NavIconComponent } from "./nav-icons";

/**
 * What a destination requires of the visitor before it is offered.
 *
 * The shape is deliberately inert here — the package never evaluates it. It is
 * handed straight back to `AppShell`'s `can` predicate, which is the app's, so a
 * fork with a different auth model keeps using this package. That is the whole
 * reason there is no `@bc-solutions-coder/auth` edge.
 */
export interface NavRequirement {
  readonly role?: string;
  readonly permission?: string;
}

/**
 * One nav destination: where it goes, what it is called, which icon names it,
 * and who may see it.
 *
 * `to` is typed as the router's own, so a consumer that has registered its route
 * tree gets its destinations checked against it. Inside this package there is no
 * registration, so the same type is the router's unregistered fallback.
 */
export interface NavDestination {
  /**
   * A stable key, and the testid suffix: the rendered row carries
   * `data-testid={`${testIdPrefix}-${id}`}`. `id: "nav-apps"` under the default
   * prefix is `dashboard-nav-apps`.
   */
  readonly id: string;
  readonly to: LinkProps["to"];
  /**
   * The accessible name, in all three modes. It is the visible label in the
   * expanded rail and the drawer, and the `aria-label` in the icon rail — never
   * both spelled separately, which is what keeps the modes from drifting.
   */
  readonly label: string;
  readonly icon: NavIconComponent;
  /** Handed to `can`; a destination with none is always visible. */
  readonly requires?: NavRequirement;
}
