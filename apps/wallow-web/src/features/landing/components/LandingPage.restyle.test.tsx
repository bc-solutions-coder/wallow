import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

import {
  allByTestId,
  byTestId,
  expectClasses,
  expectTag,
  expectTokenColorsOnly,
  waitForTestId,
} from "@shared/testing/style-contract";
import { LandingPage } from "./LandingPage";

/**
 * Typography/token spec for the landing page (Wallow-lrlm.5.3).
 *
 * The page's copy contract — which badges, feature titles and step titles render,
 * in what order, against which fork branding — stays pinned by the sibling
 * `LandingPage.test.tsx`, which this spec must not edit. What is pinned HERE is
 * the part the `Text` migration could silently regress and nothing else covered:
 *
 *   - The marketing type scales. `Text`'s catalog defaults are one step smaller
 *     than the shipped page at three sites (hero `display` is text-4xl against a
 *     text-5xl hero; `subheading` is text-xl against two text-lg h3s), so each
 *     carries a className override. An override that stopped overriding would
 *     shrink the page with every behavioural spec still green.
 *   - The two INVERTED regions. The quick-start band and the outline CTA's hover
 *     state paint light-on-dark, so their text has to be told to invert too:
 *     `Text`'s default color is `text-foreground`, which on `bg-sidebar` is
 *     dark-on-dark. `color="onSidebar"` is what keeps the band readable.
 */

/**
 * Render the page and settle on its hero heading. The step and feature titles
 * repeat their testid across rows, so the gate is the one testid that does not —
 * `waitForTestId` is strict about a single match.
 */
async function renderPage(): Promise<void> {
  await render(<LandingPage />);
  await waitForTestId("home-heading");
}

/** The quick-start band: located from its step titles, not by counting sections. */
function stepsBand(): HTMLElement {
  const [first] = allByTestId("home-step-title");
  const section = first?.closest("section");
  expect(section, "expected the step titles to live inside a <section> band").not.toBeNull();
  return section as HTMLElement;
}

describe("LandingPage type scale", () => {
  it("keeps the hero one step above the display default", async () => {
    await renderPage();
    const heading = byTestId("home-heading");

    expectTag(heading, "h1");
    expectClasses(heading, "text-5xl font-bold text-foreground mb-4");
    // The override must WIN, not merely be present: `display` is text-4xl.
    expect(heading.classList.contains("text-4xl")).toBe(false);
  });

  it("keeps each feature title one step below the subheading default", async () => {
    await renderPage();

    for (const title of allByTestId("home-feature-title")) {
      expectTag(title, "h3");
      expectClasses(title, "text-lg font-semibold text-card-foreground");
      expect(title.classList.contains("text-xl")).toBe(false);
    }
  });

  it("keeps each quick-start step title one step below the subheading default", async () => {
    await renderPage();

    for (const title of allByTestId("home-step-title")) {
      expectTag(title, "h3");
      expectClasses(title, "text-lg font-semibold");
      expect(title.classList.contains("text-xl")).toBe(false);
    }
  });
});

describe("LandingPage inverted regions", () => {
  it("paints the quick-start band with the sidebar token pair", async () => {
    await renderPage();
    const band = stepsBand();

    expectClasses(band, "bg-sidebar text-sidebar-foreground");
    // The old band inverted through `bg-foreground text-background`, a pair with
    // no semantic name; `sidebar` is the token pair that MEANS "inverted band".
    expect(band.classList.contains("bg-foreground")).toBe(false);
    expect(band.classList.contains("text-background")).toBe(false);
    expectTokenColorsOnly(band);
  });

  it("inverts every text node inside the band, not just the band's own background", async () => {
    await renderPage();
    const band = stepsBand();

    // `Text`'s default color is `text-foreground` — dark on a dark band. Every
    // `Text` under the band must therefore opt into `onSidebar`.
    const inverted = [...band.querySelectorAll("h2, h3, code")];
    expect(
      inverted.length,
      "expected the band's heading, step titles and commands",
    ).toBeGreaterThan(0);
    for (const element of inverted) {
      expectClasses(element, "text-sidebar-foreground");
      expect(element.classList.contains("text-foreground")).toBe(false);
    }
  });

  it("inverts the outline CTA's hover state through the same token pair", async () => {
    await renderPage();
    const cta = byTestId("home-github-link");

    expectClasses(cta, "hover:bg-sidebar hover:text-sidebar-foreground");
    expect(cta.classList.contains("hover:bg-foreground")).toBe(false);
    expect(cta.classList.contains("hover:text-background")).toBe(false);
  });
});
