import {
  createSdkHarness,
  type SdkCall,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
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
 *
 * WHAT WALLOW-LRLM.6.2 ADDS. The block stops being uncontrolled: the two text
 * controls become catalog `TextField`s (keeping their ids via an explicit
 * `testId` override) so the fix moves the naming onto the ui `Field` row every
 * other field on this form already uses, and the five assertions above go on
 * passing through it. Becoming real fields brings a message with them, and a
 * MESSAGE has the same association problem a label does — so the sixth case
 * asserts the one the uncontrolled block could never have had: the branding
 * display name is conditionally required (the endpoint 400s on a blank
 * `DisplayName`, so a tagline or logo without one cannot be sent), and that
 * message must be pointed at by the control's `aria-describedby` with
 * `aria-invalid` set, not merely rendered somewhere nearby.
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

/**
 * The ids `control` points its `aria-describedby` at. Split rather than compared
 * whole: Base UI appends the message to whatever else already describes the
 * control, so the claim is that it is AMONG them, not that it is alone.
 */
function describedByIds(control: HTMLElement): readonly string[] {
  return (control.getAttribute("aria-describedby") ?? "")
    .split(" ")
    .filter((id: string) => id !== "");
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

  it("associates the conditional-required message with the branding display-name input", async () => {
    // A tagline with no display name cannot be upserted — the endpoint rejects a
    // blank `DisplayName` — so the form has to say so, ON the control that is
    // missing rather than in a banner the user has to connect up by eye.
    renderWithWallow(<RegisterAppForm />, { harness });

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.type(page.getByTestId("app-branding-tagline"), "Ship faster");
    await userEvent.click(page.getByTestId("app-register-submit"));

    const message = page.getByTestId("app-branding-display-name-error");
    await expect.element(message).toBeInTheDocument();

    const messageId: string = message.element().id;
    expect(messageId).not.toBe("");

    const control: HTMLInputElement = inputAt("app-branding-display-name");
    expect(describedByIds(control)).toContain(messageId);
    expect(control.getAttribute("aria-invalid")).toBe("true");

    // The message is the point, but it is only honest if the request it stands
    // in for really did not go out.
    expect(harness.calls.filter((call: SdkCall) => call.path.endsWith("/branding"))).toHaveLength(
      0,
    );
  });
});
