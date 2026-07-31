import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { getRouter } from "@app/router";
import { Route } from "./register";

/**
 * The register-app route: page root, the mounted `RegisterAppForm`, and router
 * registration.
 */

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("routes/dashboard/apps/register (route page)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("renders a page root carrying data-testid=dashboard-apps-register", async () => {
    const Page = Route.options.component!;
    renderWithWallow(<Page />, { harness });

    await expect.element(page.getByTestId("dashboard-apps-register")).toBeInTheDocument();
  });

  it("mounts the RegisterAppForm (app-register-form)", async () => {
    const Page = Route.options.component!;
    renderWithWallow(<Page />, { harness });

    await expect.element(page.getByTestId("app-register-form")).toBeInTheDocument();
  });
});

describe("routes/dashboard/apps/register (router registration)", () => {
  it("registers /dashboard/apps/register in the router tree", () => {
    const router = getRouter();
    const paths = Object.keys(router.routesByPath);
    expect(paths).toContain("/dashboard/apps/register");
  });
});
