import { computedColor, isTransparent } from "@bc-solutions-coder/testing/contrast";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

/**
 * Theme-presence guard for this app's Vitest BROWSER project (Wallow-8ytl).
 *
 * Until that task this project loaded NO stylesheet at all — no utilities, no
 * theme. Both halves matter and they fail differently, so this file checks each:
 *
 *   - utilities missing => a ui control has no box. `Checkbox.Root`'s
 *     `<span role="checkbox">` measures 0x0 and every `userEvent.click` on it
 *     hangs to Playwright's ~15s actionability timeout, which is why four specs
 *     here used to toggle checkboxes by focus+Space instead.
 *   - theme missing => `@bc-solutions-coder/styles` maps every Tailwind colour
 *     token onto a VALUELESS custom property, so `var(--card)` is
 *     invalid-at-computed-value-time and a `bg-card` element paints
 *     `rgba(0, 0, 0, 0)`. Every rendered-colour assertion then compares two
 *     nothings and passes vacuously.
 *
 * `.test.tsx`, not `.test.ts`: the shared preset routes `src/**\/*.test.ts` to
 * the NODE project, which has no DOM to compute anything against.
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
