import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The "not unstyled HTML" half of adopting `@bc-solutions-coder/styles`
 * (Wallow-ffpq.3.4). Unlike wallow-auth — whose components were already
 * Tailwind-authored and only lacked a stylesheet — wallow-web has NO className
 * anywhere, so wiring the CSS pipeline alone would still render bare markup.
 *
 * These guards pin the backbone of the ported routes: the dashboard layout
 * shell, its nav, and the public home route must carry `className` props that
 * consume the shared `@theme` tokens, so real Tailwind-derived styling actually
 * reaches the screen. The bar is "ported + reachable" styling, not final visual
 * polish — the presence of styling hooks, not any specific class.
 */

const srcDir: URL = new URL("./", import.meta.url);

function source(relativePath: string): string {
  return readFileSync(fileURLToPath(new URL(relativePath, srcDir)), "utf8");
}

describe("wallow-web ported markup carries styling hooks", () => {
  it("styles the dashboard layout shell", () => {
    expect(source("components/DashboardLayout.tsx")).toContain("className");
  });

  it("styles the dashboard navigation", () => {
    expect(source("components/DashboardNav.tsx")).toContain("className");
  });

  it("styles the public home route", () => {
    expect(source("routes/index.tsx")).toContain("className");
  });
});
