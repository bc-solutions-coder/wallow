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
 * Accessible names and message association for the register-app branding
 * subsection.
 *
 * A placeholder is the last-resort fallback in the accessible-name computation,
 * so `toHaveAccessibleName()` alone passes for a placeholder-only control. The
 * two text inputs are asserted on the ASSOCIATION instead — `<label>`,
 * `aria-label` or `aria-labelledby` — which is what survives a filled-in field.
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
 * it — which a `placeholder` does not.
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

    await expect
      .element(page.getByTestId("app-branding-display-name"))
      .toHaveAccessibleName(/display name/i);
    await expect.element(page.getByTestId("app-branding-tagline")).toHaveAccessibleName(/tagline/i);
  });

  it("associates the conditional-required message with the branding display-name input", async () => {
    // The endpoint rejects a blank `DisplayName`, so a tagline or logo without
    // one cannot be sent.
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

    expect(harness.calls.filter((call: SdkCall) => call.path.endsWith("/branding"))).toHaveLength(
      0,
    );
  });
});
