import { logout } from "@bc-solutions-coder/sdk";
import { Link, type LinkProps } from "@tanstack/react-router";
import { useEffect } from "react";

import { navIconLabels, navIcons, type NavIconName } from "./nav-icons";
import { useIsDesktop } from "../lib/use-is-desktop";
import { useUiStore } from "../stores/ui-store";

/**
 * DashboardNav (Wallow-8w1h.8.1) — the dashboard shell's primary navigation.
 *
 * Renders `Link`s to the dashboard verticals, each carrying a
 * `data-testid="dashboard-nav-<feature>"` testid:
 *   organizations -> /dashboard/organizations (admin-gated, Wallow-ffpq.3.6)
 *   apps          -> /dashboard/apps
 *   settings      -> /dashboard/settings
 *   inquiries     -> /dashboard/inquiries
 *
 * `isAdmin` gates the Organizations link to admins. The gate hides the link only
 * when `isAdmin === false`; an unspecified `isAdmin` (the shell renders
 * `<DashboardNav />` in isolation, e.g. `DashboardLayout.test.tsx`) leaves the
 * link visible. A `dashboard-logout-link` calls the BFF logout, providing a Sign
 * Out control (net-new for Wallow-ffpq.3.6).
 *
 * THREE MODES, TWO AXES (Wallow-0byr.2). Nav state is read from `useUiStore`,
 * never from props: the controls that flip it live in the sibling
 * `DashboardLayout`, so the two components share only the store.
 *
 *   desktop expanded  (isNavCollapsed === false) — `w-64` rail, icon + label.
 *   desktop icon rail (isNavCollapsed === true)  — `w-16` rail, icon ONLY, with
 *                       the label moved to `aria-label`. It is NOT hidden text:
 *                       rendering the label and letting the rail clip it into
 *                       "Settin" / "Sign O" is the bug this epic exists for.
 *   mobile drawer     (below `md`, isMobileNavOpen) — no rail exists at all; the
 *                       drawer is a temporary sheet over the page with the full
 *                       expanded content, dismissed by backdrop, nav link, or
 *                       Escape.
 *
 * `data-nav-open` (the inverse of `isNavCollapsed`) stays the attribute styling
 * and the specs key off. Collapsing stays presentational: the aside stays
 * mounted, so the links and the toggle's `aria-controls` target `#dashboard-nav`
 * keep their identity across both desktop states.
 */

/** A nav destination: where it goes, how tests find it, and which icon names it. */
interface NavDestination {
  readonly to: LinkProps["to"];
  readonly testid: string;
  readonly icon: NavIconName;
}

const destinations: readonly NavDestination[] = [
  { to: "/dashboard/organizations", testid: "dashboard-nav-organizations", icon: "organizations" },
  { to: "/dashboard/apps", testid: "dashboard-nav-apps", icon: "apps" },
  { to: "/dashboard/settings", testid: "dashboard-nav-settings", icon: "settings" },
  { to: "/dashboard/inquiries", testid: "dashboard-nav-inquiries", icon: "inquiries" },
];

const itemClass =
  "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium whitespace-nowrap text-background/80 hover:bg-background/10 hover:text-background no-underline";
const iconOnlyItemClass = `${itemClass} justify-center`;
/** Active-route styling handed to `Link`'s `activeProps`, so both modes share it. */
const activeItemClass = "bg-background/15 text-background";
const iconClass = "size-5 shrink-0";

/**
 * One nav destination. The accessible name always comes from `navIconLabels`,
 * whether or not the label is also rendered, which is what makes "same icon,
 * same name in all three modes" structural rather than hand-maintained. The icon
 * is decorative (`aria-hidden`) precisely because the name is on the item.
 */
function NavItem(props: {
  destination: NavDestination;
  showLabel: boolean;
  onNavigate?: () => void;
}) {
  const Icon = navIcons[props.destination.icon];
  const label: string = navIconLabels[props.destination.icon];
  return (
    <Link
      to={props.destination.to}
      data-testid={props.destination.testid}
      aria-label={label}
      className={props.showLabel ? itemClass : iconOnlyItemClass}
      activeProps={{ className: activeItemClass }}
      onClick={props.onNavigate}
    >
      <Icon aria-hidden="true" className={iconClass} />
      {props.showLabel ? label : null}
    </Link>
  );
}

/**
 * The destination list. Extracted from both the rail and the drawer so the two
 * cannot drift, and so neither exceeds oxlint's `react/jsx-max-depth` budget.
 */
function NavDestinationList(props: {
  showOrganizations: boolean;
  showLabels: boolean;
  onNavigate?: () => void;
}) {
  return (
    <nav className="flex-1 px-4 py-4 flex flex-col gap-1">
      {destinations.map((destination: NavDestination) =>
        destination.icon === "organizations" && !props.showOrganizations ? null : (
          <NavItem
            key={destination.testid}
            destination={destination}
            showLabel={props.showLabels}
            onNavigate={props.onNavigate}
          />
        ),
      )}
    </nav>
  );
}

/** Sign Out — a button rather than a `Link`, since it calls the BFF logout. */
function NavLogout(props: { showLabel: boolean }) {
  const Icon = navIcons.signOut;
  const label: string = navIconLabels.signOut;
  return (
    <div className="px-4 py-4 border-t border-background/10">
      <button
        type="button"
        data-testid="dashboard-logout-link"
        aria-label={label}
        className={`${props.showLabel ? itemClass : iconOnlyItemClass} w-full text-left`}
        onClick={() => {
          logout();
        }}
      >
        <Icon aria-hidden="true" className={iconClass} />
        {props.showLabel ? label : null}
      </button>
    </div>
  );
}

/** The persistent desktop rail — expanded or narrowed to icons, never absent. */
function NavRail(props: { showOrganizations: boolean }) {
  const isNavCollapsed = useUiStore((state) => state.isNavCollapsed);
  return (
    <aside
      id="dashboard-nav"
      data-testid="dashboard-nav"
      data-nav-open={isNavCollapsed ? "false" : "true"}
      className="relative z-30 w-16 data-[nav-open=true]:w-64 bg-foreground text-background flex flex-col shrink-0 transition-[width] duration-200"
    >
      <NavDestinationList
        showOrganizations={props.showOrganizations}
        showLabels={!isNavCollapsed}
      />
      <NavLogout showLabel={!isNavCollapsed} />
    </aside>
  );
}

/**
 * The mobile overlay drawer — expanded content, because a sheet over the page has
 * the room a rail does not. Navigating dismisses it, so it never covers the page
 * it just navigated to.
 */
function NavDrawer(props: { showOrganizations: boolean }) {
  const closeMobileNav = useUiStore((state) => state.closeMobileNav);
  return (
    <div
      id="dashboard-nav-drawer"
      data-testid="dashboard-nav-drawer"
      className="fixed inset-y-0 left-0 z-30 w-64 bg-foreground text-background flex flex-col"
    >
      <NavDestinationList
        showOrganizations={props.showOrganizations}
        showLabels
        onNavigate={closeMobileNav}
      />
      <NavLogout showLabel />
    </div>
  );
}

export function DashboardNav(props: { isAdmin?: boolean } = {}) {
  const showOrganizations = props.isAdmin !== false;
  const isDesktop = useIsDesktop();
  const isMobileNavOpen = useUiStore((state) => state.isMobileNavOpen);
  const closeMobileNav = useUiStore((state) => state.closeMobileNav);

  // Escape dismisses the drawer. The listener is re-registered per open state
  // rather than gated with an early return so the effect has one exit path.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent): void => {
      if (isMobileNavOpen && event.key === "Escape") {
        closeMobileNav();
      }
    };
    globalThis.addEventListener("keydown", onKeyDown);
    return () => {
      globalThis.removeEventListener("keydown", onKeyDown);
    };
  }, [isMobileNavOpen, closeMobileNav]);

  if (!isDesktop) {
    return isMobileNavOpen ? <NavDrawer showOrganizations={showOrganizations} /> : null;
  }
  return <NavRail showOrganizations={showOrganizations} />;
}
