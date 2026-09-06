import assert from "node:assert/strict";
import { after, before, describe, test } from "node:test";

import { chromium } from "playwright";

/**
 * Browser-level contrast checks for the built DocFX fork guide.
 * The test reads rendered elements and computed colors, never source text.
 */

const DOCFX_PAGE_URL = new URL(
  "../../../.docfx/_site/getting-started/fork-guide.html",
  import.meta.url,
);
const MINIMUM_CONTRAST = 4.5;
const CHANNEL_MAX = 255;
const LINEAR_THRESHOLD = 0.04045;
const LINEAR_DIVISOR = 12.92;
const GAMMA_OFFSET = 0.055;
const GAMMA_SCALE = 1.055;
const GAMMA_EXPONENT = 2.4;
const RED_WEIGHT = 0.2126;
const GREEN_WEIGHT = 0.7152;
const BLUE_WEIGHT = 0.0722;
const CONTRAST_FLARE = 0.05;
const ALPHA_CHANNEL_INDEX = 3;
const CONTRAST_PRECISION = 2;
const EXPECTED_ELEMENT_COUNT = 1;

let browser;
let page;

function channelLuminance(channel) {
  const value = channel / CHANNEL_MAX;

  return value <= LINEAR_THRESHOLD
    ? value / LINEAR_DIVISOR
    : ((value + GAMMA_OFFSET) / GAMMA_SCALE) ** GAMMA_EXPONENT;
}

function relativeLuminance([red, green, blue]) {
  return (
    RED_WEIGHT * channelLuminance(red) +
    GREEN_WEIGHT * channelLuminance(green) +
    BLUE_WEIGHT * channelLuminance(blue)
  );
}

function contrastRatio(foreground, background) {
  const [lighter, darker] = [relativeLuminance(foreground), relativeLuminance(background)].toSorted(
    (left, right) => right - left,
  );

  return (lighter + CONTRAST_FLARE) / (darker + CONTRAST_FLARE);
}

async function renderedTextContrast(locator, surfaceSelector) {
  const { foreground, background } = await locator.evaluate((element, selector) => {
    const surface = document.querySelector(selector);

    if (surface === null) {
      throw new Error(`DocFX contrast surface not found: ${selector}`);
    }

    const canvas = document.createElement("canvas");
    const pixelOrigin = 0;
    const pixelSize = 1;
    canvas.width = pixelSize;
    canvas.height = pixelSize;

    const context = canvas.getContext("2d", { willReadFrequently: true });

    if (context === null) {
      throw new Error("DocFX contrast test could not create a canvas context");
    }

    const resolveColor = (color, backdrop) => {
      context.clearRect(pixelOrigin, pixelOrigin, pixelSize, pixelSize);

      if (backdrop !== undefined) {
        context.fillStyle = backdrop;
        context.fillRect(pixelOrigin, pixelOrigin, pixelSize, pixelSize);
      }

      context.fillStyle = color;
      context.fillRect(pixelOrigin, pixelOrigin, pixelSize, pixelSize);

      return [...context.getImageData(pixelOrigin, pixelOrigin, pixelSize, pixelSize).data];
    };

    const foregroundColor = getComputedStyle(element).color;
    const backgroundColor = getComputedStyle(surface).backgroundColor;

    return {
      foreground: resolveColor(foregroundColor, backgroundColor),
      background: resolveColor(backgroundColor),
    };
  }, surfaceSelector);

  assert.equal(background[ALPHA_CHANNEL_INDEX], CHANNEL_MAX, "surface color must be opaque");

  return contrastRatio(foreground, background);
}

async function useTheme(theme) {
  await page.locator("html").evaluate((element, value) => {
    element.dataset.bsTheme = value;
  }, theme);
}

function requireWcagAa(ratio, label) {
  assert.ok(
    ratio >= MINIMUM_CONTRAST,
    `${label} contrast is ${ratio.toFixed(CONTRAST_PRECISION)}:1; expected at least ${MINIMUM_CONTRAST}:1`,
  );
}

function registerThemeTests(theme) {
  test(`${theme} inline code meets WCAG AA`, async (context) => {
    await useTheme(theme);
    const ratio = await renderedTextContrast(
      page.locator("article table tbody td:first-child code").first(),
      "body",
    );

    context.diagnostic(`${ratio.toFixed(CONTRAST_PRECISION)}:1`);
    requireWcagAa(ratio, `${theme} inline code`);
  });

  test(`${theme} navbar text meets WCAG AA`, async (context) => {
    await useTheme(theme);
    const ratio = await renderedTextContrast(
      page.locator('.navbar-nav .nav-link[data-docfx-contrast="true"]'),
      ".navbar",
    );

    context.diagnostic(`${ratio.toFixed(CONTRAST_PRECISION)}:1`);
    requireWcagAa(ratio, `${theme} navbar text`);
  });
}

describe("rendered DocFX theme contrast", () => {
  before(async () => {
    browser = await chromium.launch({ headless: true });
    page = await browser.newPage();
    await page.goto(DOCFX_PAGE_URL.href);

    const code = page.locator("article table tbody td:first-child code").first();
    assert.equal(
      await code.count(),
      EXPECTED_ELEMENT_COUNT,
      "fork guide must render a table-cell code element",
    );

    const navbar = page.locator(".navbar");
    assert.equal(await navbar.count(), EXPECTED_ELEMENT_COUNT, "fork guide must render one navbar");

    await navbar.evaluate((element) => {
      const items = document.createElement("ul");
      items.className = "navbar-nav";

      const item = document.createElement("li");
      item.className = "nav-item";

      const link = document.createElement("a");
      link.className = "nav-link";
      link.dataset.docfxContrast = "true";
      link.href = "#docfx-contrast-test";
      link.textContent = "Contrast test";

      item.append(link);
      items.append(item);
      element.append(items);
    });
  });

  after(async () => {
    await browser?.close();
  });

  registerThemeTests("light");
  registerThemeTests("dark");
});
