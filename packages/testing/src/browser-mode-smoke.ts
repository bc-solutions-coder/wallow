import { describe, expect, it } from "vitest";

/**
 * A consumer's browser project really is real Chromium.
 *
 * Asserts signals only a genuine browser produces, so it fails if the multi-project
 * split routes the spec onto node (no `document`) or if jsdom/happy-dom is ever
 * reintroduced (fake userAgent, zero-sized layout boxes). Both are banned repo-wide
 * — see `.claude/rules/TESTING.md`.
 *
 * BROWSER-ONLY. Keep it off the barrel, which is loaded in plain Node at config time.
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
