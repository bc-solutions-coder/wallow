import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { RegisterAppForm } from "./RegisterAppForm";

/**
 * Accessible-name spec for the register-app BRANDING subsection
 * (Wallow-lrlm.4.4).
 *
 * The four catalog fields above it are already correct — they went through
 * `@bc-solutions-coder/forms`, which renders a `Field.Label` bound to each
 * control. `BrandingSection` did not: it is the hand-rolled, uncontrolled block
 * at the bottom of the form, and all three of its controls are unnamed.
 *
 *   - `app-logo-input` — a fully bare `<input type="file">`. No label, no
 *     `aria-label`, not even a placeholder: it reaches the accessibility tree
 *     with NO accessible name at all.
 *   - `app-branding-display-name` / `app-branding-tagline` — `Input`s inside a
 *     `Field` with no `Field.Label`, carrying `placeholder="Display name"` /
 *     `placeholder="Tagline"`.
 *
 * The placeholder pair needs a sharper assertion than the file input does.
 * A placeholder IS the last-resort fallback in the accessible-name computation,
 * so `toHaveAccessibleName()` alone already passes for those two and would not
 * see the gap — while the name still vanishes the moment a user types, is not
 * announced by every AT, and is not a label by WCAG. So they are asserted on the
 * ASSOCIATION: a `<label>` (`input.labels`), an `aria-label`, or an
 * `aria-labelledby` — the three things that survive a filled-in field.
 *
 * The `<legend>Branding (optional)</legend>` above them names the FIELDSET, not
 * the controls inside it, so it does not close this gap either.
 */

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * The `<input>` behind a testid, narrowed rather than cast so a testid that
 * moves onto a wrapper fails loudly instead of reading `undefined.labels`.
 */
function inputAt(testId: string): HTMLInputElement {
  const element: Element = page.getByTestId(testId).element();
  if (!(element instanceof HTMLInputElement)) {
    throw new TypeError(`${testId} is not an <input> element`);
  }
  return element;
}

/**
 * Whether the control carries a label that OUTLIVES a value being typed into
 * it — which a `placeholder`, the only thing naming two of these three controls
 * today, does not.
 */
function hasProgrammaticLabel(input: HTMLInputElement): boolean {
  return (
    (input.labels?.length ?? 0) > 0 ||
    input.getAttribute("aria-label") !== null ||
    input.getAttribute("aria-labelledby") !== null
  );
}

describe("RegisterAppForm — branding accessible names", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("gives the branding logo file input a non-empty accessible name", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });

    await expect.element(page.getByTestId("app-logo-input")).toHaveAccessibleName();
  });

  it("names the branding logo input after what it uploads", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });

    await expect.element(page.getByTestId("app-logo-input")).toHaveAccessibleName(/logo/i);
  });

  it("labels the branding display-name input with more than a placeholder", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });
    await expect.element(page.getByTestId("app-branding-display-name")).toBeInTheDocument();

    expect(hasProgrammaticLabel(inputAt("app-branding-display-name"))).toBe(true);
  });

  it("labels the branding tagline input with more than a placeholder", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });
    await expect.element(page.getByTestId("app-branding-tagline")).toBeInTheDocument();

    expect(hasProgrammaticLabel(inputAt("app-branding-tagline"))).toBe(true);
  });

  it("keeps the branding controls named after what they collect", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });
    await expect.element(page.getByTestId("app-branding-display-name")).toBeInTheDocument();

    // The names themselves are already right (the placeholders say so); this
    // pins them so the fix moves the wording onto a real label rather than
    // replacing it.
    await expect
      .element(page.getByTestId("app-branding-display-name"))
      .toHaveAccessibleName(/display name/i);
    await expect.element(page.getByTestId("app-branding-tagline")).toHaveAccessibleName(/tagline/i);
  });
});
