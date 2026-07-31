import { computedColor, isTransparent } from "@bc-solutions-coder/testing/contrast";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import type { SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { createAuthHarness } from "@shared/testing/harness";
import { MfaChallengeForm } from "./MfaChallengeForm";

/**
 * The measured half of the MFA challenge screen's Button adoption
 * (Wallow-lrlm.7.1). The consent screen covers the solid/outline PAIR; this file
 * covers the axis that pair cannot exercise — `width`.
 *
 * The recipe's `width` defaults to `full`, because eleven pre-existing call
 * sites wanted it. This screen's "Use backup code instead" toggle is the app's
 * one control that must NOT be full width: it is a quiet inline affordance under
 * a full-width submit, and a migration that dropped it onto the default would
 * stretch it into a second button competing with Verify. That is a layout fact,
 * so it is measured rather than read off a `width="auto"` in the source — the
 * live class list is `twMerge(recipe, className)`, and either side can win.
 *
 * `returnUrl` is the OIDC hand-off shape every other spec in this directory
 * renders with; nothing here depends on it, but rendering the screen the way the
 * app does keeps this file from pinning a configuration the app never ships.
 */

const RETURN_URL = "/connect/authorize?client_id=wallow-web&scope=openid";

let harness: SdkHarness;

beforeEach(() => {
  harness = createAuthHarness();
});

/** Render the challenge form and wait for its controls to mount. */
async function renderForm(): Promise<void> {
  await renderWithWallow(<MfaChallengeForm returnUrl={RETURN_URL} />, { harness });

  await expect.element(page.getByTestId("mfa-challenge-toggle-backup")).toBeInTheDocument();
}

/** The computed value of `property` on the element carrying `testId`. */
function computed(testId: string, property: string): string {
  return globalThis.getComputedStyle(page.getByTestId(testId).element()).getPropertyValue(property);
}

/** The rendered box of the element carrying `testId`. */
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

    // The submit IS the full-width control, so it doubles as the measurement of
    // "the width the row offers" — and its own non-zero width is what stops this
    // from passing on a screen that rendered nothing.
    expect(submit.width, "the submit fills the form row").toBeGreaterThan(0);
    expect(toggle.width, "the toggle is sized to its label").toBeLessThan(submit.width);
  });

  it("paints no surface behind the mode toggle", async () => {
    // The quiet variants are told apart by what they do NOT draw at rest. A
    // toggle that arrived on the default `primary` would be a second solid
    // button under Verify, which is exactly what this screen must not grow.
    await renderForm();

    const toggle: Element = page.getByTestId("mfa-challenge-toggle-backup").element();

    expect(isTransparent(computedColor(toggle, "background-color"))).toBe(true);
  });

  it("still paints the submit on a real surface", async () => {
    // The vacuity guard for the assertion above: on a theme-less page EVERY
    // colour resolves to transparent, so "the toggle paints nothing" would hold
    // for a solid primary button too.
    await renderForm();

    const submit: Element = page.getByTestId("mfa-challenge-submit").element();

    expect(isTransparent(computedColor(submit, "background-color"))).toBe(false);
  });
});
