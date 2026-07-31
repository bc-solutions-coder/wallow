import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * On-disk guard for the browser project's STYLING wiring. Node project
 * (`*.test.ts`), because it reads files rather than rendering anything.
 *
 * `theme-wiring.test.tsx` proves the CSS is present by measuring a rendered box
 * and colour. This file names the pieces that have to stay wired, so removing
 * one fails with a message saying WHICH, rather than as a pile of 15s
 * actionability timeouts (utilities) or transparent colours (theme).
 */
const appRoot = new URL("../../../", import.meta.url);

function readAppFile(relativePath: string): string {
  return readFileSync(fileURLToPath(new URL(relativePath, appRoot)), "utf8");
}

describe("browser project styling wiring", () => {
  it("registers wallowStyles() on the browser project", () => {
    const config: string = readAppFile("vitest.config.ts");

    expect(config).toContain("wallowStyles()");
    expect(config).toContain('browserSetupFiles: ["./vitest.setup.ts"]');
  });

  it("loads the Tailwind utilities and the fork theme from the setup file", () => {
    const setup: string = readAppFile("vitest.setup.ts");

    expect(setup).toContain('import "./vitest-styles.css"');
    // The theme half, as a VIRTUAL stylesheet rather than a JS import of
    // `@bc-solutions-coder/styles`: styles is a LINKED workspace package, so
    // importing it from a setup file re-optimizes the dep graph mid-run and
    // reloads the page with a second copy of the router.
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

  it("has no checkbox spec left working around a missing stylesheet", () => {
    // Without Tailwind a catalog checkbox root measures 0x0, and a spec works
    // around it by toggling with focus+Space under a "depends on no layout"
    // comment. With the stylesheet loaded, that workaround must not reappear.
    const specs: readonly string[] = [
      "src/features/accept-terms/components/AcceptTermsScreen.test.tsx",
      "src/features/register/components/RegisterForm.test.tsx",
      "src/features/login/components/LoginScreen.test.tsx",
      "src/features/login/components/OtpLoginForm.test.tsx",
    ];

    for (const spec of specs) {
      expect(readAppFile(spec), `${spec} still avoids layout`).not.toContain(
        "depends on no layout",
      );
    }
  });
});
