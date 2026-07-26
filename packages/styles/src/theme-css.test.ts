import { readFileSync } from "node:fs";

import { describe, expect, it } from "vitest";

import { forkBranding, toCssVars } from "./branding";

/**
 * The package's two halves have to agree:
 *
 *  - `styles.css` is the shared Tailwind v4 entry, the sole owner of the
 *    `@theme` block. (It was lifted token-for-token from the deleted Blazor
 *    `Wallow.Auth` stylesheet — that migration gate lived here until the
 *    oracle was deleted with the app; the move is pinned in git history.)
 *  - `branding.ts` is the palette. `styles.css` maps Tailwind tokens onto plain
 *    custom properties (`--color-primary: var(--primary)`) but must never
 *    *define* them — `api/branding.json` is the only place a fork edits to
 *    rebrand, so the values are emitted at render time from the JSON instead.
 *
 * These tests read the files off disk rather than through a bundler because the
 * CSS ships as-authored: consumers `@import` it and run their own Tailwind pass.
 */
const packageRoot: URL = new URL("../", import.meta.url);

function read(url: URL): string {
  return readFileSync(url, "utf8");
}

/**
 * Pull the `--token: value` declarations out of a stylesheet's `@theme` block.
 * Comparing parsed tokens rather than raw text lets the shared entry re-indent
 * or re-comment freely while still pinning every token's name and value.
 */
function themeTokens(css: string): Record<string, string> {
  const block: RegExpMatchArray | null = css.match(/@theme\s*\{(?<body>[^}]*)\}/u);
  if (block?.groups === undefined) {
    throw new Error("stylesheet has no @theme block");
  }

  const tokens: Record<string, string> = {};
  for (const declaration of block.groups["body"].split(";")) {
    const parsed: RegExpMatchArray | null = declaration.match(
      /^\s*(?<name>--[\w-]+)\s*:\s*(?<value>.+?)\s*$/su,
    );
    if (parsed?.groups !== undefined) {
      tokens[parsed.groups["name"]] = parsed.groups["value"];
    }
  }
  return tokens;
}

/** The custom properties a `@theme` block indirects through `var(...)`. */
function themedVarNames(tokens: Record<string, string>): readonly string[] {
  const matches: RegExpStringIterator<RegExpExecArray> = Object.values(tokens)
    .join(" ")
    .matchAll(/var\((?<name>--[\w-]+)\)/gu);
  return [...new Set([...matches].map((match): string => match.groups!["name"]))];
}

const sharedCss: string = read(new URL("styles.css", packageRoot));

describe("the shared Tailwind entry", () => {
  it("pulls in Tailwind so consumers import the framework from one place", () => {
    expect(sharedCss).toMatch(/@import\s+"tailwindcss";/u);
  });

  it("has a @theme block for the guard below to pin", () => {
    expect(Object.keys(themeTokens(sharedCss)).length).toBeGreaterThan(0);
  });

  it("leaves @source scanning to the consuming app", () => {
    // A package cannot see a consumer's source files, so an @source line here
    // would either be dead or, worse, scan this package instead of the app —
    // which builds clean and ships a stylesheet that styles nothing. Each app
    // @sources its own directory after importing this entry.
    expect(sharedCss).not.toMatch(/@source/u);
  });

  it("hardcodes no palette, so api/branding.json stays the only place to rebrand", () => {
    // The Blazor entry inlined a :root/.dark palette — Wallow.Auth's was a
    // verbatim duplicate of branding.json's. It does not come along.
    expect(sharedCss).not.toMatch(/oklch\(/u);
    expect(sharedCss).not.toMatch(/^\s*(?::root|\.dark)\s*\{/mu);
  });
});

describe("the branding palette", () => {
  const themedVars: readonly string[] = themedVarNames(themeTokens(sharedCss));

  it("is what the @theme tokens indirect through", () => {
    // Guards the guard: if the token block stopped using var(), the two tests
    // below would pass vacuously.
    expect(themedVars.length).toBeGreaterThan(0);
  });

  it("defines every custom property the @theme block maps, in light mode", () => {
    expect(Object.keys(toCssVars(forkBranding.theme.light))).toEqual(
      expect.arrayContaining([...themedVars]),
    );
  });

  it("defines every custom property the @theme block maps, in dark mode", () => {
    expect(Object.keys(toCssVars(forkBranding.theme.dark))).toEqual(
      expect.arrayContaining([...themedVars]),
    );
  });

  // The two tests above pin one direction (every var the @theme block reaches
  // for is defined by the palette); these pin the reverse (every var the palette
  // *emits* is reached for by the @theme block). Without it, adding a colour to
  // api/branding.json's theme would ship a custom property that renderThemeStyle
  // writes onto :root but no Tailwind token ever consumes — a silently dead
  // token. The recipe is two files in lockstep: a new semantic colour touches
  // api/branding.json's theme AND styles.css's @theme, nothing per-app.
  const referencesEveryEmittedVar = (mode: "light" | "dark"): void => {
    const emitted: readonly string[] = Object.keys(toCssVars(forkBranding.theme[mode]));
    const unmapped: readonly string[] = emitted.filter(
      (name: string): boolean => !themedVars.includes(name),
    );
    expect(unmapped).toEqual([]);
  };

  it("maps every custom property forkBranding.theme emits, in light mode", () => {
    referencesEveryEmittedVar("light");
  });

  it("maps every custom property forkBranding.theme emits, in dark mode", () => {
    referencesEveryEmittedVar("dark");
  });
});

describe("the package contract", () => {
  const manifest: { exports: Record<string, unknown>; files: string[] } = JSON.parse(
    read(new URL("package.json", packageRoot)),
  );

  it("exports the CSS entry at a subpath consumers can @import", () => {
    expect(manifest.exports["./styles.css"]).toBe("./styles.css");
  });

  it("ships the CSS entry, which no build step produces", () => {
    // styles.css is authored, not emitted: `vite build` only makes dist/, so an
    // exports map pointing at a file outside `files` would publish a broken
    // package.
    expect(manifest.files).toContain("styles.css");
  });
});
