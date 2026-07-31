import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

import { NotFoundPage } from "./NotFoundPage";

/**
 * Not Found screen: a heading, one explanatory line, and a way out.
 *
 * The way out is `/login`, not `/`, because this is the AUTH app — `/` only
 * redirects to `/login` anyway, and a user who mistyped an auth URL wants the
 * sign-in page rather than another bounce.
 */
describe("NotFoundPage", () => {
  it("says the page was not found", async () => {
    render(<NotFoundPage />);

    await expect.element(page.getByTestId("not-found-heading")).toHaveTextContent(/not found/iu);
  });

  it("explains what happened in a line of its own", async () => {
    render(<NotFoundPage />);

    const message = page.getByTestId("not-found-message");

    await expect.element(message).toBeInTheDocument();
    expect((message.element().textContent ?? "").trim().length).toBeGreaterThan(0);
  });

  it("offers a way back to the sign-in page", async () => {
    render(<NotFoundPage />);

    await expect
      .element(page.getByTestId("not-found-login-link"))
      .toHaveAttribute("href", "/login");
  });

  it("labels that way out in words, not just a URL", async () => {
    render(<NotFoundPage />);

    await expect
      .element(page.getByTestId("not-found-login-link"))
      .toHaveTextContent(/sign in|log in|login/iu);
  });

  it("is a heading a screen reader can land on", async () => {
    // The root shell's `<FocusOnNavigate/>` moves focus to the page's main
    // heading on every navigation; a 404 rendered as an anonymous <div> would
    // leave a screen-reader user hearing nothing about why the page changed.
    render(<NotFoundPage />);

    const heading = page.getByRole("heading", { name: /not found/iu });

    await expect.element(heading).toBeInTheDocument();
  });
});
