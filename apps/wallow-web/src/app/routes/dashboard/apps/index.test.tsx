import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { getRouter } from "@app/router";
import { Route } from "./index";

/**
 * The dashboard apps list route: page root, title, prefetch loader, register
 * link, and router registration.
 *
 * The page mounts `AppList`, whose `useQuery` runs for real against the harness
 * transport — nothing in the path is stubbed.
 */

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
    // `PageHeader` DERIVES the title's testid from the header's: `apps-header`
    // -> `apps-header-title`.
    await expect.element(page.getByTestId("apps-header-title")).toHaveTextContent("My Apps");
  });

  it("links to the register route (apps-register-link)", async () => {
    harness.resolveJson([]);

    const Page = Route.options.component!;
    renderWithWallow(<Page />, { harness });

    const link = page.getByTestId("apps-register-link");
    await expect.element(link).toHaveTextContent("Register New App");
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
