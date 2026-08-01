import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { waitForTestId } from "@bc-solutions-coder/testing/locators";
import { InquiryList } from "./InquiryList";

/**
 * Accessible names on the inquiry rows. Each row is an `<a>`, so its cells
 * compose a LINK's accessible name; the row leads with the contact's name,
 * which is what identifies it. Pinned against a later restyle that reduces a
 * cell to an icon.
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
