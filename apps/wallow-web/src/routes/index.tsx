import { login } from "@bc-solutions-coder/sdk";
import { createFileRoute, redirect } from "@tanstack/react-router";

import { PublicLayout } from "../components/PublicLayout";
import { forkBranding } from "../lib/branding";
import { getWallowSdk } from "../lib/wallow-sdk";

/**
 * The public home page (Wallow-8w1h.2.2 / Wallow-ffpq.3.6) — the React port of
 * Blazor `Home.razor` (`@page "/"`, `@layout PublicLayout`, `[AllowAnonymous]`).
 *
 * The `beforeLoad` gate mirrors `Home.razor`'s `OnInitializedAsync`:
 *   - an AUTHENTICATED visitor is redirected to the dashboard (Blazor:
 *     `NavigateTo("/dashboard/apps")`) via a TanStack `redirect`,
 *   - an unauthenticated visitor is shown the marketing page only when
 *     `forkBranding.landingPage.enabled` (Blazor: `Branding.LandingPage.Enabled`),
 *   - otherwise they are sent to the BFF login (Blazor's forced OIDC challenge),
 *     landing back on the dashboard afterwards.
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
  beforeLoad: async () => {
    const user = await getWallowSdk().user.me();
    if (user !== null) {
      // TanStack stores the target under `.options.to`; also surface `to` at the
      // top level so it reads directly off the thrown redirect.
      throw Object.assign(redirect({ to: "/dashboard/apps" }), { to: "/dashboard/apps" });
    }
    if (!forkBranding.landingPage.enabled) {
      login("/dashboard/apps");
    }
  },
  component: HomeComponent,
});
