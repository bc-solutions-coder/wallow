import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * On-disk guard for the browser project's STYLING wiring.
 *
 * The sibling `theme-wiring.test.tsx` measures a rendered colour, which is the
 * assertion that matters. This file names the three pieces that have to stay
 * wired, so removing one fails with a message saying WHICH, rather than a pile
 * of transparent-colour failures whose cause has to be rediscovered.
 */
const appRoot = new URL("../../../", import.meta.url);

function readAppFile(relativePath: string): string {
  return readFileSync(fileURLToPath(new URL(relativePath, appRoot)), "utf8");
}

describe("browser project styling wiring", () => {
  it("registers wallowStyles() on the browser project", () => {
    const config: string = readAppFile("vitest.config.ts");

    // The plugin pair that compiles ./vitest-styles.css AND serves the virtual
    // fork theme; without it neither import in the setup file resolves.
    expect(config).toContain("wallowStyles()");
    expect(config).toContain('browserSetupFiles: ["./vitest.setup.ts"]');
  });

  it("loads the Tailwind utilities and the fork theme from the setup file", () => {
    const setup: string = readAppFile("vitest.setup.ts");

    expect(setup).toContain('import "./vitest-styles.css"');
    // The theme arrives as a virtual stylesheet rather than a JS import of
    // `@bc-solutions-coder/styles`: styles is a LINKED workspace package, so
    // importing it here re-optimizes the dep graph mid-run, reloads the page and
    // hands the specs a SECOND `@tanstack/react-router` copy.
    expect(setup).toContain('import "virtual:wallow-theme.css"');
    // Matched as a real import statement rather than a bare substring, because
    // the comments here name the forbidden package and must stay legal.
    expect(setup).not.toMatch(/^\s*import\s.*@bc-solutions-coder\/styles/mu);
  });

  it("keeps the Tailwind entry at the app root, where its @source resolves", () => {
    // Tailwind v4 resolves `@source` relative to the DECLARING stylesheet, so
    // this entry cannot be hoisted into a shared package — a scan declared there
    // would see that package's files. This is why only the theme half is shared.
    const entry: string = readAppFile("vitest-styles.css");

    expect(entry).toContain('@source "./src"');
  });
});
