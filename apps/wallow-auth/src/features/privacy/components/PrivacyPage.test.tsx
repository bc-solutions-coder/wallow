import { render } from "vitest-browser-react";
import { page } from "vitest/browser";
import type { ReactElement } from "react";
import { describe, expect, it } from "vitest";

import { Route as privacyRoute } from "../../../routes/privacy";
import { PrivacyPage } from "./PrivacyPage";

/**
 * Component spec for the Privacy Policy screen (Wallow-vec7.3.3), ported from
 * the Blazor oracle `api/src/Wallow.Auth/Components/Pages/Privacy.razor`.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `privacy-heading`, `privacy-content`, `privacy-back-button`.
 *
 * WHAT THESE TESTS DO AND DO NOT PIN: the page is a legal document, and its prose
 * is not this port's to assert sentence-by-sentence — a test that hard-coded nine
 * paragraphs would fail every time Legal changed a word, which is noise, not
 * signal. What IS pinned is that all nine numbered sections survive the port with
 * their headings intact (the failure mode of porting a wall of text by eye is
 * silently dropping one) and that the page's two structural elements — its
 * heading and its way out — work.
 *
 * No SDK mock: this screen is inert. It makes no calls and reads no query string.
 */

/** The nine section headings, in the oracle's order. */
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
    // Guards the other half of the copy-paste failure: headings present, prose
    // dropped. Nine headings plus nine paragraphs is a lot of text; a page that
    // came out near-empty would still pass a heading-only check.
    await render(<PrivacyPage />);

    const content: HTMLElement = page.getByTestId("privacy-content").element() as HTMLElement;
    expect(content.textContent?.length ?? 0).toBeGreaterThan(1000);
  });

  it("gives the reader a way back to register", async () => {
    // Oracle: `Href="/register"` — this page is reached FROM the register form's
    // consent checkboxes, so back means back to register, not to login.
    await render(<PrivacyPage />);

    await expect
      .element(page.getByTestId("privacy-back-button"))
      .toHaveAttribute("href", "/register");
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
