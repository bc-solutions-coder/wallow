import { userQueries } from "@bc-solutions-coder/sdk/query";
import { createFileRoute, redirect } from "@tanstack/react-router";

import { PublicLayout } from "../components/PublicLayout";
import { forkBranding } from "../lib/branding";

/**
 * The public home page (Wallow-8w1h.2.2 / Wallow-ffpq.3.6) — the anonymous
 * marketing landing at `/`.
 *
 * The `beforeLoad` gate:
 *   - an AUTHENTICATED visitor is redirected to the dashboard
 *     (`/dashboard/apps`) via a TanStack `redirect`,
 *   - an unauthenticated visitor is shown the marketing page only when
 *     `forkBranding.landingPage.enabled`,
 *   - otherwise a thrown TanStack `redirect()` sends them to the BFF login (a
 *     forced OIDC challenge), landing back on the dashboard afterwards.
 *
 * That forced-login branch deliberately does NOT use the SDK's `login(returnTo)`
 * helper: it assigns to the bare global `location`, which does not exist under
 * Node, so a full-page SSR load of `/` would throw and the request handler would
 * surface it as HTTP 500 instead of a redirect (Wallow-fqw9, the sibling of the
 * `/dashboard` fix in Wallow-zyxe). A thrown `redirect()` works on both sides —
 * the SSR request handler turns it into a 307 with a `Location` header, and the
 * client router navigates. It is marked `reloadDocument` because `/bff/login` is
 * a BFF endpoint rather than a route in the TanStack tree, so a relative href
 * would otherwise be committed against the route tree and land on a not-found
 * match.
 *
 * The component still server-renders an `<h1 data-testid="home-heading">` (the
 * SSR contract the boot smoke test asserts), now wrapped in the `PublicLayout`
 * navbar/footer chrome.
 */
function HomeComponent() {
  return (
    <PublicLayout>
      <section className="max-w-4xl mx-auto px-6 py-24 flex flex-col items-center gap-6 text-center">
        <h1 data-testid="home-heading" className="text-5xl font-bold text-foreground">
          {forkBranding.appName}
        </h1>
        <p className="text-lg text-foreground/80">{forkBranding.tagline}</p>
        <a
          href="/bff/login?returnTo=/dashboard/apps"
          className="bg-primary text-primary-foreground text-sm font-medium px-6 py-3 rounded-full no-underline"
        >
          Get Started
        </a>
      </section>
    </PublicLayout>
  );
}

export const Route = createFileRoute("/")({
  beforeLoad: async ({ context }) => {
    const user = await context.queryClient.ensureQueryData(userQueries.currentUser());
    if (user !== null) {
      // TanStack stores the target under `.options.to`; also surface `to` at the
      // top level so it reads directly off the thrown redirect.
      throw Object.assign(redirect({ to: "/dashboard/apps" }), { to: "/dashboard/apps" });
    }
    if (!forkBranding.landingPage.enabled) {
      throw redirect({
        href: "/bff/login?returnTo=%2Fdashboard%2Fapps",
        reloadDocument: true,
      });
    }
  },
  component: HomeComponent,
});
