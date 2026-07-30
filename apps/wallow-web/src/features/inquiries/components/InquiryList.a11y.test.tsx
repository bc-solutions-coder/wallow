import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { waitForTestId } from "@shared/testing/style-contract";
import { InquiryList } from "./InquiryList";

/**
 * Accessible-name REGRESSION GUARD for the inquiry rows (Wallow-lrlm.4.4) — the
 * sibling of `organizations/components/OrganizationList.a11y.test.tsx`, kept
 * beside the component it guards rather than shared, because the two lists carry
 * different cells.
 *
 * Wallow-lrlm.4.1 made each row an `<a>`, so the row's cells now compose a
 * LINK's accessible name. The inquiry row leads with the contact's name, so that
 * name is non-empty and identifies the row; this pins it against a later
 * restyle that reduces a cell to an icon.
 *
 * Like its sibling, this file asserts a property the app ALREADY has.
 */

const INQUIRIES = [
  { id: "i1", name: "Ada Lovelace", company: "Analytical Engines", status: "New" },
  { id: "i2", name: "Alan Turing", company: null, status: "Closed" },
];

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("InquiryList — row accessible names", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson(INQUIRIES);
  });

  it("gives every row link a non-empty accessible name", async () => {
    renderWithWallow(<InquiryList />, { harness });
    await waitForTestId("inquiries-table");

    const rows = page.getByTestId("inquiry-item");
    await expect.element(rows.first()).toHaveAccessibleName();
    await expect.element(rows.last()).toHaveAccessibleName();
  });

  it("names each row link after its own inquiry", async () => {
    renderWithWallow(<InquiryList />, { harness });
    await waitForTestId("inquiries-table");

    await expect
      .element(page.getByTestId("inquiry-item").first())
      .toHaveAccessibleName(/Ada Lovelace/);
    await expect
      .element(page.getByTestId("inquiry-item").last())
      .toHaveAccessibleName(/Alan Turing/);
  });
});
