import { render } from "vitest-browser-react";
import { page } from "vitest/browser";
import type { ReactElement } from "react";
import { describe, expect, it } from "vitest";

import { Route as privacyRoute } from "@app/routes/privacy";
import { PrivacyPage } from "./PrivacyPage";

/**
 * Privacy Policy screen.
 *
 * The prose is a legal document, so it is not asserted sentence by sentence —
 * that would fail on every wording change. What is pinned is that all nine
 * numbered sections are present and carry a body, because silently dropping
 * one is how a wall of text goes wrong, plus the page's heading and its way
 * out.
 */

/** The nine section headings, in document order. */
const SECTIONS: readonly string[] = [
  "Information We Collect",
  "How We Use Your Information",
  "Information Sharing",
  "Data Security",
  "Your Rights",
  "Cookies",
  "Children's Privacy",
  "Changes to This Policy",
  "Contact",
];

describe("PrivacyPage", () => {
  it("is titled Privacy Policy", async () => {
    await render(<PrivacyPage />);

    await expect.element(page.getByTestId("privacy-heading")).toHaveTextContent("Privacy Policy");
  });

  it("shows the last-updated date", async () => {
    await render(<PrivacyPage />);

    expect(document.body.textContent).toMatch(/last updated/iu);
  });

  it("carries all nine sections of the policy", async () => {
    await render(<PrivacyPage />);

    const content: HTMLElement = page.getByTestId("privacy-content").element() as HTMLElement;

    for (const [index, section] of SECTIONS.entries()) {
      expect(content).toHaveTextContent(`${String(index + 1)}. ${section}`);
    }
  });

  it("gives every section a body, not just a heading", async () => {
    await render(<PrivacyPage />);

    const content: HTMLElement = page.getByTestId("privacy-content").element() as HTMLElement;
    expect(content.textContent?.length ?? 0).toBeGreaterThan(1000);
  });

  it("gives the reader a way back to register", async () => {
    // This page is reached FROM the register form's consent checkboxes, so back
    // means back to register, not to login.
    await render(<PrivacyPage />);

    await expect
      .element(page.getByTestId("privacy-back-button"))
      .toHaveAttribute("href", "/register");

    // And it is announced as a LINK: the catalog Button composes onto an anchor
    // here, and Base UI stamps `role="button"` on every non-native element it
    // substitutes, which would drop this control out of a screen reader's links
    // list. Same assertion in `TermsPage.test.tsx`.
    expect(page.getByRole("link", { name: /back to register/iu }).query()).toBe(
      page.getByTestId("privacy-back-button").element(),
    );
    expect(page.getByRole("button", { name: /back to register/iu }).query()).toBeNull();
  });
});

describe("/privacy route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    const RouteComponent = privacyRoute.options.component as () => ReactElement;

    await render(<RouteComponent />);

    expect(page.getByTestId("route-placeholder").query()).toBeNull();
    await expect.element(page.getByTestId("privacy-heading")).toBeInTheDocument();
  });
});
