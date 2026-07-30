import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { routeHarness } from "@shared/testing/harness-routes";
import { InquiryDetail } from "./InquiryDetail";

/**
 * Accessible-name spec for the inquiry-detail page (Wallow-lrlm.4.4).
 *
 * The add-comment control is the BARE `<textarea>` the bead names: it carries a
 * `data-testid` and a copy of the `Input` recipe's classes, but no `Label`, no
 * wrapping `Field`, and no `aria-label` — so it reaches the accessibility tree
 * with NO accessible name at all. A screen reader announces it as an unlabelled
 * multi-line edit field, and the only clue to what it collects is the "Add
 * comment" button underneath it, which names the SUBMIT, not the control.
 *
 * The fix is the association the sibling MFA screens already ship
 * (`features/mfa/components/MfaEnrollFlow.tsx`'s `<Label htmlFor>` + `<Input
 * id>` inside a `Field`) or the equivalent `aria-label`; this spec pins the
 * OUTCOME — a non-empty accessible name — rather than either mechanism, so the
 * implementation is free to pick whichever the catalog makes cleanest.
 *
 * `toHaveAccessibleName()` with no argument is the AC's exact claim ("a
 * non-empty accessible name"), computed off the real accessibility tree in
 * headless Chromium rather than off the markup, so a `Label` that is present but
 * NOT associated still fails.
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

    // The heading settles first: the form only exists once the detail read
    // resolves, so asserting on the control before that would race the render.
    await expect.element(page.getByTestId("inquiry-detail-heading")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-comment-content")).toHaveAccessibleName();
  });

  it("keeps the add-comment textarea reachable by its label", async () => {
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await expect.element(page.getByTestId("inquiry-detail-heading")).toBeInTheDocument();

    // The complement of the assertion above, from the USER's side: whatever
    // names the control must also make it findable by that name, which is what
    // an accessible name is for. `textbox` is the role a `textarea` maps to.
    const named = page.getByTestId("inquiry-comment-content").element();
    const byRole = page.getByRole("textbox", { name: /comment/i }).elements();

    expect(byRole).toContain(named);
  });

  it("names the status select after the choice it governs", async () => {
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await expect.element(page.getByTestId("inquiry-detail-heading")).toBeInTheDocument();

    // The select trigger is UNNAMED today, not merely vaguely named. It renders
    // as `<button role="combobox">New</button>`, and `combobox` does not take
    // its name from its contents — the chosen option is the control's VALUE, and
    // an author-supplied name (`aria-label`/`aria-labelledby`/a label element) is
    // the only thing that names it. So the trigger reaches the accessibility
    // tree with no name at all. The sibling
    // `organization-detail-register-client-type` trigger has the identical gap:
    // both come from `shared/components/SelectControl.tsx`, which accepts no
    // accessible-name prop.
    await expect.element(page.getByTestId("inquiry-status-select")).toHaveAccessibleName(/status/i);
  });
});
