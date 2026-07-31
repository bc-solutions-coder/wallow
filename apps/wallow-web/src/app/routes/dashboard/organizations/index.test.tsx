import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { getRouter } from "@app/router";
import { Route } from "./index";

/**
 * The dashboard organizations list route: page root, title, prefetch loader, the
 * inline `CreateOrganizationForm`, and router registration.
 *
 * The page mounts `OrganizationList`, whose `useQuery` runs for real against the
 * harness transport — nothing in the path is stubbed.
 */

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
    await expect
      .element(page.getByTestId("organizations-header-title"))
      .toHaveTextContent("Organizations");
    // The create form mounts inline below the list, so there is no page-level
    // CTA — and `PageHeader` omits the actions slot rather than leaving an empty
    // flex child in the header row.
    expect(page.getByTestId("organizations-header-actions").elements()).toHaveLength(0);
  });

  // The create form mounts INLINE on this index page; there is no
  // `/dashboard/organizations/create` route.
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
