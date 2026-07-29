import { isAdmin, requireAuth } from "@bc-solutions-coder/sdk";
import { createFileRoute, redirect } from "@tanstack/react-router";

import { DashboardLayout } from "../../components/DashboardLayout";
import { currentUserQuery } from "../../lib/current-user";

/**
 * The `/dashboard` layout route (Wallow-8w1h.8.1) — the authenticated shell that
 * wraps the organizations/apps/settings/inquiries child routes (reparented under
 * it in `src/router.tsx`) and gates them behind an auth check.
 *
 * `beforeLoad` reads the current user through the router-context QueryClient via
 * `ensureQueryData(currentUserQuery(context.sdk.client))`, so the cached entry
 * (staleTime 30s) is reused across navigations instead of re-reading the user on
 * every route change (it resolves `null` on 401). The gate itself is the
 * SDK's shared `requireAuth` (Wallow-pu6a.5.6) rather than logic hand-rolled
 * here: it returns the user when there is one and otherwise throws the
 * `redirect()` handed to it, built by the SDK's `loginRedirect`. Injecting
 * TanStack's `redirect` keeps the SDK router-free while the throw still works on
 * both sides — the SSR request handler turns it into a 307 with a `Location`
 * header, and the client router navigates. `loginRedirect` owns both the encoded
 * `returnTo` (so the visitor lands back on the gated page) and `reloadDocument`,
 * which is required because `/bff/login` is a BFF endpoint rather than a route in
 * the TanStack tree. It deliberately does NOT use the SDK's `login(returnTo)`
 * helper: that assigns to the bare global `location`, which does not exist under
 * Node, so a full-page SSR load of `/dashboard/**` returned HTTP 500 instead of a
 * redirect (Wallow-zyxe).
 *
 * When a user IS present it derives `isAdmin` from the user's roles claim
 * (Wallow-ffpq.3.6) and exposes it on the route context so the
 * shell can gate the Organizations nav link.
 */

function DashboardShell() {
  const context = Route.useRouteContext();
  return <DashboardLayout isAdmin={context.isAdmin} />;
}

export const Route = createFileRoute("/dashboard")({
  beforeLoad: async ({ context, location }) => {
    const user = requireAuth({
      user: await context.queryClient.ensureQueryData(currentUserQuery(context.sdk.client)),
      returnTo: location.pathname,
      redirect,
    });
    return { isAdmin: isAdmin(user) };
  },
  component: DashboardShell,
});
