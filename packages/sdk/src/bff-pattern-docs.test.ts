import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

// packages/sdk/src -> repo root
const repoRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
const bffPatternDocPath: string = resolve(repoRoot, "docs/integrations/bff-pattern.md");

function readDoc(): string {
  return readFileSync(bffPatternDocPath, "utf8");
}

/**
 * The `## `-level section that names {@link CookieSessionStore}, or `null` when
 * the doc never names it.
 */
function sessionStoreSection(): string | null {
  const sections: string[] = readDoc().split(/^## /mu);
  return (
    sections.find((section: string): boolean => section.includes("CookieSessionStore")) ?? null
  );
}

// The cookie session store keeps the whole session in the browser's cookie, so
// it cannot be revoked server-side and is only ever a development default. That
// operational requirement has no compile-time enforcement, which is why it is
// pinned here as a documentation contract (bead Wallow-pu6a.1.6 / finding R6).
describe("docs/integrations/bff-pattern.md — session store guidance", () => {
  it("names both concrete SessionStore implementations the SDK ships", () => {
    const doc: string = readDoc();

    expect(doc).toContain("CookieSessionStore");
    expect(doc).toContain("ValkeySessionStore");
  });

  it("documents the cookie store as a dev default with Valkey required in production", () => {
    const section: string | null = sessionStoreSection();

    expect(section).not.toBeNull();
    expect(section).toMatch(/dev(elopment)?/iu);
    expect(section).toMatch(/production/iu);
    expect(section).toContain("ValkeySessionStore");
  });

  it("explains that a cookie-store session cannot be revoked server-side", () => {
    const section: string | null = sessionStoreSection();

    expect(section).toMatch(/destroy\(\)/u);
    expect(section).toMatch(/revok/iu);
  });
});
