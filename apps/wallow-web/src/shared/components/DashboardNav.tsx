import { logout } from "@bc-solutions-coder/sdk";
// The per-component subpath, NOT the root barrel: the barrel also pulls in
// `FocusOnNavigate`, which imports `useRouterState`, and every spec in this
// directory stubs `@tanstack/react-router` down to `Link` alone. Bundlers
// tree-shake that away; a dev/test module graph does not, so the barrel would
// fail to link here.
import { ErrorBanner } from "@bc-solutions-coder/ui/error-banner";
import { NavigationMenu } from "@bc-solutions-coder/ui/navigation-menu";
import { ThemeToggle } from "@bc-solutions-coder/ui/theme-toggle";
import { Link, type LinkProps } from "@tanstack/react-router";
import { useEffect, useState } from "react";

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
 * Those three are the states a visitor can be IN. Before them sits one render in
 * which the width is not yet known — `useIsDesktop() === undefined` on the
 * server and through hydration (Wallow-lrlm.6.3) — and it is resolved by CSS,
 * not by picking a mode; see `NavRail`'s `hideBelowMd`.
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

/*
 * The rail's palette is NAMED, not mixed (Wallow-lrlm.5.4). `bg-foreground
 * text-background` used to invert the two page colours, which only lands on a
 * sidebar by coincidence — in dark mode it painted a glaring light rail against
 * a dark page. `--color-sidebar` / `--color-sidebar-foreground` /
 * `--color-sidebar-accent` (Wallow-lrlm.1.1) name the surface instead, so both
 * modes are deliberate and a fork rebrands the rail from `branding.json`.
 *
 * The hover (`/10`) and active (`/15`) overlays collapse onto the ONE
 * `sidebar-accent` the theme ships: 10% of the page background over the rail is
 * that token's value exactly. The active row stays legible against an idle one
 * because it alone carries a surface at rest.
 *
 * `hover:text-sidebar-foreground` is NOT redundant with the rest state. A row is
 * a `NavigationMenu.Link`, so what renders is `twMerge(navigationMenuLinkRecipe(),
 * itemClass)` and the recipe contributes `hover:text-accent-foreground`. twMerge
 * only drops a class the caller CONFLICTS with — variant included — so an
 * unmodified `text-sidebar-foreground` leaves the recipe's hover colour standing
 * and the label drops to ~1.3:1 against `sidebar-accent` in light mode. This
 * class is the suppression; `DashboardNav.restyle.test.tsx` asserts the merged
 * output, not this string.
 */
const itemClass =
  "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium whitespace-nowrap text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-foreground no-underline";
const iconOnlyItemClass = `${itemClass} justify-center`;
/** Active-route styling handed to `Link`'s `activeProps`, so both modes share it. */
const activeItemClass = "bg-sidebar-accent text-sidebar-foreground";
const iconClass = "size-5 shrink-0";

/**
 * One nav destination — a `NavigationMenu.Item` (`<li>`) holding a
 * `NavigationMenu.Link` (`<a>`), which is the flat "row that navigates" shape
 * Base UI supports directly inside an item.
 *
 * The routing still belongs to TanStack: `render` hands the anchor over to the
 * router `Link`, so `to` resolves and `activeProps` supplies active-route
 * styling exactly as before, while the catalog contributes the list semantics
 * and its own link recipe underneath our sidebar palette.
 *
 * The accessible name always comes from `navIconLabels`, whether or not the
 * label is also rendered, which is what makes "same icon, same name in all three
 * modes" structural rather than hand-maintained. The icon is decorative
 * (`aria-hidden`) precisely because the name is on the item.
 */
function NavItem(props: {
  destination: NavDestination;
  showLabel: boolean;
  onNavigate?: () => void;
}) {
  const Icon = navIcons[props.destination.icon];
  const label: string = navIconLabels[props.destination.icon];
  return (
    <NavigationMenu.Item>
      <NavigationMenu.Link
        render={<Link to={props.destination.to} activeProps={{ className: activeItemClass }} />}
        data-testid={props.destination.testid}
        aria-label={label}
        className={props.showLabel ? itemClass : iconOnlyItemClass}
        onClick={props.onNavigate}
      >
        <Icon aria-hidden="true" className={iconClass} />
        {props.showLabel ? label : null}
      </NavigationMenu.Link>
    </NavigationMenu.Item>
  );
}

/**
 * The destination list. Extracted from both the rail and the drawer so the two
 * cannot drift, and so neither exceeds oxlint's `react/jsx-max-depth` budget.
 *
 * `NavigationMenu.Root` is the `<nav>` landmark and `NavigationMenu.List` the
 * `<ul>` inside it, so the destinations announce as ONE list of N items rather
 * than N loose links — and a gated-away destination shrinks the count instead of
 * leaving an empty row behind, because the gate drops the whole `Item`.
 */
function NavDestinationList(props: {
  showOrganizations: boolean;
  showLabels: boolean;
  onNavigate?: () => void;
}) {
  return (
    <NavigationMenu.Root className="flex-1 flex-col px-4 py-4">
      <NavigationMenu.List className="flex-col">
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
      </NavigationMenu.List>
    </NavigationMenu.Root>
  );
}

/**
 * The theme control, in the footer band above Sign Out so it is reachable in all
 * THREE nav modes — a toggle that existed only in the expanded rail would vanish
 * the moment a visitor collapsed the nav or opened the dashboard on a phone.
 *
 * The catalog control always renders its state as text ("Light"/"Dark"/
 * "System"), so the icon rail gets a smaller box rather than the label-stripping
 * `showLabel` treatment the destinations get: there is no icon to fall back to.
 */
function NavThemeToggle(props: { showLabel: boolean }) {
  return (
    <div className="px-4 pt-4">
      <ThemeToggle
        data-testid="theme-toggle"
        className={props.showLabel ? "w-full" : "w-full px-1 text-xs"}
      />
    </div>
  );
}

/** Sign Out — a button rather than a `Link`, since it calls the BFF logout. */
function NavLogout(props: { showLabel: boolean }) {
  const Icon = navIcons.signOut;
  const label: string = navIconLabels.signOut;
  const [error, setError] = useState<string | null>(null);

  // `logout()` POSTs to the CSRF-gated `/bff/logout` and navigates on the
  // redirect it answers with, so it can reject (403 CSRF, 405) and leave the
  // session live. Saying so beats a silent no-op button and an unhandled
  // rejection.
  async function signOut(): Promise<void> {
    setError(null);
    try {
      await logout();
    } catch {
      setError("Sign out failed. You are still signed in — please try again.");
    }
  }

  return (
    <div className="px-4 py-4 border-t border-sidebar-accent">
      <button
        type="button"
        data-testid="dashboard-logout-link"
        aria-label={label}
        className={`${props.showLabel ? itemClass : iconOnlyItemClass} w-full text-left`}
        onClick={() => {
          void signOut();
        }}
      >
        <Icon aria-hidden="true" className={iconClass} />
        {props.showLabel ? label : null}
      </button>
      {error === null ? null : (
        <ErrorBanner data-testid="dashboard-logout-error">{error}</ErrorBanner>
      )}
    </div>
  );
}

const railClass =
  "relative z-30 w-16 data-[nav-open=true]:w-64 bg-sidebar text-sidebar-foreground flex-col shrink-0 transition-[width] duration-200";

/**
 * The persistent desktop rail — expanded or narrowed to icons, never absent.
 *
 * `hideBelowMd` is the pre-hydration treatment (Wallow-lrlm.6.3): while
 * `useIsDesktop` still answers `undefined` the rail is emitted but left to the
 * `md` media query, so a phone's FIRST PAINT already omits it. `display: none`
 * is not a half measure — it takes the rail out of the layout, the tab order and
 * the accessibility tree exactly as not rendering it would, for the one render
 * before the real viewport is known. Once it is known the rail is either mounted
 * unconditionally visible or not mounted at all, which is why this stays a
 * pre-hydration escape hatch rather than the steady state.
 */
function NavRail(props: { showOrganizations: boolean; hideBelowMd?: boolean }) {
  const isNavCollapsed = useUiStore((state) => state.isNavCollapsed);
  return (
    <aside
      id="dashboard-nav"
      data-testid="dashboard-nav"
      data-nav-open={isNavCollapsed ? "false" : "true"}
      className={`${props.hideBelowMd ? "hidden md:flex" : "flex"} ${railClass}`}
    >
      <NavDestinationList
        showOrganizations={props.showOrganizations}
        showLabels={!isNavCollapsed}
      />
      <NavThemeToggle showLabel={!isNavCollapsed} />
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
      className="fixed inset-y-0 left-0 z-30 w-64 bg-sidebar text-sidebar-foreground flex flex-col"
    >
      <NavDestinationList
        showOrganizations={props.showOrganizations}
        showLabels
        onNavigate={closeMobileNav}
      />
      <NavThemeToggle showLabel />
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

  // No viewport observed yet (server render, first hydration render): hand the
  // breakpoint to CSS instead of guessing one. The drawer is not a candidate
  // here — it opens only from a control the visitor has not been able to press.
  if (isDesktop === undefined) {
    return <NavRail showOrganizations={showOrganizations} hideBelowMd />;
  }
  if (!isDesktop) {
    return isMobileNavOpen ? <NavDrawer showOrganizations={showOrganizations} /> : null;
  }
  return <NavRail showOrganizations={showOrganizations} />;
}
