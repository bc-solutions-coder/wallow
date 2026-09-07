/**
 * Browser-only assertions for nonempty theme tokens, a nontransparent color probe,
 * and a Tailwind-sized element. Call assertThemeWiring at module scope in a
 * .test.tsx file so the preset runs its suite in Chromium.
 */
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

import { computedColor, isTransparent } from "./contrast";

/** Options for {@link assertThemeWiring}. */
export interface ThemeWiringOptions {
  /**
   * Fork-theme custom properties that must resolve to a value — the surfaces
   * this app is actually painted from.
   */
  tokens: readonly string[];
  /**
   * Utility classes for the colour probe, e.g. `"bg-card text-card-foreground"`.
   * Its background is what the transparency assertion measures.
   */
  probeClass: string;
}

/**
 * Declare the shared `describe` block asserting this app's browser project has
 * both the Tailwind utilities and the fork theme loaded.
 *
 * @param options See {@link ThemeWiringOptions}.
 */
export function assertThemeWiring(options: ThemeWiringOptions): void {
  describe("browser project theme wiring", () => {
    for (const token of options.tokens) {
      it(`resolves the fork theme custom property ${token}`, () => {
        const value: string = globalThis
          .getComputedStyle(document.documentElement)
          .getPropertyValue(token);

        expect(value.trim()).not.toBe("");
      });
    }

    it("paints the probe with a real colour rather than transparent", async () => {
      await render(
        <div data-testid="theme-probe" className={options.probeClass}>
          probe
        </div>,
      );

      const probe: Element = page.getByTestId("theme-probe").element();

      expect(isTransparent(computedColor(probe, "background-color"))).toBe(false);
    });

    it("compiles the Tailwind utilities, so a catalog control has a box", async () => {
      await render(<div data-testid="box-probe" className="size-4" />);

      const box: DOMRect = page.getByTestId("box-probe").element().getBoundingClientRect();

      // `size-4` is what gives `Checkbox.Root`'s `<span role="checkbox">` a
      // clickable area; at 0x0 every `userEvent.click` on it hangs to
      // Playwright's actionability timeout.
      expect(box.width).toBeGreaterThan(0);
      expect(box.height).toBeGreaterThan(0);
    });
  });
}
