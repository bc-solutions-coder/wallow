import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { getRouter } from "@app/router";
import { Route } from "./register";

/**
 * Route spec for the register-app route (Wallow-ffpq.3.5) — the intended mount
 * point for the orphan `RegisterAppForm`. Mirrors
 * routes/dashboard/apps/index.test.tsx. Covers three contracts:
 *   1. The route page renders a root carrying `data-testid="dashboard-apps-
 *      register"` and mounts `RegisterAppForm` (its `app-register-form` testid).
 *   2. `src/router.tsx` registers the route under `/dashboard` at
 *      `/dashboard/apps/register` (bound manually alongside `apps`, no
 *      file-based codegen yet).
 *
 * RED note (list-route gotcha, Wallow-8w1h.5.2): "exposes a route component"
 * passes on the compile-safe stub because `createFileRoute` always defines a
 * component; the wrapper-render, form-mount, and router-registration assertions
 * fail until GREEN.
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
