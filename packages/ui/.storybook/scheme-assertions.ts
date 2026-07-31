import {
  computedColor,
  isTransparent,
  relativeLuminance,
  type Rgba,
} from "@bc-solutions-coder/testing/contrast";
import { expect } from "storybook/test";

/*
 * Wallow-lrlm.11 — the assertion a scheme-scoped story makes about ITSELF: the
 * scheme it is named for is the scheme it actually paints.
 *
 * WHY THIS EXISTS. Six story files (`empty-state`, `list-card`, `list-row`,
 * `page-header`, `text`, `theme-toggle`) used to scope a scheme with a wrapper
 * `<div className="dark">` that asserted nothing about colour, so every story
 * named *Dark rendered the LIGHT palette in the explorer and in the `storybook`
 * Vitest project with nothing to say so. They now stamp `document.documentElement`
 * via the `scheme-decorators.tsx` pair instead, which is the reference pattern to
 * copy. A wrapper cannot work: `renderThemeStyle` emits `:root` / `.dark` / `.light`
 * blocks carrying the RAW variables (`--background`, `--foreground`, …) while `styles.css`'s
 * `@theme` declares the TOKEN (`--color-background: var(--background)`) on
 * `:root` ALONE — and a `var()` inside a custom property is substituted at
 * computed-value time on the DECLARING element. A descendant `.dark` rebinds the
 * raw variable; the token above it keeps the light value it already computed,
 * and that stale value is what inherits down into every utility. Only the class
 * on `document.documentElement` puts both blocks on the same element.
 *
 * WHY IT MEASURES, AND WHERE. Nothing about this is visible in a class string —
 * the markup under a broken wrapper is byte-identical to the markup under a
 * working one; only the painted colour differs. So this reads colour back, and
 * it can only do that in the `storybook` project: the `browser` project loads no
 * Tailwind, so `--color-background` is not declared there at all and every
 * reading would be `rgba(0, 0, 0, 0)`. The preview supplies both halves —
 * preview.css compiles `@theme`, preview.tsx inlines `renderThemeStyle` for the
 * fork's real branding.
 *
 * WHY A CANVAS, NOT A REGEX. The fork palette is `oklch(...)` and Chromium keeps
 * that in the computed value, so an `rgb()` regex silently misses. The shared
 * `@bc-solutions-coder/testing/contrast` helpers paint the string into a 2d
 * context and read sRGB bytes back, which normalises any CSS colour syntax.
 *
 * WHY NO HARD-CODED COLOURS. api/branding.json is fork config; a fork that
 * repaints its palette must not have to edit assertions here. Both criteria
 * below are relations, not values.
 */

/** The two schemes a story can scope itself to. */
export type SchemeName = "dark" | "light";

/** Decimal places the failure message reports a relative luminance to. */
const LUMINANCE_DIGITS = 3;

/**
 * The slice of a Storybook play context this assertion needs. Declared
 * structurally so the returned function drops straight into `play` without
 * naming Storybook's renderer-parameterised context type.
 */
interface SchemeCanvas {
  /** The container Storybook renders the decorated story into. */
  canvasElement: HTMLElement;
}

/**
 * Read one CSS value as the browser resolves it AT `host`, by painting it onto a
 * throwaway probe parented there.
 *
 * The probe has to live inside the story's own scheme scope: a `.dark` wrapper
 * rebinds custom properties for its DESCENDANTS, so a probe parented anywhere
 * above it would read the document's values and could not tell the two
 * implementations apart.
 */
function paintedAt(host: HTMLElement, value: string): Rgba {
  const probe: HTMLDivElement = document.createElement("div");
  probe.style.backgroundColor = value;
  host.append(probe);

  try {
    return computedColor(probe, "background-color");
  } finally {
    probe.remove();
  }
}

/**
 * The innermost element the scheme decorator renders, i.e. the deepest point
 * whose colours are the ones the story is claiming to show.
 *
 * Located by the surface class every one of these decorators paints rather than
 * by the scheme class itself, so it keeps finding the right element once the
 * scheme moves off the wrapper and onto `document.documentElement`. With no
 * wrapper at all the canvas is the honest fallback — under a correct
 * implementation the whole document carries the scheme.
 */
function schemeHost(canvasElement: HTMLElement): HTMLElement {
  return canvasElement.querySelector<HTMLElement>(".bg-background") ?? canvasElement;
}

/**
 * A `play` that asserts the story paints `scheme`.
 *
 * Two criteria, both relations rather than values:
 *
 *  1. THE PALETTE IS THE RIGHT WAY UP. Dark mode is light text on a dark ground,
 *     so the background's relative luminance sits BELOW the foreground's; light
 *     mode is the reverse. This is what fails today for every *Dark story: under
 *     a wrapper they paint the light background and the dark foreground.
 *  2. THE TOKEN LAYER AGREES WITH THE RAW LAYER. `--color-background` is defined
 *     as `var(--background)`, so inside a correctly scoped scheme the two paint
 *     the same colour. They disagree exactly when a block rebound the raw
 *     variable somewhere the token could not see — the mechanism behind the
 *     first criterion, stated in the one place it is directly visible.
 *
 * Preceded by a tripwire, because a theme-less page makes every reading
 * transparent and would let both criteria pass for the wrong reason.
 */
export function expectScheme(scheme: SchemeName): (context: SchemeCanvas) => Promise<void> {
  return async ({ canvasElement }: SchemeCanvas): Promise<void> => {
    const host: HTMLElement = schemeHost(canvasElement);
    const background: Rgba = paintedAt(host, "var(--color-background)");
    const foreground: Rgba = paintedAt(host, "var(--color-foreground)");
    const rawBackground: Rgba = paintedAt(host, "var(--background)");

    await expect(
      isTransparent(background),
      "--color-background paints nothing — is the fork theme loaded?",
    ).toBe(false);
    await expect(
      isTransparent(foreground),
      "--color-foreground paints nothing — is the fork theme loaded?",
    ).toBe(false);

    // Criterion 1: the palette is the right way up for the scheme claimed.
    const backgroundLuminance: number = relativeLuminance(background);
    const foregroundLuminance: number = relativeLuminance(foreground);
    const paintsDarkPalette: boolean = backgroundLuminance < foregroundLuminance;
    const measured: string =
      `background ${backgroundLuminance.toFixed(LUMINANCE_DIGITS)}, ` +
      `foreground ${foregroundLuminance.toFixed(LUMINANCE_DIGITS)}`;

    await expect(
      paintsDarkPalette,
      `this story paints the ${paintsDarkPalette ? "DARK" : "LIGHT"} palette while ` +
        `claiming ${scheme} (relative luminance: ${measured})`,
    ).toBe(scheme === "dark");

    // Criterion 2: the token still resolves to the raw variable in force here.
    // The mechanism behind criterion 1, and the reading that names the fix: the
    // two disagree exactly when a block rebound the raw variable below the
    // element the token was computed on.
    await expect(
      background,
      `--color-background and --background disagree, so the ${scheme} scheme was ` +
        "selected somewhere the token layer cannot see — the class belongs on " +
        "document.documentElement",
    ).toEqual(rawBackground);
  };
}
