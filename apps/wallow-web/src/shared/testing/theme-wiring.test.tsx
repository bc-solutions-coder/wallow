import { computedColor, isTransparent } from "@bc-solutions-coder/testing/contrast";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

/**
 * The fork theme is present in this app's Vitest BROWSER project.
 *
 * `@bc-solutions-coder/styles` maps every Tailwind colour token onto a
 * VALUELESS custom property (`--color-sidebar: var(--sidebar, …)`); the values
 * arrive only with `virtual:wallow-theme.css`. Without it every `var()` is
 * invalid-at-computed-value-time, a `bg-sidebar` element paints
 * `rgba(0, 0, 0, 0)`, and every rendered-colour assertion in the project goes
 * vacuous — both sides of a contrast pair read as the same nothing.
 */
describe("browser project theme wiring", () => {
  /**
   * `--background` covers the app surface; `--sidebar` and `--foreground` are
   * the two halves of the nav contrast pair.
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
