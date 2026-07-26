import type { WallowUser } from "@bc-solutions-coder/sdk";
import { userQueries } from "@bc-solutions-coder/sdk/query";
import { createFileRoute, redirect } from "@tanstack/react-router";

import { DashboardLayout } from "../../components/DashboardLayout";

/**
 * The `/dashboard` layout route (Wallow-8w1h.8.1) — the authenticated shell that
 * wraps the organizations/apps/settings/inquiries child routes (reparented under
 * it in `src/router.tsx`) and gates them behind an auth check.
 *
 * `beforeLoad` reads the current user through the router-context QueryClient via
 * `ensureQueryData(userQueries.currentUser())` (SDK query layer), so the cached
 * entry (staleTime 30s) is reused across navigations instead of refetching GET
 * `/bff/user` every time (it resolves `null` on 401). When there is no user it
 * throws a TanStack `redirect()` to the BFF login. It deliberately does NOT use
 * the SDK's `login(returnTo)` helper: that assigns to the bare global
 * `location`, which does not exist under Node, so a full-page SSR load of
 * `/dashboard/**` returned HTTP 500 instead of a redirect (Wallow-zyxe). A
 * thrown `redirect()` works on both sides — the SSR request handler turns it
 * into a 307 with a `Location` header, and the client router navigates. It is
 * marked `reloadDocument` because `/bff/login` is a BFF endpoint rather than a
 * route in the TanStack tree, so a relative href would otherwise be committed
 * against the route tree and land on a not-found match. The `returnTo` is the
 * path the user was heading to, so they land back on the gated page after
 * authenticating.
 *
 * When a user IS present it derives `isAdmin` from the user's roles claim
 * (Wallow-ffpq.3.6) and exposes it on the route context so the
 * shell can gate the Organizations nav link.
 */

/** True when the user's roles claim contains an `admin` role (case-insensitive). */
function isAdminUser(user: WallowUser): boolean {
  const raw: unknown = user.roles ?? user.role;
  let roles: unknown[] = [];
  if (Array.isArray(raw)) {
    roles = raw;
  } else if (typeof raw === "string") {
    roles = [raw];
  }
  return roles.some((role) => String(role).toLowerCase() === "admin");
}

function DashboardShell() {
  const { isAdmin } = Route.useRouteContext();
  return <DashboardLayout isAdmin={isAdmin} />;
}

export const Route = createFileRoute("/dashboard")({
  beforeLoad: async ({ context, location }) => {
    const user = await context.queryClient.ensureQueryData(userQueries.currentUser());
    if (user === null) {
      throw redirect({
        href: `/bff/login?returnTo=${encodeURIComponent(location.pathname)}`,
        reloadDocument: true,
      });
    }
    return { isAdmin: isAdminUser(user) };
  },
  component: DashboardShell,
});
