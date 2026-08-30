import { ensureCurrentUser } from "@bc-solutions-coder/auth";
import { forkBranding } from "@bc-solutions-coder/styles";
import { createFileRoute, redirect } from "@tanstack/react-router";

import { PublicLayout } from "@shared/components/PublicLayout";
import { LandingPage } from "@features/landing";

/**
 * The public home page (Wallow-8w1h.2.2 / Wallow-ffpq.3.6) — the anonymous
 * marketing landing at `/`.
 *
 * "Who is signed in" comes from `@bc-solutions-coder/auth` (Wallow-x4qn.8), the
 * one definition of it in this workspace; `ensureCurrentUser` is its
 * `beforeLoad` primer — `ensureQueryData(currentUserQuery(client))` — so this
 * gate and `/dashboard`'s read the SAME cache entry with the same 30s staleTime.
 *
 * The `beforeLoad` gate:
 *   - an AUTHENTICATED visitor is redirected to the dashboard
 *     (`/dashboard/my-organizations`) via a TanStack `redirect`,
 *   - an unauthenticated visitor is shown the marketing page only when
 *     `forkBranding.landingPage.enabled`,
 *   - otherwise a thrown TanStack `redirect()` sends them to the BFF login (a
 *     forced OIDC challenge), landing back on the dashboard afterwards.
 *
 * That forced-login branch must never navigate by assigning to the bare global
 * `location` (as the SDK's since-deleted `login(returnTo)` helper did): it does
 * not exist under Node, so a full-page SSR load of `/` would throw and the
 * request handler would surface it as HTTP 500 instead of a redirect
 * (Wallow-fqw9, the sibling of the `/dashboard` fix in Wallow-zyxe). A thrown `redirect()` works on both sides —
 * the SSR request handler turns it into a 307 with a `Location` header, and the
 * client router navigates. It is marked `reloadDocument` because `/bff/login` is
 * a BFF endpoint rather than a route in the TanStack tree, so a relative href
 * would otherwise be committed against the route tree and land on a not-found
 * match.
 *
 * The page body is `LandingPage` — the `<h1 data-testid="home-heading">` that the
 * SSR contract asserts now lives inside that component's hero, wrapped here in
 * the `PublicLayout` navbar/footer chrome.
 */
function HomeComponent() {
  return (
    <PublicLayout>
      <LandingPage />
    </PublicLayout>
  );
}

export const Route = createFileRoute("/")({
  beforeLoad: async ({ context }) => {
    const user = await ensureCurrentUser({
      queryClient: context.queryClient,
      client: context.sdk.client,
    });
    if (user !== null) {
      // TanStack stores the target under `.options.to`; also surface `to` at the
      // top level so it reads directly off the thrown redirect.
      throw Object.assign(redirect({ to: "/dashboard/my-organizations" }), {
        to: "/dashboard/my-organizations",
      });
    }
    if (!forkBranding.landingPage.enabled) {
      throw redirect({
        href: "/bff/login?returnTo=%2Fdashboard%2Fmy-organizations",
        reloadDocument: true,
      });
    }
  },
  component: HomeComponent,
});
