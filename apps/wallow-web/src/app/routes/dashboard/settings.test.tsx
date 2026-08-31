import {
  createSdkHarness,
  routeHarness,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { getRouter } from "@app/router";
import { Route } from "./settings";

/**
 * The dashboard settings route: page root, title, prefetch loader, the composed
 * profile, MFA and connected-applications sections, and router registration.
 *
 * The sections run for real against the harness transport, so they are driven
 * by ANSWERING their reads rather than by seeding their caches.
 */

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/** Answer every section's read so they render content, not their loading states. */
function renderSettings() {
  routeHarness(harness, {
    "GET /v1/identity/users/me": {
      id: "user-1",
      email: "ada@lovelace.io",
      firstName: "Ada",
      lastName: "Lovelace",
      roles: ["Owner"],
      permissions: [],
    },
    "GET /v1/identity/mfa/status": {
      enabled: false,
      method: null,
      backupCodeCount: 0,
    },
    "GET /v1/identity/me/authorizations": [],
  });

  const Page = Route.options.component!;
  return renderWithWallow(<Page />, { harness });
}

describe("routes/dashboard/settings (route page)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("prefetches profile + mfa status via a loader", () => {
    expect(Route.options.loader).toBeDefined();
  });

  it("renders a page root carrying data-testid=dashboard-settings", async () => {
    renderSettings();

    await expect.element(page.getByTestId("dashboard-settings")).toBeInTheDocument();
    await expect.element(page.getByTestId("settings-header-title")).toHaveTextContent("Settings");
  });

  it("composes the profile section inside the dashboard-settings root", async () => {
    renderSettings();

    const root = page.getByTestId("dashboard-settings");
    await expect
      .element(root.getByTestId("settings-profile-name"))
      .toHaveTextContent("Ada Lovelace");
    await expect
      .element(root.getByTestId("settings-profile-email"))
      .toHaveTextContent("ada@lovelace.io");
  });

  it("composes the mfa status card inside the dashboard-settings root", async () => {
    renderSettings();

    const root = page.getByTestId("dashboard-settings");
    await expect.element(root.getByTestId("settings-mfa-status")).toHaveTextContent("Disabled");
  });

  it("composes the connected-applications card inside the dashboard-settings root", async () => {
    renderSettings();

    const root = page.getByTestId("dashboard-settings");
    await expect
      .element(root.getByTestId("connected-apps-empty"))
      .toHaveTextContent("No connected applications.");
  });
});

describe("routes/dashboard/settings (router registration)", () => {
  it("registers /dashboard/settings in the router tree", () => {
    const router = getRouter();
    const paths = Object.keys(router.routesByPath);
    expect(paths).toContain("/dashboard/settings");
  });
});
