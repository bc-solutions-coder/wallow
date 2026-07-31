import { computedColor, isTransparent, type Rgba } from "@bc-solutions-coder/testing/contrast";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import type { SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { createAuthHarness } from "@shared/testing/harness";
import { ConsentScreen } from "./ConsentScreen";

/**
 * The MEASURED half of wallow-auth's catalog adoption (Wallow-lrlm.7.1), taken
 * on the consent screen because it is the one screen carrying both halves of the
 * migration in one card: a hand-rolled `<h2>` heading and a hand-rolled pair of
 * `<button>`s (approve/deny) whose class strings are a copy of the `Button`
 * recipe.
 *
 * WHY MEASURED. `catalog-adoption.test.ts` can prove the source names `Text` and
 * `Button` and names a variant. It cannot prove what the browser PAINTS: the
 * live class list is the `twMerge` of the catalog recipe with whatever
 * `className` the call site passes, so a caller utility can quietly win an axis
 * the recipe thought it owned. Every claim about a rendered box or colour is
 * therefore read off `getComputedStyle` here.
 *
 * WHY PROBES RATHER THAN LITERALS. Asserting `font-size: 20px` or an `rgb()`
 * string would pin Tailwind's and `api/branding.json`'s current values into this
 * app's spec. Instead each assertion renders a sibling element carrying the
 * utility the migration is supposed to land on and compares the two computed
 * values, so the spec keeps meaning what it says after a fork retunes its scale
 * or palette. The probes double as the vacuity guard: this project loads the
 * real Tailwind pipeline and the fork theme (Wallow-8ytl), and the assertions
 * below check the probes actually resolved rather than trusting that they did.
 */

const CLIENT_ID = "wallow-web";
const RETURN_URL = "/connect/authorize?client_id=wallow-web&scope=openid";
const SCOPE = "openid profile";

/** A `ConsentInfoResponse` with a long enough title to exercise the card. */
function consentInfo() {
  return {
    clientId: CLIENT_ID,
    displayName: "Wallow Web",
    logoUrl: null,
    requestedScopes: [
      { name: "openid", description: "Sign you in" },
      { name: "profile", description: "See your profile" },
    ],
  };
}

/**
 * The utilities each assertion measures against, rendered beside the screen.
 *
 * `text-base` is `Text`'s `body` step — the app-wide card-heading scale settled
 * on by Wallow-lrlm.13 — and `text-lg`/`text-xl` are the two steps wallow-auth's
 * headings used to split across before it. All three are rendered so the spec
 * can prove they are DISTINGUISHABLE before claiming the heading matches one of
 * them.
 */
function StyleProbes() {
  return (
    <div data-testid="probes">
      <div data-testid="probe-base" className="text-base">
        probe
      </div>
      <div data-testid="probe-lg" className="text-lg">
        probe
      </div>
      <div data-testid="probe-xl" className="text-xl">
        probe
      </div>
      <div data-testid="probe-primary" className="bg-primary">
        probe
      </div>
      <div data-testid="probe-border" className="border border-border">
        probe
      </div>
    </div>
  );
}

let harness: SdkHarness;

beforeEach(() => {
  harness = createAuthHarness();
  harness.resolveJson(consentInfo());
});

/** Render the consent prompt and wait for it to resolve, probes alongside. */
async function renderPrompt(): Promise<void> {
  await renderWithWallow(
    <>
      <ConsentScreen clientId={CLIENT_ID} returnUrl={RETURN_URL} scope={SCOPE} />
      <StyleProbes />
    </>,
    { harness },
  );

  // The screen renders NOTHING while the lookup is in flight, so every locator
  // below would otherwise race the query.
  await expect.element(page.getByTestId("consent-approve")).toBeInTheDocument();
}

/** The computed value of `property` on the element carrying `testId`. */
function computed(testId: string, property: string): string {
  return globalThis.getComputedStyle(page.getByTestId(testId).element()).getPropertyValue(property);
}

/** The rendered box of the element carrying `testId`. */
function box(testId: string): DOMRect {
  return page.getByTestId(testId).element().getBoundingClientRect();
}

describe("the consent card's heading takes its scale from Text", () => {
  it("distinguishes the three type steps at all", async () => {
    // The vacuity guard for the assertions below: if the Tailwind pipeline were
    // missing, all three probes would compute the inherited size and "the
    // heading matches text-base" would also be "the heading matches text-lg".
    await renderPrompt();

    const steps: Set<string> = new Set([
      computed("probe-base", "font-size"),
      computed("probe-lg", "font-size"),
      computed("probe-xl", "font-size"),
    ]);

    expect(steps).toHaveProperty("size", 3);
  });

  it("renders the heading at the app-wide card-heading step", async () => {
    await renderPrompt();

    expect(computed("consent-heading", "font-size")).toBe(computed("probe-base", "font-size"));
  });

  it("leaves the two steps this app's headings used to split across", async () => {
    // The regression, stated as an absence: `text-xl` is where this screen's own
    // heading sat, and `text-lg` is where its seven `CardTitle` siblings sat.
    await renderPrompt();

    const heading: string = computed("consent-heading", "font-size");

    expect(heading).not.toBe(computed("probe-lg", "font-size"));
    expect(heading).not.toBe(computed("probe-xl", "font-size"));
  });

  it("keeps the heading on the card's foreground colour", async () => {
    await renderPrompt();

    const heading: Rgba = computedColor(page.getByTestId("consent-heading").element(), "color");

    // Not merely "some colour": a theme-less page resolves every token to an
    // invalid value and paints transparent, which would make any comparison pass.
    expect(isTransparent(heading), "the heading paints a real colour").toBe(false);
  });
});

describe("the consent actions are the catalog's Button", () => {
  it("gives the approve action the recipe's centred flex box", async () => {
    // `inline-flex` + centred content is the recipe's base string, and it is what
    // a hand-rolled `<button className="w-full rounded-md …">` does not have:
    // that one computes `inline-block` and leaves its label wherever the text
    // alignment put it.
    await renderPrompt();

    expect(computed("consent-approve", "display")).toBe("inline-flex");
    expect(computed("consent-approve", "justify-content")).toBe("center");
    expect(computed("consent-approve", "align-items")).toBe("center");
  });

  it("gives the deny action the same box", async () => {
    await renderPrompt();

    expect(computed("consent-deny", "display")).toBe("inline-flex");
    expect(computed("consent-deny", "justify-content")).toBe("center");
    expect(computed("consent-deny", "align-items")).toBe("center");
  });

  it("paints approve on the primary surface", async () => {
    await renderPrompt();

    const approve: Rgba = computedColor(
      page.getByTestId("consent-approve").element(),
      "background-color",
    );
    const probe: Rgba = computedColor(
      page.getByTestId("probe-primary").element(),
      "background-color",
    );

    expect(isTransparent(probe), "the fork theme resolved --color-primary").toBe(false);
    expect(approve).toEqual(probe);
  });

  it("draws deny as an outline — a border and no surface of its own", async () => {
    // The whole reason F3.T1 added the `outline` variant: a deny button that
    // paints the same solid surface as approve gives the two answers equal
    // visual weight.
    await renderPrompt();

    const deny: Element = page.getByTestId("consent-deny").element();
    const border: Rgba = computedColor(deny, "border-top-color");
    const probeBorder: Rgba = computedColor(
      page.getByTestId("probe-border").element(),
      "border-top-color",
    );

    // Widths are compared as the browser's own strings against the `border`
    // probe rather than parsed to numbers: the probe carries the same utility
    // the recipe does, so the comparison stays true if Tailwind retunes it, and
    // the guard below is what stops `"0px" === "0px"` from passing for a button
    // with no border at all.
    expect(isTransparent(computedColor(deny, "background-color")), "no surface at rest").toBe(true);
    expect(computed("probe-border", "border-top-width"), "the border utility resolved").not.toBe(
      "0px",
    );
    expect(computed("consent-deny", "border-top-width")).toBe(
      computed("probe-border", "border-top-width"),
    );
    expect(isTransparent(probeBorder), "the fork theme resolved --color-border").toBe(false);
    expect(border).toEqual(probeBorder);
  });

  it("keeps both answers the same full width", async () => {
    // `Button`'s `width` axis defaults to `full`, which is what these two want —
    // but `cn()` merges a caller `className` over the recipe, so a stray width
    // utility at either call site would silently shrink one answer and not the
    // other.
    await renderPrompt();

    const approve: DOMRect = box("consent-approve");
    const deny: DOMRect = box("consent-deny");

    expect(approve.width).toBeGreaterThan(0);
    expect(deny.width).toBeCloseTo(approve.width, 0);
  });
});
