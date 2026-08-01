import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

import { AccessRequestPage } from "./AccessRequestPage";

/**
 * The request-submitted screen: what a pending join request says, and the two ways out.
 *
 * The sign-out link is not decoration. The visitor reaching this screen is authenticated as
 * somebody without access to the organization behind the client, so the home link alone would
 * send them back through authorize and return them here.
 */
describe("AccessRequestPage", () => {
  it("says the request was sent", async () => {
    render(<AccessRequestPage />);

    await expect
      .element(page.getByTestId("access-request-heading"))
      .toHaveTextContent(/request sent/iu);
  });

  it("explains that the request is awaiting review", async () => {
    render(<AccessRequestPage />);

    await expect
      .element(page.getByTestId("access-request-message"))
      .toHaveTextContent(/waiting for an administrator to review/iu);
  });

  it("offers a way out of the wrong account", async () => {
    render(<AccessRequestPage />);

    await expect
      .element(page.getByTestId("access-request-sign-out-link"))
      .toHaveAttribute("href", expect.stringContaining("/logout"));
  });

  it("offers a way home", async () => {
    render(<AccessRequestPage />);

    await expect.element(page.getByTestId("access-request-back-link")).toBeVisible();
  });
});
