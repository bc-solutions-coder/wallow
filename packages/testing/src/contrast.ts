/**
 * Browser-only computed-color and contrast helpers.
 * A canvas converts CSS color syntax to pixel data. Use opaque inputs for reliable
 * byte-range channels; parseColor documents the current translucent-input limitation.
 */

/** The 0-255 range one sRGB channel comes back in. */
const CHANNEL_MAX = 255;

/** Fully opaque / fully transparent alpha. */
const OPAQUE = 1;
const TRANSPARENT = 0;

/**
 * WCAG 2.x sRGB-to-linear constants (the `channelLuminance` piecewise curve) and
 * the relative-luminance channel weights, verbatim from the spec's definition of
 * contrast ratio. Named rather than inline so the arithmetic below reads as the
 * formula it is.
 */
const LINEAR_THRESHOLD = 0.04045;
const LINEAR_DIVISOR = 12.92;
const GAMMA_OFFSET = 0.055;
const GAMMA_SCALE = 1.055;
const GAMMA_EXPONENT = 2.4;
const RED_WEIGHT = 0.2126;
const GREEN_WEIGHT = 0.7152;
const BLUE_WEIGHT = 0.0722;

/** The spec's flare term, which keeps the ratio finite for pure black. */
const CONTRAST_FLARE = 0.05;

/** RGB channels on a nominal 0-255 scale and alpha. parseColor can exceed that scale for translucent input. */
export interface Rgba {
  /** Red channel on the 0-255 scale. */
  r: number;
  /** Green channel on the 0-255 scale. */
  g: number;
  /** Blue channel on the 0-255 scale. */
  b: number;
  /** Alpha, 0-1. */
  a: number;
}

/** The single pixel every parse paints into and reads back. */
const PIXEL_ORIGIN = 0;
const PIXEL_SIZE = 1;

/** Lazily created 1x1 scratch canvas; every parse paints into this one context. */
let scratch: CanvasRenderingContext2D | null = null;

function scratchContext(): CanvasRenderingContext2D {
  if (scratch === null) {
    const canvas = document.createElement("canvas");
    canvas.width = PIXEL_SIZE;
    canvas.height = PIXEL_SIZE;
    // `willReadFrequently` keeps the readback on the CPU path; without it every
    // `getImageData` round-trips the GPU and a per-row contrast loop crawls.
    const context = canvas.getContext("2d", { willReadFrequently: true });

    if (context === null) {
      throw new Error("contrast: could not obtain a 2d canvas context");
    }

    scratch = context;
  }

  return scratch;
}

/**
 * Read a CSS color through a browser canvas.
 *
 * Invalid CSS retains the black fill rather than throwing. For translucent colors,
 * the current implementation divides the canvas channels by alpha without clamping,
 * so returned RGB values can exceed 255. Prefer opaque inputs for contrast assertions.
 * Throws if the canvas context or pixel data cannot be obtained.
 */
export function parseColor(value: string): Rgba {
  const context = scratchContext();

  context.clearRect(PIXEL_ORIGIN, PIXEL_ORIGIN, PIXEL_SIZE, PIXEL_SIZE);
  // A canvas keeps its previous `fillStyle` when handed an invalid colour, so
  // seed black before assigning. Invalid strings therefore return black.
  context.fillStyle = "#000000";
  context.fillStyle = value;
  context.fillRect(PIXEL_ORIGIN, PIXEL_ORIGIN, PIXEL_SIZE, PIXEL_SIZE);

  const [r, g, b, alpha] = context.getImageData(
    PIXEL_ORIGIN,
    PIXEL_ORIGIN,
    PIXEL_SIZE,
    PIXEL_SIZE,
  ).data;

  if (r === undefined || g === undefined || b === undefined || alpha === undefined) {
    throw new Error(`contrast: could not read back the colour "${value}"`);
  }

  const a: number = alpha / CHANNEL_MAX;

  return {
    // This alpha division can produce channels above 255 for translucent input.
    // Fully transparent input returns zero channels.
    r: a === TRANSPARENT ? TRANSPARENT : Math.round(r / a),
    g: a === TRANSPARENT ? TRANSPARENT : Math.round(g / a),
    b: a === TRANSPARENT ? TRANSPARENT : Math.round(b / a),
    a,
  };
}

/** The computed value of `property` on `element`, parsed to sRGB bytes. */
export function computedColor(element: Element, property: string): Rgba {
  return parseColor(globalThis.getComputedStyle(element).getPropertyValue(property));
}

/** True when the colour paints nothing at all — the theme-less failure mode. */
export function isTransparent(color: Rgba): boolean {
  return color.a === TRANSPARENT;
}

/**
 * Composite a (possibly translucent) colour over an opaque backdrop, so a
 * `bg-destructive/10` fill is measured as what the eye actually sees rather than
 * as its authored colour.
 */
export function over(color: Rgba, backdrop: Rgba): Rgba {
  const inverse: number = OPAQUE - color.a;

  return {
    r: Math.round(color.r * color.a + backdrop.r * inverse),
    g: Math.round(color.g * color.a + backdrop.g * inverse),
    b: Math.round(color.b * color.a + backdrop.b * inverse),
    a: OPAQUE,
  };
}

/** WCAG 2.x relative luminance of one 0-255 channel. */
function channelLuminance(channel: number): number {
  const c: number = channel / CHANNEL_MAX;

  return c <= LINEAR_THRESHOLD
    ? c / LINEAR_DIVISOR
    : ((c + GAMMA_OFFSET) / GAMMA_SCALE) ** GAMMA_EXPONENT;
}

/** WCAG 2.x relative luminance. Alpha is ignored — composite first with {@link over}. */
export function relativeLuminance(color: Rgba): number {
  return (
    RED_WEIGHT * channelLuminance(color.r) +
    GREEN_WEIGHT * channelLuminance(color.g) +
    BLUE_WEIGHT * channelLuminance(color.b)
  );
}

/**
 * WCAG 2.x contrast ratio between two OPAQUE colours, from 1 (identical) to 21
 * (black on white). Composite any translucent input with {@link over} first.
 */
export function contrastRatio(a: Rgba, b: Rgba): number {
  const [lighter, darker] = [relativeLuminance(a), relativeLuminance(b)].toSorted(
    (x: number, y: number): number => y - x,
  ) as [number, number];

  return (lighter + CONTRAST_FLARE) / (darker + CONTRAST_FLARE);
}

/**
 * The nearest ancestor background an element is actually painted against —
 * skipping the transparent ancestors that carry no fill — falling back to white,
 * the page canvas.
 *
 * A theme-less page makes EVERY ancestor transparent, so this walks to the
 * fallback; that is the collapse these specs exist to catch, so callers should
 * assert the surface they care about is non-transparent rather than trusting
 * this to have found it.
 */
export function effectiveBackground(element: Element): Rgba {
  let node: Element | null = element;

  while (node !== null) {
    const color: Rgba = computedColor(node, "background-color");

    if (color.a === OPAQUE) {
      return color;
    }

    if (color.a > TRANSPARENT) {
      return over(color, effectiveBackground(node.parentElement ?? document.body));
    }

    node = node.parentElement;
  }

  return { r: CHANNEL_MAX, g: CHANNEL_MAX, b: CHANNEL_MAX, a: OPAQUE };
}

/**
 * Contrast ratio of an element's TEXT against the surface it is painted on,
 * resolving translucency on both sides.
 */
export function textContrast(element: Element, surface: Element = element): number {
  const background: Rgba = effectiveBackground(surface);

  return contrastRatio(over(computedColor(element, "color"), background), background);
}
