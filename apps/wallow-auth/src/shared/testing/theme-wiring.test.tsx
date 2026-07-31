import { computedColor, isTransparent } from "@bc-solutions-coder/testing/contrast";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

/**
 * Stylesheet-presence guard for this app's Vitest BROWSER project; the two
 * halves fail differently, so each is checked. With no utilities a catalog
 * control has no box — `Checkbox.Root`'s `<span role="checkbox">` measures 0x0
 * and every click hangs to Playwright's ~15s actionability timeout. With no
 * theme every Tailwind colour token maps onto a VALUELESS custom property, so
 * `bg-card` paints `rgba(0, 0, 0, 0)` and colour assertions pass vacuously.
 *
 * `.test.tsx`, not `.test.ts`: the preset routes `*.test.ts` to the NODE project.
 */
describe("browser project theme wiring", () => {
  /** The page surfaces every auth screen is painted from. */
  const tokens: readonly string[] = ["--background", "--foreground", "--card"];

  for (const token of tokens) {
    it(`resolves the fork theme custom property ${token}`, () => {
      const value: string = globalThis
        .getComputedStyle(document.documentElement)
        .getPropertyValue(token);

      expect(value.trim()).not.toBe("");
    });
  }

  it("paints a bg-card element with a real colour rather than transparent", async () => {
    await render(
      <div data-testid="theme-probe" className="bg-card text-card-foreground">
        probe
      </div>,
    );

    const probe: Element = page.getByTestId("theme-probe").element();

    expect(isTransparent(computedColor(probe, "background-color"))).toBe(false);
  });

  it("compiles the Tailwind utilities, so a catalog control has a box", async () => {
    await render(<div data-testid="box-probe" className="size-4" />);

    const box: DOMRect = page.getByTestId("box-probe").element().getBoundingClientRect();

    expect(box.width).toBeGreaterThan(0);
    expect(box.height).toBeGreaterThan(0);
  });
});
