import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { routeHarness } from "@shared/testing/harness-routes";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { chooseOption, expectCatalogSelect } from "@shared/testing/catalog-select";
import { byTestId, waitForTestId } from "@shared/testing/locators";
import { InquiryDetail } from "./InquiryDetail";
import { INQUIRY_STATUSES } from "../statuses";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * The two catalog controls on the inquiry-detail page:
 * `inquiry-status-select` (a `Select`) and `inquiry-comment-internal` (a
 * `Checkbox`). Each testid names the component's interactive part — the
 * Select's trigger, the Checkbox's root — because that is the element the E2E
 * suite and the sibling behaviour specs click.
 *
 * These cases assert the a11y contract each primitive brings — an explicit
 * `role`, a self-reporting `aria-` state, a listbox genuinely absent until
 * opened — and NOT which Base UI parts the component composes.
 */

const inquiry = {
  id: "i1",
  name: "Ada Lovelace",
  email: "ada@example.com",
  company: "Analytical Engines",
  projectType: "web-app",
  status: "New",
  createdAt: "2026-07-15T00:00:00Z",
};

/** Render the detail with its inquiry and (empty) thread already in cache. */
async function renderDetail(): Promise<void> {
  routeHarness(harness, {
    "GET /v1/inquiries/i1": inquiry,
    "GET /v1/inquiries/i1/comments": [],
  });

  renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
  await waitForTestId("inquiry-detail-heading");
}

beforeEach(() => {
  harness = createSdkHarness();
});

describe("InquiryDetail status control (catalog Select)", () => {
  it("presents the status control as a combobox rather than a native select", async () => {
    await renderDetail();

    expectCatalogSelect("inquiry-status-select");
  });

  it("keeps every inquiry status reachable as a named option", async () => {
    await renderDetail();

    await userEvent.click(byTestId("inquiry-status-select"));

    // The domain's four statuses are options in a portalled listbox, not
    // `<option>` children.
    for (const status of INQUIRY_STATUSES) {
      await expect.element(page.getByRole("option", { name: status, exact: true })).toBeVisible();
    }
  });

  it("reports the chosen status on the trigger", async () => {
    await renderDetail();

    await chooseOption("inquiry-status-select", "Contacted");

    await expect.element(page.getByTestId("inquiry-status-select")).toHaveTextContent("Contacted");
  });

  it("closes the listbox once a status is chosen", async () => {
    await renderDetail();

    await chooseOption("inquiry-status-select", "Reviewed");

    // Absent, not hidden: the popup is portalled and unmounted, so nothing of
    // the list survives the choice.
    await expect.element(page.getByRole("listbox")).not.toBeInTheDocument();
  });
});

describe("InquiryDetail internal-note flag (catalog Checkbox)", () => {
  it("presents the internal flag as a checkbox that reports its own state", async () => {
    await renderDetail();

    const flag: HTMLElement = byTestId("inquiry-comment-internal");

    // The catalog `Checkbox` states role and checked state explicitly, which is
    // what makes them readable without inspecting `.checked`.
    expect(flag.getAttribute("role")).toBe("checkbox");
    expect(flag.getAttribute("aria-checked")).toBe("false");
  });

  it("flips its reported state when activated", async () => {
    await renderDetail();

    await userEvent.click(byTestId("inquiry-comment-internal"));

    await expect
      .element(page.getByTestId("inquiry-comment-internal"))
      .toHaveAttribute("aria-checked", "true");
  });
});
