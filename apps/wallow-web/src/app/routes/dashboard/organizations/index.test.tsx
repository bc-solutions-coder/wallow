import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { getRouter } from "@app/router";
import { Route } from "./index";

/**
 * Route spec for the CANONICAL dashboard list route (Wallow-8w1h.4.2). Covers
 * two contracts every later vertical (Phases 4-6) copies:
 *   1. The route page renders a root carrying `data-testid="dashboard-
 *      organizations"` and prefetches via a `loader`.
 *   2. `src/router.tsx` registers the route under the root at
 *      `/dashboard/organizations` (no dashboard layout route exists yet, so the
 *      route is bound manually — see router.tsx's `indexRouteWithParent`).
 */

// The rendered page mounts OrganizationList, whose `useQuery` now runs for real
// against the harness transport (Wallow-pu6a.5.5) — the facade this spec used to
// mock is deleted, and there is nothing left in the path to stub.

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("routes/dashboard/organizations (route page)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("prefetches the org list via a loader", () => {
    expect(Route.options.loader).toBeDefined();
  });

  it("renders a page root carrying data-testid=dashboard-organizations", async () => {
    harness.resolveJson([]);

    const Page = Route.options.component!;
    renderWithWallow(<Page />, { harness });

    await expect.element(page.getByTestId("dashboard-organizations")).toBeInTheDocument();
  });

  // Wallow-ffpq.3.5 — the orphan CreateOrganizationForm mounts INLINE on this
  // index page (NOT a separate /dashboard/organizations/create route — no such
  // route exists and the bead's AC forbids recreating it).
  it("mounts the CreateOrganizationForm inline (organization-create-form)", async () => {
    harness.resolveJson([]);

    const Page = Route.options.component!;
    renderWithWallow(<Page />, { harness });

    await expect.element(page.getByTestId("organization-create-form")).toBeInTheDocument();
  });
});

describe("routes/dashboard/organizations (router registration)", () => {
  it("registers /dashboard/organizations in the router tree", () => {
    const router = getRouter();
    const paths = Object.keys(router.routesByPath);
    expect(paths).toContain("/dashboard/organizations");
  });
});
