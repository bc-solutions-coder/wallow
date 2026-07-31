import { computedColor, isTransparent, type Rgba } from "@bc-solutions-coder/testing/contrast";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import type { SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { createAuthHarness } from "@shared/testing/harness";
import { ConsentScreen } from "./ConsentScreen";

/**
 * The measured half of wallow-auth's catalog adoption, on the consent screen.
 *
 * Lint proves the source names `Text` and `Button`; only the browser can say
 * what it PAINTS, since the live class list is `twMerge`d with the call site's
 * `className`. Every claim here reads `getComputedStyle` and compares against a
 * sibling probe rather than a literal, so a fork retuning its scale or palette
 * keeps the spec true — and an unresolved probe is caught, not passed over.
 */

const CLIENT_ID = "wallow-web";
const RETURN_URL = "/connect/authorize?client_id=wallow-web&scope=openid";
const SCOPE = "openid profile";

/** A `ConsentInfoResponse`, as the generated type shapes it. */
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
 * All three type steps are rendered so the spec can prove they are
 * DISTINGUISHABLE before claiming the heading matches one of them.
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

/**
 * Move the real pointer to a corner nothing below renders into, BEFORE the
 * fixture mounts.
 *
 * Every claim here is about the card AT REST, and `buttonRecipe` gives both
 * answers a hover arm with a `motion-safe:transition-colors` base. The
 * Playwright pointer position persists across spec FILES, and the browser
 * re-evaluates `:hover` when new content is inserted underneath it — so a
 * rest-state colour read can return a hover colour, or that transition caught
 * partway. Parking before the render is what makes this deterministic: the
 * buttons are never under the cursor at any point in their lifetime. It is the
 * POSITION that persists, not the node, so the park element can be removed.
 */
async function parkPointer(): Promise<void> {
  const park: HTMLDivElement = document.createElement("div");
  park.style.cssText = "position:fixed;bottom:0;right:0;width:4px;height:4px";
  document.body.append(park);

  try {
    await userEvent.hover(park);
  } finally {
    park.remove();
  }
}

beforeEach(async () => {
  harness = createAuthHarness();
  harness.resolveJson(consentInfo());
  await parkPointer();
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
    // The vacuity guard: with no Tailwind pipeline all three probes compute the
    // inherited size, and every comparison below passes against any of them.
    await renderPrompt();

    const steps: Set<string> = new Set([
      computed("probe-base", "font-size"),
      computed("probe-lg", "font-size"),
      computed("probe-xl", "font-size"),
    ]);

    expect(steps).toHaveProperty("size", 3);
  });

  it("renders the heading at the catalog-wide card-heading step", async () => {
    await renderPrompt();

    expect(computed("consent-heading", "font-size")).toBe(computed("probe-xl", "font-size"));
  });

  it("leaves the two steps this screen's heading used to sit on", async () => {
    await renderPrompt();

    const heading: string = computed("consent-heading", "font-size");

    expect(heading).not.toBe(computed("probe-lg", "font-size"));
    expect(heading).not.toBe(computed("probe-base", "font-size"));
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
    // The discriminator: a hand-rolled `<button className="w-full rounded-md …">`
    // computes `inline-block` and leaves its label wherever text-align put it.
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

    const probe: Rgba = computedColor(
      page.getByTestId("probe-primary").element(),
      "background-color",
    );

    expect(isTransparent(probe), "the fork theme resolved --color-primary").toBe(false);

    // A bounded poll rather than one read: background-color is the one axis
    // carrying a transition. It cannot let a wrong surface through — the poll's
    // only exit is this exact equality against the `bg-primary` probe.
    await expect
      .poll(() => computedColor(page.getByTestId("consent-approve").element(), "background-color"))
      .toEqual(probe);
  });

  it("draws deny as an outline — a border and no surface of its own", async () => {
    // A deny painting the same solid surface as approve gives the two answers
    // equal visual weight.
    await renderPrompt();

    const deny: Element = page.getByTestId("consent-deny").element();
    const border: Rgba = computedColor(deny, "border-top-color");
    const probeBorder: Rgba = computedColor(
      page.getByTestId("probe-border").element(),
      "border-top-color",
    );

    // Widths are compared as the browser's own strings against the `border`
    // probe; the non-`"0px"` guard is what stops `"0px" === "0px"` from passing
    // for a button with no border at all.
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
