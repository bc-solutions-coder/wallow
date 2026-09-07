import { describe, expect, it } from "vitest";

/**
 * Declare browser tests that check Chromium globals and nonzero layout.
 * Call at module scope in a browser .test.tsx file. appName labels the suite.
 */
export function assertBrowserModeSmoke(appName: string): void {
  describe(`${appName} browser-mode smoke`, () => {
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
}
