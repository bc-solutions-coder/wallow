import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { forkBranding, toAppIconUrl } from "@bc-solutions-coder/styles";
import { describe, expect, it } from "vitest";

import { BASE_PATH } from "./base-path";
import { appIconUrl, forkResolvedBranding } from "./branding";

/**
 * This app's branding constants, bound to its base path (Wallow-8via).
 *
 * Node project: pure string work, no DOM.
 *
 * `@bc-solutions-coder/styles` ships a PREBUILT bundle, so it cannot read this
 * app's `import.meta.env.BASE_URL` — its own is frozen at "/". The prefix has to
 * be handed to it, which is what this module does, once, so no screen picks up
 * the package's unbased `appIconUrl` by accident and 404s the icon under
 * `AUTH_BASE_PATH=/auth`.
 *
 * The wiring assertions below are source-text checks for the same reason
 * `request-origin.test.ts` uses them: the value is baked at BUILD time, so a
 * default (`AUTH_BASE_PATH` unset) test run cannot tell a prefixed URL from an
 * unprefixed one at runtime — only that the prefix is threaded through.
 */

const libDir: string = dirname(fileURLToPath(import.meta.url));

function sourceOf(...segments: string[]): string {
  return readFileSync(resolve(libDir, "..", ...segments), "utf8");
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

describe("src/lib/branding.ts wiring", () => {
  const source: string = sourceOf("lib", "branding.ts");

  it("passes this build's base path into both", () => {
    expect(source).toMatch(/toAppIconUrl\(BASE_PATH\)/u);
    expect(source).toMatch(/resolveForkBranding\(BASE_PATH\)/u);
  });
});

describe("the screens that render branding", () => {
  it.each([
    ["the root route", ["routes", "__root.tsx"], "../lib/branding"],
    ["the auth layout", ["components", "auth-layout.tsx"], "../lib/branding"],
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
    expect(sourceOf("routes", "login.tsx")).toMatch(
      /mergeClientBranding\(forkBranding,\s*data,\s*BASE_PATH\)/u,
    );
  });
});
