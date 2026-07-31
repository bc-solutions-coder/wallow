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
 * Page chrome for the settings page: the shared container, the header row, and
 * the order of the profile and MFA sections.
 *
 * Both reads are answered so the sections are on screen rather than in their
 * loading states for the token-colour scan.
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

    // `PageHeader`'s derived testid. The `mb-8` sits on the header ROW, which is
    // where page rhythm belongs when a title can grow an actions slot.
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
