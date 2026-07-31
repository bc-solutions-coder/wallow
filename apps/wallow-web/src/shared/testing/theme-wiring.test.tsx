import { computedColor, isTransparent } from "@bc-solutions-coder/testing/contrast";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

/**
 * Theme-presence guard for this app's Vitest BROWSER project (Wallow-8ytl).
 *
 * `@bc-solutions-coder/styles`'s shared entry maps every Tailwind colour token
 * onto a VALUELESS custom property (`--color-sidebar: var(--sidebar, …)`); the
 * values arrive only from `renderThemeStyle(forkResolvedBranding)`, which this
 * project gets as `virtual:wallow-theme.css` through ../../../vitest.setup.ts.
 * Without it every `var()` is invalid-at-computed-value-time, so a `bg-sidebar`
 * element paints `rgba(0, 0, 0, 0)`.
 *
 * That is not a cosmetic gap. It makes EVERY rendered-colour assertion in this
 * project vacuous — both sides of a contrast pair read as the same nothing — so
 * a contrast defect passes a green suite. This file fails outright if the theme
 * goes missing, and it asserts nothing about any component's class string, so it
 * cannot be quietly satisfied by editing a recipe.
 *
 * `.test.tsx`, not `.test.ts`: the shared preset routes `src/**\/*.test.ts` to
 * the NODE project, which has no DOM to compute anything against.
 */
describe("browser project theme wiring", () => {
  /**
   * The tokens whose absence made the drawer and its backdrop measure
   * transparent when this gap was found. `--background` covers the app surface;
   * `--sidebar` and `--foreground` are the two halves of the nav contrast pair.
   */
  const tokens: readonly string[] = ["--background", "--foreground", "--sidebar"];

  for (const token of tokens) {
    it(`resolves the fork theme custom property ${token}`, () => {
      const value: string = globalThis
        .getComputedStyle(document.documentElement)
        .getPropertyValue(token);

      expect(value.trim()).not.toBe("");
    });
  }

  it("paints a bg-sidebar element with a real colour rather than transparent", async () => {
    await render(
      <div data-testid="theme-probe" className="bg-sidebar text-sidebar-foreground">
        probe
      </div>,
    );

    const probe: Element = page.getByTestId("theme-probe").element();

    // The exact failure recorded on the bead: `rgba(0, 0, 0, 0)` on a bg-sidebar
    // element, measured in this app with the nav drawer open.
    expect(isTransparent(computedColor(probe, "background-color"))).toBe(false);
  });

  it("compiles the Tailwind utilities, so a catalog control has a box", async () => {
    await render(<div data-testid="box-probe" className="size-4" />);

    const box: DOMRect = page.getByTestId("box-probe").element().getBoundingClientRect();

    // `size-4` is what gives `Checkbox.Root`'s `<span role="checkbox">` a
    // clickable area; at 0x0 every `userEvent.click` on it hangs to Playwright's
    // actionability timeout.
    expect(box.width).toBeGreaterThan(0);
    expect(box.height).toBeGreaterThan(0);
  });
});
