import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { routeHarness } from "@shared/testing/harness-routes";
import { beforeEach, describe, expect, it } from "vitest";

import {
  byTestId,
  expectClasses,
  expectPageContainer,
  expectPrecedes,
  expectTag,
  expectTokenColorsOnly,
  waitForTestId,
} from "@shared/testing/style-contract";
import { Route } from "./settings";

/**
 * Restyle spec for the settings page (Wallow-urec.4.4), following the worked
 * example in `routes/dashboard/apps/index.restyle.test.tsx`. Settings leans on
 * the `ui` Card sections rather than the list-card surface.
 *
 * Wallow-lrlm.5.1 retired the NARROW/WIDE shell split this page used to opt into
 * (`max-w-2xl` against the list pages' `max-w-5xl`). Every dashboard page now
 * takes the one shared `PAGE_CONTAINER`, so the width is no longer a per-page
 * decision and this spec no longer names one.
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

  it("centers the settings page in the shared dashboard container", async () => {
    const root = await renderPage();

    expectPageContainer(root);
  });

  it("titles the page with an h1 reading Settings", async () => {
    await renderPage();

    // Wallow-lrlm.5.1: `PageHeader`'s derived testid and `Text`'s title scale.
    // The `mb-8` the hand-rolled `<h1>` carried moved to the header ROW, which
    // is where the page rhythm belongs when a title can grow an actions slot.
    const heading = byTestId("settings-header-title");
    expectTag(heading, "h1");
    expect(heading.textContent).toBe("Settings");
    expectClasses(heading, "text-3xl font-bold tracking-tight text-foreground");
    expectClasses(byTestId("settings-header"), "flex items-start justify-between gap-4 mb-8");
  });

  it("renders the heading above the profile section and the MFA section", async () => {
    await renderPage();

    expectPrecedes(byTestId("settings-header-title"), byTestId("settings-profile-name"));
    expectPrecedes(byTestId("settings-profile-name"), byTestId("settings-mfa-status"));
  });

  it("styles the page with theme tokens only", async () => {
    const root = await renderPage();

    expectTokenColorsOnly(root);
  });
});
