import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

import { AuthLayout } from "./auth-layout";

/**
 * AuthLayout's theme control. wallow-auth has no nav, so this layout is the
 * chrome every auth screen renders inside and the app's only shared home for
 * the toggle.
 *
 * `data-testid="theme-toggle"` is app-owned: `packages/ui` deliberately does not
 * default it and the Playwright suites select on it, so it is asserted here at
 * the place that supplies it.
 */
describe("AuthLayout theme toggle", () => {
  it("renders the theme toggle on every auth screen", async () => {
    await render(
      <AuthLayout>
        <p data-testid="login-form">sign in</p>
      </AuthLayout>,
    );

    await expect.element(page.getByTestId("theme-toggle")).toBeInTheDocument();
  });

  it("names the control for assistive tech", async () => {
    await render(<AuthLayout />);

    expect(page.getByTestId("theme-toggle").element().getAttribute("aria-label")).toMatch(
      /theme/iu,
    );
  });

  it("keeps the toggle out of the branded heading", async () => {
    // The `<h1>` is the route-change focus target `FocusOnNavigate` moves to on
    // every navigation; a control inside it would be announced as part of the
    // screen's name.
    await render(<AuthLayout />);

    const heading = page.getByRole("heading", { level: 1 }).element();
    expect(heading.querySelector('[data-testid="theme-toggle"]')).toBeNull();
  });
});
