import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { getRouter } from "@app/router";
import { routeHarness } from "@shared/testing/harness-routes";
import { Route } from "./settings";

/**
 * Route spec for the dashboard settings route (Wallow-8w1h.6.5), mirroring
 * routes/dashboard/apps/index.test.tsx. Covers three contracts:
 *   1. The route page renders a root carrying `data-testid="dashboard-settings"`
 *      and prefetches both queries via a `loader`.
 *   2. Both composed sections render inside that root — the profile section
 *      (`settings-profile-*`) and the MFA status card (`settings-mfa-*`).
 *   3. `src/router.tsx` registers the route under the root at
 *      `/dashboard/settings` (bound manually, no dashboard layout route yet).
 */

// The rendered page mounts ProfileSection (the current-user read) and
// MfaSettingsSection (the MFA status read plus its disable/regenerate mutations).
// Both now run for real against the harness transport (Wallow-pu6a.5.5) — the
// facade this spec used to mock is deleted — so the sections are driven by
// ANSWERING their two requests rather than by seeding their caches.

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
