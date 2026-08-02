import type { NavDestination } from "@bc-solutions-coder/navigation";
import { Building2, LayoutGrid, MailPlus, MessageSquare, Settings } from "lucide-react";

/**
 * The dashboard's nav manifest — one entry per vertical, in render order.
 *
 * The shell renders each entry in all three of its modes (expanded rail,
 * collapsed icon rail, mobile drawer) from THIS entry, which is what makes "same
 * icon, same accessible name everywhere" structural: there is one place to
 * change and three renders that read it.
 *
 * `id` is the testid suffix. Under the shell's default `testIdPrefix` of
 * `"dashboard"` these produce `dashboard-nav-organizations`,
 * `dashboard-nav-invitations`, `dashboard-nav-apps`, `dashboard-nav-settings`
 * and `dashboard-nav-inquiries`.
 *
 * `requires` is inert to the package — it is handed straight back to the `can`
 * predicate `DashboardLayout` supplies, which is what keeps
 * `@bc-solutions-coder/navigation` free of an auth dependency. Organizations and
 * Invitations are the admin-gated destinations; the invitations screen has no
 * `orgId` of its own to scope a non-admin's view by (it reads the caller's
 * ambient tenant), so it carries the same gate as Organizations.
 *
 * The icons come straight from `lucide-react` rather than through a keyed map:
 * the manifest already enforces one icon per destination, so a map would only
 * add a second name to keep in step.
 */
export const ADMIN_ROLE = "Admin";

export const dashboardDestinations: readonly NavDestination[] = [
  {
    id: "nav-organizations",
    to: "/dashboard/organizations",
    label: "Organizations",
    icon: Building2,
    requires: { role: ADMIN_ROLE },
  },
  {
    id: "nav-invitations",
    to: "/dashboard/organizations/invitations",
    label: "Invitations",
    icon: MailPlus,
    requires: { role: ADMIN_ROLE },
  },
  { id: "nav-apps", to: "/dashboard/apps", label: "Apps", icon: LayoutGrid },
  { id: "nav-settings", to: "/dashboard/settings", label: "Settings", icon: Settings },
  { id: "nav-inquiries", to: "/dashboard/inquiries", label: "Inquiries", icon: MessageSquare },
];
