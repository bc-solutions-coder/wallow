import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

import {
  byTestId,
  expectClasses,
  expectTag,
  expectTokenColorsOnly,
  waitForTestId,
  within,
} from "@shared/testing/style-contract";
import { PublicLayout } from "./PublicLayout";

/**
 * Which token paints the public chrome's footer band.
 *
 * The band is inverted, so every text node inside it has to invert with the
 * surface: `Text`'s default color is `text-foreground`, which on `bg-sidebar`
 * lands dark-on-dark and reads as a blank strip.
 */
describe("PublicLayout footer band", () => {
  it("paints the footer with the sidebar token pair", async () => {
    await render(<PublicLayout />);
    const footer = await waitForTestId("public-footer");

    expectClasses(footer, "bg-sidebar text-sidebar-foreground");
    // `bg-foreground text-background` inverts by hand and names nothing;
    // `sidebar` is the token pair that MEANS "inverted".
    expect(footer.classList.contains("bg-foreground")).toBe(false);
    expect(footer.classList.contains("text-background")).toBe(false);
    expectTokenColorsOnly(footer);
  });

  it("inverts the license notice rather than leaving it on the default color", async () => {
    await render(<PublicLayout />);
    const footer = await waitForTestId("public-footer");

    const notice = within(footer, "span");
    expectTag(notice, "span");
    expect(notice.textContent).toBe("MIT Licensed");
    // `bodySm`, not the `body` default: the notice is footer-row copy at
    // `text-sm`, and inverting it must not resize it.
    expectClasses(notice, "text-sm text-sidebar-foreground");
    expect(notice.classList.contains("text-foreground")).toBe(false);
  });

  it("inverts both footer links, keeping the primary hover accent", async () => {
    await render(<PublicLayout />);
    await waitForTestId("public-footer");

    for (const testId of ["public-footer-github", "public-footer-docs"]) {
      const link = byTestId(testId);
      expectClasses(link, "text-sidebar-foreground hover:text-primary no-underline");
      expect(link.classList.contains("text-background")).toBe(false);
    }
  });
});
