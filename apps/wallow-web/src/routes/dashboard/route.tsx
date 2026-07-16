import { login } from "@bc-solutions-coder/sdk";
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
 */
export const Route = createFileRoute("/dashboard")({
  beforeLoad: async ({ location }) => {
    const user = await getWallowSdk().user.me();
    if (user === null) {
      login(location.pathname);
    }
  },
  component: DashboardLayout,
});
