import { Building2, LayoutGrid, LogOut, MessageSquare, Settings } from "lucide-react";
import type { ReactNode } from "react";

import { navRowClassName } from "./app-nav";
import { AppShell } from "./app-shell";
import type { NavDestination } from "./destinations";

/**
 * The manifest and slots every spec in this package renders the shell with.
 *
 * It is deliberately the shape a real consumer supplies — four destinations, one
 * of them role-gated, and a footer control that is a plain `<button>` rather than
 * a catalog row. A fixture that skipped either would let a defect through in the
 * exact place the package's API is load-bearing: the gate that drops a whole
 * `<li>`, and the footer band's separator.
 *
 * The ids and labels reproduce the testids a consuming dashboard renders
 * (`dashboard-nav-apps`, `dashboard-logout-link`) so a spec reads the same in
 * either place.
 *
 * Not shipped: nothing under `src/index.ts` imports it, so neither the Vite lib
 * build nor `tsc -p tsconfig.build.json` reaches it.
 */

/** The role a gated destination asks for; `ShellFixture` answers it from `isAdmin`. */
const ADMIN_ROLE = "Admin";

export const fixtureDestinations: readonly NavDestination[] = [
  {
    id: "nav-organizations",
    to: "/dashboard/organizations",
    label: "Organizations",
    icon: Building2,
    requires: { role: ADMIN_ROLE },
  },
  { id: "nav-apps", to: "/dashboard/apps", label: "Apps", icon: LayoutGrid },
  { id: "nav-settings", to: "/dashboard/settings", label: "Settings", icon: Settings },
  { id: "nav-inquiries", to: "/dashboard/inquiries", label: "Inquiries", icon: MessageSquare },
];

/**
 * A footer slot in the shape a consumer's really is: a `<button>` that states
 * the rail's rest/hover pair itself, because it is not a catalog row and gets
 * none of the `surface="sidebar"` treatment the destinations do.
 */
function FixtureFooter(props: { showLabel: boolean }) {
  return (
    <button
      type="button"
      data-testid="dashboard-logout-link"
      aria-label="Sign Out"
      className={`${navRowClassName(props.showLabel)} text-sidebar-foreground hover:bg-sidebar-accent w-full text-left`}
    >
      <LogOut aria-hidden="true" className="size-5 shrink-0" />
      {props.showLabel ? "Sign Out" : null}
    </button>
  );
}

/**
 * Module scope, mirroring a consumer: the shell CALLS this slot, so what it
 * returns is ordinary markup — but written inline in a component body it reads
 * as a nested component definition to a reader and to the linter alike.
 */
const renderFixtureFooter = (showLabel: boolean) => <FixtureFooter showLabel={showLabel} />;

/**
 * The shell under test.
 *
 * `isAdmin` mirrors a consumer's gate exactly, including its one asymmetry: the
 * gate hides the role-bound destination only when `isAdmin === false`. Left
 * unspecified — a shell rendered in isolation — the destination stays visible,
 * so a spec about the rail's geometry does not have to know about roles.
 */
export function ShellFixture(
  props: { isAdmin?: boolean; testIdPrefix?: string; children?: ReactNode } = {},
) {
  return (
    <AppShell
      destinations={fixtureDestinations}
      can={(requirement) => requirement.role !== ADMIN_ROLE || props.isAdmin !== false}
      footer={renderFixtureFooter}
      testIdPrefix={props.testIdPrefix}
    >
      {props.children}
    </AppShell>
  );
}
