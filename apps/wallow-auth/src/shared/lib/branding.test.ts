import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { forkBranding, toAppIconUrl } from "@bc-solutions-coder/styles";
import { describe, expect, it } from "vitest";

import { BASE_PATH } from "./base-path";
import { appIconUrl, forkResolvedBranding } from "./branding";

/**
 * This app's branding constants, bound to its base path. Node project: pure
 * string work, no DOM.
 *
 * `@bc-solutions-coder/styles` ships a PREBUILT bundle, so it cannot read this
 * app's `import.meta.env.BASE_URL` — its own is frozen at "/", and a screen that
 * picks up the package's unbased `appIconUrl` 404s the icon under a base path.
 * The wiring assertions are source-text checks because the value is baked at
 * BUILD time: a default run cannot tell a prefixed URL from an unprefixed one.
 */

const libDir: string = dirname(fileURLToPath(import.meta.url));

/** `src/`, from `src/shared/lib/` — every path below is spelled zone-first. */
const srcDir: string = resolve(libDir, "..", "..");

function sourceOf(...segments: string[]): string {
  return readFileSync(resolve(srcDir, ...segments), "utf8");
}

/** The `@bc-solutions-coder/styles` import statement of a source file, if any. */
function stylesImportIn(source: string): string {
  return source.match(/import\s*\{[^}]*\}\s*from\s*"@bc-solutions-coder\/styles";/u)?.[0] ?? "";
}

describe("appIconUrl", () => {
  it("serves the fork's icon from under this build's base path", () => {
    expect(appIconUrl).toBe(toAppIconUrl(BASE_PATH));
  });

  it("is unchanged under the default build, where there is no prefix", () => {
    expect(appIconUrl).toBe(`/${forkBranding.appIcon}`);
  });
});

describe("forkResolvedBranding", () => {
  it("resolves the fork's own branding with its icon under this build's base path", () => {
    expect(forkResolvedBranding.logoUrl).toBe(toAppIconUrl(BASE_PATH));
  });

  it("still carries the fork identity the layout renders", () => {
    expect(forkResolvedBranding.name).toBe(forkBranding.appName);
    expect(forkResolvedBranding.tagline).toBe(forkBranding.tagline);
  });
});

describe("src/shared/lib/branding.ts wiring", () => {
  const source: string = sourceOf("shared", "lib", "branding.ts");

  it("passes this build's base path into both", () => {
    expect(source).toMatch(/toAppIconUrl\(BASE_PATH\)/u);
    expect(source).toMatch(/resolveForkBranding\(BASE_PATH\)/u);
  });
});

describe("the screens that render branding", () => {
  // Two specifiers for one module, and both are correct. The root route is in the
  // `app` zone and reaches `shared` by ALIAS, which is the only legal shape for a
  // cross-zone import; the auth layout lives in `shared` beside this file and
  // reaches it relatively, which is the only legal shape WITHIN a zone.
  it.each([
    ["the root route", ["app", "routes", "__root.tsx"], "@shared/lib/branding"],
    ["the auth layout", ["shared", "components", "auth-layout.tsx"], "../lib/branding"],
  ])(
    "has %s take its branding from this module, not the package",
    (_label: string, segments: string[], specifier: string) => {
      const source: string = sourceOf(...segments);

      expect(source).toContain(`from "${specifier}"`);
      // The package's own constants are the unbased ones; importing them here is
      // exactly the bug.
      expect(stylesImportIn(source)).not.toMatch(/appIconUrl|forkResolvedBranding/u);
    },
  );

  it("has the login route overlay client branding on top of the based fork branding", () => {
    expect(sourceOf("app", "routes", "login.tsx")).toMatch(
      /mergeClientBranding\(forkBranding,\s*data,\s*BASE_PATH\)/u,
    );
  });
});
