/**
 * On-disk guard for a consumer's browser-project STYLING wiring.
 *
 * The rendered-colour half is `./theme-wiring`, and it is the assertion that
 * matters. This one names the pieces that have to stay wired, so removing one
 * fails with a message saying WHICH, rather than as a pile of 15s actionability
 * timeouts (no utilities) or transparent colours (no theme).
 *
 * Node-project only: it reads files and renders nothing.
 */
import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

/** The workaround string a spec grows when the stylesheet is missing. */
const LAYOUT_WORKAROUND = "depends on no layout";

/** Options for {@link assertBrowserStylesWiring}. */
export interface BrowserStylesWiringOptions {
  /** Absolute path to the app root — the directory holding `vitest.config.ts`. */
  appDir: string;
  /**
   * App-relative spec paths that must not have grown a
   * `"depends on no layout"` workaround. Omit when the app has none: the extra
   * case is only emitted for a non-empty list, so an empty one cannot pass
   * vacuously.
   */
  extraSpecs?: readonly string[];
}

/**
 * Declare the shared `describe` block asserting this app's browser project is
 * wired for real CSS.
 *
 * @param options See {@link BrowserStylesWiringOptions}.
 */
export function assertBrowserStylesWiring(options: BrowserStylesWiringOptions): void {
  const read = (relativePath: string): string =>
    readFileSync(join(options.appDir, relativePath), "utf8");

  describe("browser project styling wiring", () => {
    it("registers wallowStyles() on the browser project", () => {
      const config: string = read("vitest.config.ts");

      // The plugin pair that compiles ./vitest-styles.css AND serves the virtual
      // fork theme; without it neither import in the setup file resolves.
      expect(config).toContain("wallowStyles()");
      expect(config).toContain('browserSetupFiles: ["./vitest.setup.ts"]');
    });

    it("loads the Tailwind utilities and the fork theme from the setup file", () => {
      const setup: string = read("vitest.setup.ts");

      expect(setup).toContain('import "./vitest-styles.css"');
      // The theme arrives as a virtual stylesheet rather than a JS import of
      // `@bc-solutions-coder/styles`: styles is a LINKED workspace package, so
      // importing it here re-optimizes the dep graph mid-run, reloads the page
      // and hands the specs a SECOND `@tanstack/react-router` copy.
      expect(setup).toContain('import "virtual:wallow-theme.css"');
      // Matched as a real import statement rather than a bare substring, because
      // the setup file's own comment names the forbidden package and must stay
      // legal.
      expect(setup).not.toMatch(/^\s*import\s.*@bc-solutions-coder\/styles/mu);
    });

    it("keeps the Tailwind entry at the app root, where its @source resolves", () => {
      // Tailwind v4 resolves `@source` relative to the DECLARING stylesheet, so
      // this entry cannot be hoisted into a shared package — a scan declared
      // there would see that package's files. This is why only the theme half is
      // shared.
      expect(read("vitest-styles.css")).toContain('@source "./src"');
    });

    const extraSpecs: readonly string[] = options.extraSpecs ?? [];

    it.skipIf(extraSpecs.length === 0)(
      "has no checkbox spec left working around a missing stylesheet",
      () => {
        // Without Tailwind a catalog checkbox root measures 0x0, and a spec works
        // around it by toggling with focus+Space under a "depends on no layout"
        // comment. With the stylesheet loaded, that workaround must not reappear.
        for (const spec of extraSpecs) {
          expect(read(spec), `${spec} still avoids layout`).not.toContain(LAYOUT_WORKAROUND);
        }
      },
    );
  });
}
