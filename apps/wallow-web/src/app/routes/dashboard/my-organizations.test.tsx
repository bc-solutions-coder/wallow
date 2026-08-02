import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { getRouter } from "@app/router";
import { Route } from "./my-organizations";

/**
 * The dashboard "my organizations" route (Wallow-yp3e.7): page root, title,
 * prefetch loader, and router registration.
 *
 * The page mounts `MyOrganizations`, whose `useQuery` runs for real against the
 * harness transport — nothing in the path is stubbed.
 */

let harness: SdkHarness;

describe("routes/dashboard/my-organizations (route page)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("prefetches the memberships list via a loader", () => {
    expect(Route.options.loader).toBeDefined();
  });

  it("renders a page root carrying data-testid=dashboard-my-organizations", async () => {
    harness.resolveJson([]);

    const Page = Route.options.component!;
    renderWithWallow(<Page />, { harness });

    await expect.element(page.getByTestId("dashboard-my-organizations")).toBeInTheDocument();
    await expect
      .element(page.getByTestId("my-organizations-heading-title"))
      .toHaveTextContent("My organizations");
  });

  it("mounts MyOrganizations (my-organizations-table)", async () => {
    harness.resolveJson([{ organizationId: "o1", name: "Acme", slug: "acme", isOwner: true }]);

    const Page = Route.options.component!;
    renderWithWallow(<Page />, { harness });

    await expect.element(page.getByTestId("my-organizations-table")).toBeInTheDocument();
  });
});

describe("routes/dashboard/my-organizations (router registration)", () => {
  it("registers /dashboard/my-organizations in the router tree", () => {
    const router = getRouter();
    const paths = Object.keys(router.routesByPath);
    expect(paths).toContain("/dashboard/my-organizations");
  });
});
