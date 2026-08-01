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
 * profile and MFA sections, and router registration.
 *
 * `ProfileSection` and `MfaSettingsSection` run for real against the harness
 * transport, so they are driven by ANSWERING their two reads rather than by
 * seeding their caches.
 */

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/** Answer both sections' reads so they render content, not their loading states. */
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
});

describe("routes/dashboard/settings (router registration)", () => {
  it("registers /dashboard/settings in the router tree", () => {
    const router = getRouter();
    const paths = Object.keys(router.routesByPath);
    expect(paths).toContain("/dashboard/settings");
  });
});
