import { describe, expect, it } from "vitest";

/**
 * Browser-mode smoke spec for Wallow-xzha.2.2 (wallow-web).
 *
 * This `.test.tsx` MUST execute in Vitest's browser project — real Chromium via
 * `provider: "playwright"` — with ZERO jsdom involvement. It deliberately does
 * NOT carry a `// @vitest-environment jsdom` pragma and asserts signals only a
 * genuine browser produces, so it fails if the multi-project split routes it to
 * node (no `window`) or to jsdom (fake userAgent, zero-size layout boxes).
 *
 * Today it FAILS: all four workspace vitest.config.ts files run `environment:
 * "node"` with no browser project, so this file is collected under node and has
 * no `window`/`document`. It goes green only once the browser project exists.
 */
describe("wallow-web browser-mode smoke", () => {
  it("runs inside a real Chromium window, not node or jsdom", () => {
    // node has no `document` at all; jsdom's navigator.userAgent contains "jsdom".
    expect(typeof document).toBe("object");
    expect(navigator.userAgent).toMatch(/Chrome|Chromium|HeadlessChrome/u);
  });

  it("has a real layout engine — jsdom reports every box as zero-sized", () => {
    const box: HTMLDivElement = document.createElement("div");
    box.style.width = "120px";
    box.style.height = "40px";
    document.body.append(box);

    const rect: DOMRect = box.getBoundingClientRect();
    expect(rect.width).toBeGreaterThan(0);
    expect(rect.height).toBeGreaterThan(0);

    box.remove();
  });
});
