import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * On-disk guard for the browser project's STYLING wiring (Wallow-8ytl), the
 * same shape `packages/ui/src/core/storybook-setup.test.ts` uses for Storybook's
 * theme decorator.
 *
 * `theme-wiring.test.tsx` proves the theme is present by measuring a rendered
 * colour, which is the assertion that matters. This file is the revert-proofing
 * underneath it: it names the three pieces that have to stay wired, so a change
 * that removes one fails with a message saying WHICH, instead of a pile of
 * transparent-colour failures whose cause has to be rediscovered.
 *
 * Runs on the NODE project (`*.test.ts`), because it reads files rather than
 * rendering anything.
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
    // The theme half. It arrives as a virtual stylesheet rather than a JS import
    // of `@bc-solutions-coder/styles` on purpose: styles is a LINKED workspace
    // package, and importing it here re-optimizes the dep graph mid-run, reloads
    // the page and hands the specs a SECOND `@tanstack/react-router` copy — which
    // `src/app/routes/index.gate.test.tsx` catches through `isRedirect`.
    expect(setup).toContain('import "virtual:wallow-theme.css"');
    // ...and NOT by importing the styles package. Matched as a real import
    // statement, not as a bare substring: this file's own comment explains why
    // the JS import is forbidden, and naming the symbol must stay allowed.
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
