import {
  createSdkHarness,
  routeHarness,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { InquiryDetail } from "./InquiryDetail";

/**
 * Accessible names on the inquiry-detail page: the add-comment textarea and the
 * status select.
 *
 * `toHaveAccessibleName()` is computed off the real accessibility tree in
 * headless Chromium, not off the markup, so a `Label` that is present but NOT
 * associated still fails. These cases pin the OUTCOME, leaving the
 * implementation free to name the control however the catalog makes cleanest.
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

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/** Answer the detail + comments reads so the form under test is on screen. */
function seedLoaded(): void {
  routeHarness(
    harness,
    {
      "GET /v1/inquiries/i1": inquiry,
      "GET /v1/inquiries/i1/comments": [],
    },
    { fallback: [] },
  );
}

describe("InquiryDetail — accessible names", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("gives the add-comment textarea a non-empty accessible name", async () => {
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });

    // The form only exists once the detail read resolves, so the heading has to
    // settle first or the assertion races the render.
    await expect.element(page.getByTestId("inquiry-detail-heading")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-comment-content")).toHaveAccessibleName();
  });

  it("keeps the add-comment textarea reachable by its label", async () => {
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await expect.element(page.getByTestId("inquiry-detail-heading")).toBeInTheDocument();

    // The same claim from the USER's side: whatever names the control must also
    // make it findable by that name. `textbox` is the role a `textarea` maps to.
    const named = page.getByTestId("inquiry-comment-content").element();
    const byRole = page.getByRole("textbox", { name: /comment/i }).elements();

    expect(byRole).toContain(named);
  });

  it("names the status select after the choice it governs", async () => {
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await expect.element(page.getByTestId("inquiry-detail-heading")).toBeInTheDocument();

    // The trigger renders as `<button role="combobox">New</button>`, and
    // `combobox` does not take its name from its contents — the chosen option is
    // the control's VALUE. An author-supplied name (`aria-label`,
    // `aria-labelledby`, or a label element) is the only thing that names it.
    await expect.element(page.getByTestId("inquiry-status-select")).toHaveAccessibleName(/status/i);
  });
});
