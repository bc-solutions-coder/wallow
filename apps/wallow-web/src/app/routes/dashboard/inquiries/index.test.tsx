import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { getRouter } from "@app/router";
import { Route } from "./index";

/**
 * The dashboard inquiries list route: page root, title, prefetch loader, the
 * inline `CreateInquiryForm`, and router registration.
 *
 * The page mounts `InquiryList`, whose `useQuery` runs for real against the
 * harness transport — nothing in the path is stubbed.
 */

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
    await expect.element(page.getByTestId("inquiries-header-title")).toHaveTextContent("Inquiries");
  });

  // The create form mounts INLINE — list and create share this page, and there
  // is no standalone create route.
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
