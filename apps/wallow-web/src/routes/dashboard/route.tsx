import { login, type WallowUser } from "@bc-solutions-coder/sdk";
import { createFileRoute } from "@tanstack/react-router";

import { DashboardLayout } from "../../components/DashboardLayout";
import { getWallowSdk } from "../../lib/wallow-sdk";

/**
 * The `/dashboard` layout route (Wallow-8w1h.8.1) — the authenticated shell that
 * wraps the organizations/apps/settings/inquiries child routes (reparented under
 * it in `src/router.tsx`) and gates them behind an auth check.
 *
 * `beforeLoad` reads the current user via the `getWallowSdk().user.me()` facade
 * (which delegates to the SDK's `getUser()` — GET `/bff/user`, resolving `null`
 * on 401). When there is no user it calls the SDK's `login(returnTo)`, which
 * performs a real browser navigation to the BFF login (`location.href = ...`),
 * NOT a TanStack `redirect()`. The `returnTo` is the path the user was heading
 * to, so they land back on the gated page after authenticating.
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
  beforeLoad: async ({ location }) => {
    const user = await getWallowSdk().user.me();
    if (user === null) {
      login(location.pathname);
      return { isAdmin: false };
    }
    return { isAdmin: isAdminUser(user) };
  },
  component: DashboardShell,
});
