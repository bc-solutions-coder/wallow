import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { waitForTestId } from "@shared/testing/locators";
import { OrganizationList } from "./OrganizationList";

/**
 * Accessible names of the list rows.
 *
 * Each row is an `<a>`, so its accessible name is computed from its contents.
 * A row whose cells became an icon would announce as an unnamed link; these
 * cases exist so a later restyle has to notice.
 */

const ORGS = [
  { id: "o1", name: "Acme", domain: "acme.io", memberCount: "3" },
  { id: "o2", name: "Globex", domain: null, memberCount: "1" },
];

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("OrganizationList — row accessible names", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson(ORGS);
  });

  it("gives every row link a non-empty accessible name", async () => {
    renderWithWallow(<OrganizationList />, { harness });
    await waitForTestId("organizations-table");

    const rows = page.getByTestId("organization-item");
    await expect.element(rows.first()).toHaveAccessibleName();
    await expect.element(rows.last()).toHaveAccessibleName();
  });

  it("names each row link after its own organization", async () => {
    renderWithWallow(<OrganizationList />, { harness });
    await waitForTestId("organizations-table");

    // Each row must be distinguishable from the one beside it by name alone.
    await expect
      .element(page.getByTestId("organization-item").first())
      .toHaveAccessibleName(/Acme/);
    await expect
      .element(page.getByTestId("organization-item").last())
      .toHaveAccessibleName(/Globex/);
  });
});
