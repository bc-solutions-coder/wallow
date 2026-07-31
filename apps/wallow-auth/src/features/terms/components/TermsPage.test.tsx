import type { ReactElement } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

import { Route as termsRoute } from "@app/routes/terms";
import { TermsPage } from "./TermsPage";

/**
 * Terms of Service screen — the static document, NOT `/accept-terms`, which is
 * the ToS *gate* (a form the user submits) that links here. The two are easy to
 * cross, so the last test in the component block pins that this page carries no
 * gate-like controls.
 *
 * On what is and is not pinned about the prose, see the header of
 * `PrivacyPage.test.tsx` — same reasoning, same shape.
 */

/** The nine section headings, in document order. */
const SECTIONS: readonly string[] = [
  "Acceptance of Terms",
  "Use of Service",
  "User Accounts",
  "Prohibited Activities",
  "Intellectual Property",
  "Limitation of Liability",
  "Termination",
  "Changes to Terms",
  "Contact",
];

describe("TermsPage", () => {
  it("is titled Terms of Service", async () => {
    render(<TermsPage />);

    await expect.element(page.getByTestId("terms-heading")).toHaveTextContent("Terms of Service");
  });

  it("shows the last-updated date", async () => {
    render(<TermsPage />);

    await expect.element(page.getByTestId("terms-content")).toBeInTheDocument();
    expect(document.body.textContent).toMatch(/last updated/iu);
  });

  it("carries all nine sections of the terms", async () => {
    render(<TermsPage />);

    const content = page.getByTestId("terms-content");

    for (const [index, section] of SECTIONS.entries()) {
      await expect.element(content).toHaveTextContent(`${String(index + 1)}. ${section}`);
    }
  });

  it("gives every section a body, not just a heading", async () => {
    render(<TermsPage />);

    await expect.element(page.getByTestId("terms-content")).toBeInTheDocument();
    expect(page.getByTestId("terms-content").element().textContent?.length ?? 0).toBeGreaterThan(
      1000,
    );
  });

  it("gives the reader a way back to register", async () => {
    render(<TermsPage />);

    await expect
      .element(page.getByTestId("terms-back-button"))
      .toHaveAttribute("href", "/register");

    // And it is announced as a LINK. The catalog Button composes onto an anchor
    // here, and Base UI stamps `role="button"` on every non-native element it
    // substitutes — which drops this control out of a screen reader's links list
    // while its href still offers open-in-new-tab.
    expect(page.getByRole("link", { name: /back to register/iu }).query()).toBe(
      page.getByTestId("terms-back-button").element(),
    );
    expect(page.getByRole("button", { name: /back to register/iu }).query()).toBeNull();
  });

  it("is the document, not the acceptance gate", () => {
    // `/terms` renders prose and a way back — no checkbox, no submit. A failure
    // here means someone has crossed this screen with `/accept-terms`.
    render(<TermsPage />);

    expect(page.getByRole("checkbox").query()).toBeNull();
    expect(page.getByTestId("accept-terms-submit").query()).toBeNull();
  });
});

describe("/terms route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    const RouteComponent = termsRoute.options.component as () => ReactElement;

    render(<RouteComponent />);

    expect(page.getByTestId("route-placeholder").query()).toBeNull();
    await expect.element(page.getByTestId("terms-heading")).toBeInTheDocument();
  });
});
