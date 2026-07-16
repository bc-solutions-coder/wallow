/** @vitest-environment jsdom */
import * as matchers from "@testing-library/jest-dom/matchers";
import { render, screen } from "@testing-library/react";
import type { ReactElement } from "react";
import { describe, expect, it } from "vitest";

import { Route as termsRoute } from "../../../routes/terms";
import { TermsPage } from "./TermsPage";

expect.extend(matchers);

/**
 * Component spec for the Terms of Service screen (Wallow-vec7.3.3), ported from
 * the Blazor oracle `api/src/Wallow.Auth/Components/Pages/Terms.razor`.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `terms-heading`, `terms-content`, `terms-back-button`.
 *
 * NOT `/accept-terms` — that is the ToS *gate* (a form the user submits), owned
 * by Wallow-vec7.3.10. This is the static document the gate links to. The two
 * are easy to cross; the last test in the component block pins that this page
 * has no gate-like controls on it.
 *
 * On what is and is not pinned about the prose, see the header of
 * `PrivacyPage.test.tsx` — same reasoning, same shape.
 */

/** The nine section headings, in the oracle's order. */
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
  it("is titled Terms of Service", () => {
    render(<TermsPage />);

    expect(screen.getByTestId("terms-heading")).toHaveTextContent("Terms of Service");
  });

  it("shows the last-updated date", () => {
    render(<TermsPage />);

    expect(document.body.textContent).toMatch(/last updated/iu);
  });

  it("carries all nine sections of the terms", () => {
    render(<TermsPage />);

    const content: HTMLElement = screen.getByTestId("terms-content");

    for (const [index, section] of SECTIONS.entries()) {
      expect(content).toHaveTextContent(`${String(index + 1)}. ${section}`);
    }
  });

  it("gives every section a body, not just a heading", () => {
    render(<TermsPage />);

    expect(screen.getByTestId("terms-content").textContent?.length ?? 0).toBeGreaterThan(1000);
  });

  it("gives the reader a way back to register", () => {
    render(<TermsPage />);

    expect(screen.getByTestId("terms-back-button")).toHaveAttribute("href", "/register");
  });

  it("is the document, not the acceptance gate", () => {
    // `/terms` renders prose and a way back — no checkbox, no submit. If this
    // ever fails, someone has crossed this screen with `/accept-terms`
    // (Wallow-vec7.3.10).
    render(<TermsPage />);

    expect(screen.queryByRole("checkbox")).toBeNull();
    expect(screen.queryByTestId("accept-terms-submit")).toBeNull();
  });
});

describe("/terms route", () => {
  it("renders the real screen in place of the pre-registration placeholder", () => {
    const RouteComponent = termsRoute.options.component as () => ReactElement;

    render(<RouteComponent />);

    expect(screen.queryByTestId("route-placeholder")).toBeNull();
    expect(screen.getByTestId("terms-heading")).toBeInTheDocument();
  });
});
