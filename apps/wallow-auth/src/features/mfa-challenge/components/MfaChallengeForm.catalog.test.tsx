import { computedColor, isTransparent } from "@bc-solutions-coder/testing/contrast";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { createPassthroughHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { MfaChallengeForm } from "./MfaChallengeForm";

/**
 * The MFA challenge screen's Button controls, measured in a real browser.
 *
 * The Button recipe's `width` defaults to `full`. This screen's "Use backup code instead"
 * toggle is the one control that must NOT be: a full-width toggle under a full-width submit
 * reads as a second button competing with Verify. Measured rather than read off a
 * `width="auto"` in the source — the live class list is `twMerge(recipe, className)`, and
 * either side can win.
 */

const RETURN_URL = "/connect/authorize?client_id=wallow-web&scope=openid";

let harness: SdkHarness;

beforeEach(() => {
  harness = createPassthroughHarness();
});

async function renderForm(): Promise<void> {
  await renderWithWallow(<MfaChallengeForm returnUrl={RETURN_URL} />, { harness });

  await expect.element(page.getByTestId("mfa-challenge-toggle-backup")).toBeInTheDocument();
}

function computed(testId: string, property: string): string {
  return globalThis.getComputedStyle(page.getByTestId(testId).element()).getPropertyValue(property);
}

function box(testId: string): DOMRect {
  return page.getByTestId(testId).element().getBoundingClientRect();
}

describe("the MFA challenge screen's controls come from the catalog", () => {
  it("gives the mode toggle the recipe's centred flex box", async () => {
    await renderForm();

    expect(computed("mfa-challenge-toggle-backup", "display")).toBe("inline-flex");
    expect(computed("mfa-challenge-toggle-backup", "justify-content")).toBe("center");
  });

  it("keeps the mode toggle off the full-width default", async () => {
    await renderForm();

    const toggle: DOMRect = box("mfa-challenge-toggle-backup");
    const submit: DOMRect = box("mfa-challenge-submit");

    // The submit IS the full-width control, so it doubles as the measurement of "the width
    // the row offers", and its own non-zero width stops this passing on a blank screen.
    expect(submit.width, "the submit fills the form row").toBeGreaterThan(0);
    expect(toggle.width, "the toggle is sized to its label").toBeLessThan(submit.width);
  });

  it("paints no surface behind the mode toggle", async () => {
    // The quiet variants are told apart by what they do NOT draw at rest.
    await renderForm();

    const toggle: Element = page.getByTestId("mfa-challenge-toggle-backup").element();

    expect(isTransparent(computedColor(toggle, "background-color"))).toBe(true);
  });

  it("still paints the submit on a real surface", async () => {
    // The vacuity guard for the assertion above: on a theme-less page EVERY colour resolves
    // to transparent, so "the toggle paints nothing" would hold for a solid button too.
    await renderForm();

    const submit: Element = page.getByTestId("mfa-challenge-submit").element();

    expect(isTransparent(computedColor(submit, "background-color"))).toBe(false);
  });
});
