import { forkBranding, forkRepositoryUrl } from "@bc-solutions-coder/styles";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

import { LandingPage } from "./LandingPage";
import { getStartedHref } from "@shared/lib/site-links";

/**
 * LandingPage — the marketing body.
 *
 * Everything a FORK owns is asserted against data, never a literal baked into
 * the test: the heading against `forkBranding.tagline`, the hero icon against
 * the branding filename, the two CTAs against the constants they render.
 * The copy the PLATFORM owns — tech badges, feature titles, quick-start steps
 * — is pinned literally and in order; that ordering is the content contract.
 */

/** Rooted, so the hero icon resolves to one file from every route depth. */
const rootedAppIcon = `/${forkBranding.appIcon}`;

const techBadges: readonly string[] = [
  ".NET 10",
  "React",
  "PostgreSQL",
  "Wolverine",
  "OpenIddict",
  "Clean Architecture",
];

const featureTitles: readonly string[] = [
  "Multi-tenancy",
  "Modular Monolith",
  "Clean Architecture & DDD",
  "CQRS with Wolverine",
  "Event-Driven Messaging",
  "Identity & OAuth",
];

const stepTitles: readonly string[] = ["Fork & Clone", "Start Infrastructure", "Run the API"];

/** Every `data-testid` match's text, in document order. */
function textsOf(testId: string): (string | null)[] {
  return page
    .getByTestId(testId)
    .elements()
    .map((element: Element): string | null => element.textContent);
}

describe("LandingPage", () => {
  it("headlines the fork's tagline as a single plain-text heading", async () => {
    await render(<LandingPage />);

    const heading: Element = page.getByTestId("home-heading").element();

    expect(heading.textContent).toBe(forkBranding.tagline);
    // The SSR shell spec matches a non-empty `<h1>..<h6>` with a regex that
    // rejects nested markup, so the tagline must be the heading's ONLY child
    // and a bare text node.
    expect(heading.children).toHaveLength(0);
  });

  it("serves the hero icon from the site root", async () => {
    await render(<LandingPage />);

    await expect.element(page.getByTestId("home-hero-icon")).toHaveAttribute("src", rootedAppIcon);
  });

  it("points its two CTAs at the BFF login flow and the fork's repository", async () => {
    await render(<LandingPage />);

    await expect
      .element(page.getByTestId("home-get-started"))
      .toHaveAttribute("href", getStartedHref);
    await expect
      .element(page.getByTestId("home-github-link"))
      .toHaveAttribute("href", forkRepositoryUrl);
  });

  it("lists the platform's six tech badges in order", async () => {
    await render(<LandingPage />);

    expect(textsOf("home-tech-badge")).toEqual(techBadges);
  });

  it("anchors a six-card features grid at #features", async () => {
    await render(<LandingPage />);

    // PublicLayout's Features nav link targets "/#features", so the section
    // must carry that id or the nav link goes nowhere.
    expect(page.getByTestId("home-features").element().getAttribute("id")).toBe("features");
    expect(textsOf("home-feature-title")).toEqual(featureTitles);
  });

  it("walks through the three quick-start steps in order", async () => {
    await render(<LandingPage />);

    expect(textsOf("home-step-title")).toEqual(stepTitles);
  });
});
