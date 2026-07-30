import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { getRouter } from "../../../router";
import { Route } from "./index";

/**
 * Route spec for the dashboard inquiries list route (Wallow-8w1h.7.2), mirroring
 * routes/dashboard/organizations/index.test.tsx. Covers two contracts:
 *   1. The route page renders a root carrying `data-testid="dashboard-inquiries"`
 *      and prefetches via a `loader`.
 *   2. `src/router.tsx` registers the route under the root at
 *      `/dashboard/inquiries` (bound manually alongside the other dashboard
 *      routes, no layout route yet).
 *
 * RED note (list-route gotcha, Wallow-8w1h.5.2): "exposes a route component"
 * passes on the compile-safe stub because `createFileRoute` always defines a
 * component; the other three assertions (loader, dashboard-inquiries render,
 * router registration) fail until GREEN.
 */

// The rendered page mounts InquiryList, whose `useQuery` now runs for real
// against the harness transport (Wallow-pu6a.5.5) — the facade this spec used to
// mock is deleted, and there is nothing left in the path to stub.

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("routes/dashboard/inquiries (route page)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("prefetches the inquiry list via a loader", () => {
    expect(Route.options.loader).toBeDefined();
  });

  it("renders a page root carrying data-testid=dashboard-inquiries", async () => {
    harness.resolveJson([]);

    const Page = Route.options.component!;
    renderWithWallow(<Page />, { harness });

    await expect.element(page.getByTestId("dashboard-inquiries")).toBeInTheDocument();
  });

  // Wallow-ffpq.3.5 — the orphan CreateInquiryForm mounts INLINE on this index
  // page (list + create on the SAME page), NOT a standalone route.
  it("mounts the CreateInquiryForm inline (inquiry-create-form)", async () => {
    harness.resolveJson([]);

    const Page = Route.options.component!;
    renderWithWallow(<Page />, { harness });

    await expect.element(page.getByTestId("inquiry-create-form")).toBeInTheDocument();
  });
});

describe("routes/dashboard/inquiries (router registration)", () => {
  it("registers /dashboard/inquiries in the router tree", () => {
    const router = getRouter();
    const paths = Object.keys(router.routesByPath);
    expect(paths).toContain("/dashboard/inquiries");
  });
});
