import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { routeHarness } from "@shared/testing/harness-routes";
import { beforeEach, describe, expect, it } from "vitest";

import {
  byTestId,
  expectClasses,
  expectPrecedes,
  expectTag,
  expectTokenColorsOnly,
  waitForTestId,
} from "@shared/testing/style-contract";
import { Route } from "./settings";

/**
 * Restyle spec for the settings page (Wallow-urec.4.4), following the worked
 * example in `routes/dashboard/apps/index.restyle.test.tsx`. Settings is
 * form-heavy rather than a table, so it takes the NARROW shell (`max-w-2xl`) and
 * leans on the `ui` Card sections rather than the list-card surface.
 *
 * Only page chrome is asserted here; the route's behaviour (loader, the
 * `dashboard-settings` root, and the fact that both sections mount inside it)
 * stays pinned by the sibling `settings.test.tsx`, which the restyle must not
 * edit. The page renders with both caches seeded so the profile and MFA sections
 * are on screen (not in their loading states) for the token-color scan.
 */

const PROFILE = {
  id: "u1",
  email: "ada@lovelace.io",
  firstName: "Ada",
  lastName: "Lovelace",
  roles: ["Owner"],
  permissions: [],
};

const MFA_STATUS = { enabled: false, method: null, backupCodeCount: 0 };

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/** Render the route page and resolve its settled root element. */
async function renderPage(): Promise<HTMLElement> {
  const Page = Route.options.component!;
  renderWithWallow(<Page />, { harness });
  return waitForTestId("dashboard-settings");
}

describe("routes/dashboard/settings (restyle)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    routeHarness(harness, {
      "GET /v1/identity/users/me": PROFILE,
      "GET /v1/identity/mfa/status": MFA_STATUS,
    });
  });

  it("constrains the settings page to the narrow shell", async () => {
    const root = await renderPage();

    expectClasses(root, "max-w-2xl mx-auto");
  });

  it("titles the page with an h1 reading Settings", async () => {
    await renderPage();

    const heading = byTestId("settings-heading");
    expectTag(heading, "h1");
    expect(heading.textContent).toBe("Settings");
    expectClasses(heading, "text-3xl font-bold text-foreground mb-8");
  });

  it("renders the heading above the profile section and the MFA section", async () => {
    await renderPage();

    expectPrecedes(byTestId("settings-heading"), byTestId("settings-profile-name"));
    expectPrecedes(byTestId("settings-profile-name"), byTestId("settings-mfa-status"));
  });

  it("styles the page with theme tokens only", async () => {
    const root = await renderPage();

    expectTokenColorsOnly(root);
  });
});
