import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { getRouter } from "@app/router";
import { Route } from "./index";

/**
 * Route spec for the dashboard apps list route (Wallow-8w1h.5.2), mirroring
 * routes/dashboard/organizations/index.test.tsx. Covers two contracts:
 *   1. The route page renders a root carrying `data-testid="dashboard-apps"` and
 *      prefetches via a `loader`.
 *   2. `src/router.tsx` registers the route under the root at `/dashboard/apps`
 *      (bound manually alongside the organizations routes, no layout route yet).
 */

// The rendered page mounts AppList, whose `useQuery` now runs for real against
// the harness transport (Wallow-pu6a.5.5) — the facade this spec used to mock is
// deleted, and there is nothing left in the path to stub.

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("routes/dashboard/apps (route page)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("prefetches the app list via a loader", () => {
    expect(Route.options.loader).toBeDefined();
  });

  it("renders a page root carrying data-testid=dashboard-apps", async () => {
    harness.resolveJson([]);

    const Page = Route.options.component!;
    renderWithWallow(<Page />, { harness });

    await expect.element(page.getByTestId("dashboard-apps")).toBeInTheDocument();
  });

  // Wallow-ffpq.3.5 — the apps index links to the register route so
  // RegisterAppForm is reachable via normal UI navigation (the
  // `apps-register-link`), not just a directly-typed URL.
  it("links to the register route (apps-register-link)", async () => {
    harness.resolveJson([]);

    const Page = Route.options.component!;
    renderWithWallow(<Page />, { harness });

    const link = page.getByTestId("apps-register-link");
    await expect.element(link).toBeInTheDocument();
    await expect.element(link).toHaveAttribute("href", "/dashboard/apps/register");
  });
});

describe("routes/dashboard/apps (router registration)", () => {
  it("registers /dashboard/apps in the router tree", () => {
    const router = getRouter();
    const paths = Object.keys(router.routesByPath);
    expect(paths).toContain("/dashboard/apps");
  });
});
