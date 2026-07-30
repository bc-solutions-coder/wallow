import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { waitForTestId } from "@shared/testing/style-contract";
import { OrganizationList } from "./OrganizationList";

/**
 * Accessible-name REGRESSION GUARD for the list rows (Wallow-lrlm.4.4).
 *
 * Wallow-lrlm.4.1 turned every row from an inert `<li>` into an `<a>` (the
 * catalog `ListRow`'s `render` substitutes the element). That promoted the row's
 * cells from loose text into a LINK's accessible name, which is computed from
 * its contents — so a row with no text, or one whose only text is an icon, would
 * have become an unnamed link.
 *
 * It did not: each row still renders the org name (plus an optional domain and
 * the member-count badge), so the name is non-empty and leads with the thing the
 * link is about. These cases exist so that stays true — a later restyle that
 * swaps a cell for an icon has to notice.
 *
 * Unlike the sibling `*.a11y.test.tsx` specs in this feature, this file asserts
 * a property the app ALREADY has.
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

    // A row that navigates to one org must be distinguishable from the row
    // beside it by name alone — "link, link" is what an icon-only row would
    // announce.
    await expect
      .element(page.getByTestId("organization-item").first())
      .toHaveAccessibleName(/Acme/);
    await expect
      .element(page.getByTestId("organization-item").last())
      .toHaveAccessibleName(/Globex/);
  });
});
