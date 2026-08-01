/**
 * Stylesheet-presence guard for a consumer's Vitest BROWSER project.
 *
 * The two halves fail differently, so each is asserted. With no utilities a
 * catalog control has no box — `Checkbox.Root`'s `<span role="checkbox">`
 * measures 0x0 and every click hangs to Playwright's ~15s actionability timeout.
 * With no theme every Tailwind colour token maps onto a VALUELESS custom
 * property (`--color-card: var(--card, …)`), so `bg-card` paints
 * `rgba(0, 0, 0, 0)` and every rendered-colour assertion in the project goes
 * vacuous — both sides of a contrast pair read as the same nothing.
 *
 * The caller's spec must be `*.test.tsx`: the preset routes `*.test.ts` to the
 * NODE project, where none of this can run.
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
