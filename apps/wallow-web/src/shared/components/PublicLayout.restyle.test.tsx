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
 * Token spec for the public chrome's footer (Wallow-lrlm.5.3).
 *
 * The footer is the public page's SECOND inverted band — the landing page's
 * quick-start band is the first — and nothing pinned it. Its links and license
 * notice sit light-on-dark, so every text node inside it has to invert with the
 * surface: `Text`'s default color is `text-foreground`, which on `bg-sidebar`
 * lands dark-on-dark and reads as a blank strip.
 *
 * The footer's CONTENT — the MIT notice, the GitHub/Docs links and their fork
 * link targets — stays pinned by the sibling `PublicLayout.test.tsx`, which this
 * spec must not edit.
 */
describe("PublicLayout footer band", () => {
  it("paints the footer with the sidebar token pair", async () => {
    await render(<PublicLayout />);
    const footer = await waitForTestId("public-footer");

    expectClasses(footer, "bg-sidebar text-sidebar-foreground");
    // The old footer inverted through `bg-foreground text-background`, a pair
    // with no semantic name; `sidebar` is the token pair that MEANS "inverted".
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
    // `bodySm`, not the `body` default: the notice inherited the footer row's
    // `text-sm` before it became a `Text`, and a restyle never resizes copy.
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
