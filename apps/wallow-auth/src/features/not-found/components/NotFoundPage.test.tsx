import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

import { NotFoundPage } from "./NotFoundPage";

/**
 * Component spec for the Not Found screen (Wallow-ffpq.2.7).
 *
 * There is no Blazor oracle for this screen — `Wallow.Auth`'s `Routes.razor`
 * defined no `<NotFound>` fragment either, so unmatched auth URLs used to fall
 * through to whatever the framework printed. The contract below is therefore
 * this repo's own, and it is deliberately the same shape as `ErrorPage`: a
 * heading, one explanatory line, and a way out. The way out is `/login` rather
 * than `/` because this is the AUTH app — `/` only redirects to `/login`
 * anyway (`routes/index.tsx`), and a user who mistyped an auth URL wants the
 * sign-in page, not another bounce.
 *
 * Testids follow the repo's `{page}-{element}` rule: `not-found-heading`,
 * `not-found-message`, `not-found-login-link`.
 *
 * No SDK mock and no router: this screen is inert and takes no props, so it
 * renders bare.
 */
describe("NotFoundPage", () => {
  it("says the page was not found", async () => {
    render(<NotFoundPage />);

    await expect.element(page.getByTestId("not-found-heading")).toHaveTextContent(/not found/iu);
  });

  it("explains what happened in a line of its own", async () => {
    // Separate from the heading so the page reads as a page rather than a bare
    // status string — the framework default ("Not Found" and nothing else) is
    // exactly what this screen replaces.
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
    // `AuthLayout` supplies the `<h1>`, so this screen owns the `<h2>` beneath
    // it — the level `ErrorPage` uses for the same slot.
    render(<NotFoundPage />);

    const heading = page.getByRole("heading", { name: /not found/iu });

    await expect.element(heading).toBeInTheDocument();
  });
});
