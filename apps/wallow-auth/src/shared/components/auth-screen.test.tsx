import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

import { AuthScreen } from "./auth-screen";

/**
 * The skeleton all 16 screens open with, extracted from the 11 local
 * `CardHeading` functions that each rebuilt it.
 *
 * What this pins is the ORDER — heading, then error, then body, then footer —
 * because every screen relied on it and none of them stated it. The error slot
 * is the load-bearing half: each screen names its own banner testid, which the
 * Playwright suites assert on, so the shell must never derive one.
 */

/** A stand-in for whatever a screen puts in the body. */
const BODY = "screen body";

describe("AuthScreen", () => {
  it("renders the title as the card's h2", async () => {
    await render(<AuthScreen title="Two-factor authentication">{BODY}</AuthScreen>);

    expect(page.getByRole("heading", { level: 2 }).element().textContent).toBe(
      "Two-factor authentication",
    );
  });

  it("renders a description beneath the title when given one", async () => {
    await render(
      <AuthScreen title="Reset your password" description="Enter your new password below.">
        {BODY}
      </AuthScreen>,
    );

    expect(page.getByText("Enter your new password below.").element()).toBeDefined();
  });

  it("renders no error banner when error is null", async () => {
    // Every screen spelled this as `error === null ? null : <ErrorBanner/>`. An
    // always-rendered empty banner would leave a coloured gap on a clean screen
    // AND make the testid resolvable before anything has failed, which is what
    // the E2E suites wait on to prove a failure.
    const { container } = await render(
      <AuthScreen title="Sign in" error={null} errorTestId="mfa-challenge-error">
        {BODY}
      </AuthScreen>,
    );

    expect(container.querySelector('[data-testid="mfa-challenge-error"]')).toBeNull();
  });

  it("renders no error banner when error is absent entirely", async () => {
    const { container } = await render(
      <AuthScreen title="Sign in" errorTestId="mfa-challenge-error">
        {BODY}
      </AuthScreen>,
    );

    expect(container.querySelector('[data-testid="mfa-challenge-error"]')).toBeNull();
  });

  it("renders the banner under the screen's own testid when error is set", async () => {
    // The testid is passed in rather than derived: it is an E2E contract each
    // screen already owns, and 8 distinct values are in use.
    const { container } = await render(
      <AuthScreen title="Sign in" error="Invalid code" errorTestId="mfa-challenge-error">
        {BODY}
      </AuthScreen>,
    );

    const banner = container.querySelector('[data-testid="mfa-challenge-error"]');
    expect(banner).not.toBeNull();
    expect((banner as HTMLElement).textContent).toBe("Invalid code");
  });

  it("orders the heading, the error, the body and the footer", async () => {
    // The order every screen relied on and none of them stated. An error banner
    // rendered below the form is one a user scrolls past.
    const { container } = await render(
      <AuthScreen
        title="Sign in"
        error="Invalid code"
        errorTestId="login-error"
        footer={<a href="/login">Back to sign in</a>}
      >
        <p data-testid="body">{BODY}</p>
      </AuthScreen>,
    );

    const order = [...container.querySelectorAll("h2, [data-testid], a")].map((element) =>
      element.tagName.toLowerCase(),
    );

    // heading, banner, body, footer — the banner is the `div` in the middle.
    expect(order).toEqual(["h2", "div", "p", "a"]);
  });

  it("renders the footer beneath the body", async () => {
    await render(
      <AuthScreen title="Sign in" footer={<a href="/login">Back to sign in</a>}>
        {BODY}
      </AuthScreen>,
    );

    expect(page.getByRole("link", { name: "Back to sign in" }).element()).toBeDefined();
  });

  it("passes a spacing override through to the card surface", async () => {
    // LoginScreen and RegisterForm are the two measured outliers; without this
    // the shell would silently retune both.
    const { container } = await render(
      <AuthScreen title="Sign in" spacing="p-6 space-y-4">
        {BODY}
      </AuthScreen>,
    );

    const card = container.firstElementChild as HTMLElement;
    expect([...card.classList]).toContain("space-y-4");
    expect([...card.classList]).not.toContain("space-y-6");
  });
});
