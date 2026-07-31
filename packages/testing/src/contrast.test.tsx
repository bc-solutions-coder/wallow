import { page } from "vitest/browser";
import { describe, expect, it } from "vitest";

import {
  computedColor,
  contrastRatio,
  effectiveBackground,
  isTransparent,
  over,
  parseColor,
  relativeLuminance,
  type Rgba,
  textContrast,
} from "./contrast";
import { render } from "./render";

/**
 * Specs for the `./contrast` subpath — the measured-colour helpers app specs use
 * instead of class-string assertions.
 *
 * `.test.tsx` because these need a real DOM and a real canvas: the preset routes
 * `*.test.ts` to the NODE project, where `document` does not exist.
 *
 * This project loads no stylesheet, which suits the subject — every case sets
 * colours inline, so what is under test is the PARSING and the arithmetic rather
 * than any Tailwind token.
 */
describe("parseColor", () => {
  it("parses rgb() and rgba()", () => {
    expect(parseColor("rgb(255, 0, 0)")).toEqual({ r: 255, g: 0, b: 0, a: 1 });
    expect(parseColor("rgba(0, 0, 0, 0)").a).toBe(0);
  });

  it("parses oklch(), the syntax api/branding.json actually uses", () => {
    // The whole reason these helpers paint into a canvas rather than running an
    // `rgb()` regex: Chromium hands back the authored colour space in a computed
    // value, and the fork palette is authored in oklch.
    const white: Rgba = parseColor("oklch(1 0 0)");

    expect(white.a).toBe(1);
    expect(white.r).toBeGreaterThan(250);
    expect(white.g).toBeGreaterThan(250);
    expect(white.b).toBeGreaterThan(250);
  });

  it("recovers the authored channels of a translucent colour", () => {
    // Painted onto a cleared canvas the bytes come back scaled by alpha; the
    // helper undoes that, so `/40` opacity utilities report their real colour.
    const half: Rgba = parseColor("rgba(255, 0, 0, 0.5)");

    expect(half.r).toBeGreaterThan(250);
    expect(half.a).toBeCloseTo(0.5, 1);
  });
});

describe("contrastRatio", () => {
  it("gives 21:1 for black on white and 1:1 for a colour on itself", () => {
    const black: Rgba = parseColor("#000000");
    const white: Rgba = parseColor("#ffffff");

    expect(contrastRatio(black, white)).toBeCloseTo(21, 1);
    expect(contrastRatio(white, white)).toBeCloseTo(1, 5);
  });

  it("is symmetric — the order of the pair does not matter", () => {
    const a: Rgba = parseColor("#123456");
    const b: Rgba = parseColor("#abcdef");

    expect(contrastRatio(a, b)).toBeCloseTo(contrastRatio(b, a), 10);
  });

  it("reproduces the 1.27:1 pair that shipped", () => {
    // The dashboard nav's hovered label: accent-foreground on sidebar-accent in
    // light mode. A class-string assertion could not see this; a ratio can.
    const ratio: number = contrastRatio(
      parseColor("oklch(0.22 0.035 45)"),
      parseColor("oklch(0.30 0.03 45)"),
    );

    expect(ratio).toBeLessThan(1.5);
    expect(ratio).toBeLessThan(4.5);
  });
});

describe("over", () => {
  it("composites a translucent fill onto its backdrop", () => {
    const result: Rgba = over(parseColor("rgba(0, 0, 0, 0.5)"), parseColor("#ffffff"));

    expect(result.a).toBe(1);
    expect(result.r).toBeCloseTo(128, -1);
  });

  it("leaves an opaque colour untouched", () => {
    expect(over(parseColor("#ff0000"), parseColor("#ffffff"))).toEqual({
      r: 255,
      g: 0,
      b: 0,
      a: 1,
    });
  });
});

describe("relativeLuminance", () => {
  it("runs 0 for black to 1 for white", () => {
    expect(relativeLuminance(parseColor("#000000"))).toBeCloseTo(0, 5);
    expect(relativeLuminance(parseColor("#ffffff"))).toBeCloseTo(1, 5);
  });
});

describe("effectiveBackground", () => {
  it("skips transparent ancestors to find the surface that paints", async () => {
    await render(
      <div style={{ backgroundColor: "rgb(0, 0, 255)" }}>
        <div>
          <span data-testid="leaf">leaf</span>
        </div>
      </div>,
    );

    expect(effectiveBackground(page.getByTestId("leaf").element())).toEqual({
      r: 0,
      g: 0,
      b: 255,
      a: 1,
    });
  });

  it("composites a translucent surface onto the one beneath it", async () => {
    await render(
      <div style={{ backgroundColor: "rgb(255, 255, 255)" }}>
        <span data-testid="tinted" style={{ backgroundColor: "rgba(0, 0, 0, 0.5)" }}>
          tinted
        </span>
      </div>,
    );

    const surface: Rgba = effectiveBackground(page.getByTestId("tinted").element());

    expect(surface.a).toBe(1);
    expect(surface.r).toBeCloseTo(128, -1);
  });
});

describe("textContrast", () => {
  it("measures a label against the surface it is painted on", async () => {
    await render(
      <div style={{ backgroundColor: "rgb(255, 255, 255)" }}>
        <span data-testid="label" style={{ color: "rgb(0, 0, 0)" }}>
          label
        </span>
      </div>,
    );

    expect(textContrast(page.getByTestId("label").element())).toBeCloseTo(21, 1);
  });
});

describe("isTransparent", () => {
  it("names the theme-less failure mode", async () => {
    await render(<span data-testid="bare">bare</span>);

    // An element with no background paints nothing — which is exactly what a
    // `bg-sidebar` element does when the fork theme is not loaded.
    const bare: Element = page.getByTestId("bare").element();

    expect(isTransparent(computedColor(bare, "background-color"))).toBe(true);
  });
});
