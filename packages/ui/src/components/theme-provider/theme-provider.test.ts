import { describe, expect, it } from "vitest";

import {
  resolveThemeMode,
  THEME_STORAGE_KEY,
  themeInitScript,
  type ThemeMode,
} from "./theme-provider";

/*
 * Wallow-lrlm.1.2 — the PURE half of theme activation. This is a `*.test.ts`, so
 * it runs in the vitest NODE project (`src/**\/*.test.ts`), which is exactly
 * what makes the resolution order provable: `prefers-color-scheme` cannot be
 * emulated from a vitest browser spec, so the precedence table would otherwise
 * only ever be exercised at whatever colour scheme the CI machine happens to
 * report. Feeding `resolveThemeMode` its three inputs directly covers all of it.
 *
 * The order under test, lowest priority first (the bead's acceptance criterion):
 *
 *   1. `packages/styles/branding.json`'s `theme.defaultMode`   — the fork's own choice
 *   2. `prefers-color-scheme`                       — what the OS asks for
 *   3. the visitor's persisted `localStorage` value — what they clicked
 *
 * `systemMode: null` is the OS stating NO preference (neither media query
 * matches). That case is the only thing that gives `defaultMode` a job, so it is
 * pinned rather than folded into "light".
 */

/** Every input combination, as a table, so the precedence is read as one block. */
const PRECEDENCE_TABLE: {
  readonly stored: string | null;
  readonly systemMode: ThemeMode | null;
  readonly defaultMode: ThemeMode;
  readonly expected: ThemeMode;
}[] = [
  // A stored choice wins over both weaker signals, in both directions.
  { stored: "dark", systemMode: "light", defaultMode: "light", expected: "dark" },
  { stored: "light", systemMode: "dark", defaultMode: "dark", expected: "light" },
  // The OS wins over the fork default, in both directions.
  { stored: null, systemMode: "dark", defaultMode: "light", expected: "dark" },
  { stored: null, systemMode: "light", defaultMode: "dark", expected: "light" },
  // Nothing stored, OS silent: the fork default decides.
  { stored: null, systemMode: null, defaultMode: "dark", expected: "dark" },
  { stored: null, systemMode: null, defaultMode: "light", expected: "light" },
  // A stored "system" is an explicit request to follow the OS, so it hands the
  // decision DOWN a level rather than pinning a mode.
  { stored: "system", systemMode: "dark", defaultMode: "light", expected: "dark" },
  { stored: "system", systemMode: null, defaultMode: "light", expected: "light" },
];

describe("resolveThemeMode", () => {
  it.each(PRECEDENCE_TABLE)(
    "resolves stored=$stored system=$systemMode default=$defaultMode to $expected",
    ({ stored, systemMode, defaultMode, expected }) => {
      expect(resolveThemeMode({ stored, systemMode, defaultMode })).toBe(expected);
    },
  );

  it("ignores a stored value that is not a preference", () => {
    // `localStorage` is shared per ORIGIN, so this key can hold anything another
    // app (or an older build) wrote. Junk must degrade to "no preference" and
    // let the OS decide, never crash and never stamp a bogus class name.
    for (const stored of ["", "  ", "Dark", "DARK", "midnight", "{}", "null", "0"]) {
      expect(
        resolveThemeMode({ stored, systemMode: "dark", defaultMode: "light" }),
        `stored=${JSON.stringify(stored)}`,
      ).toBe("dark");
    }
  });

  it("keeps the fork default as the floor for every unusable input", () => {
    expect(resolveThemeMode({ stored: "banana", systemMode: null, defaultMode: "dark" })).toBe(
      "dark",
    );
  });
});

describe("THEME_STORAGE_KEY", () => {
  it("is the origin-scoped key both the script and the provider read", () => {
    // Pinned as a VALUE: the pre-paint script embeds it as a string literal and
    // the provider reads it through this constant. If they drift, the script
    // stamps one theme and React immediately publishes another.
    expect(THEME_STORAGE_KEY).toBe("wallow-theme");
  });
});

describe("themeInitScript", () => {
  it("reads the persisted preference from the shared storage key", () => {
    expect(themeInitScript("dark")).toContain(THEME_STORAGE_KEY);
  });

  it("consults BOTH colour-scheme media queries, so 'no preference' is distinguishable", () => {
    // Only asking for `dark` collapses "the OS wants light" and "the OS has no
    // opinion" into the same answer, which silently retires `defaultMode`.
    const source: string = themeInitScript("dark");

    expect(source).toContain("prefers-color-scheme: dark");
    expect(source).toContain("prefers-color-scheme: light");
  });

  it("embeds the fork default it was given", () => {
    expect(themeInitScript("dark")).toContain("dark");
    expect(themeInitScript("light")).toContain("light");
  });

  it("stamps the class on the document element itself", () => {
    const source: string = themeInitScript("dark");

    expect(source).toContain("documentElement");
    expect(source).toContain("classList");
  });

  it("survives a storage read that throws", () => {
    // `localStorage` throws outright under a blocked-cookies policy and in some
    // private-browsing modes. An uncaught throw here runs BEFORE the app's
    // scripts and takes the whole page down, so the guard is mandatory.
    expect(themeInitScript("dark")).toMatch(/\btry\b/u);
    expect(themeInitScript("dark")).toMatch(/\bcatch\b/u);
  });

  it("contains no '<', so it cannot terminate its own <script> element", () => {
    // ThemeScript renders the source as a TEXT CHILD of `<script>` — the same
    // pattern `DocumentStyles` uses for `<style>`, which is why neither
    // component needs React's raw-HTML escape hatch. React does not escape a
    // script element's text child, so a `<` in the source would reach the
    // browser verbatim; the source has to be written without one (`a < b`
    // becomes `b > a`).
    expect(themeInitScript("dark")).not.toContain("<");
    expect(themeInitScript("light")).not.toContain("<");
  });

  it("is a self-contained expression that leaks no globals", () => {
    // It runs in the document's top-level scope alongside the app's own
    // bundles; a stray global here is a name collision waiting to happen.
    const source: string = themeInitScript("dark");

    expect(source.startsWith("(")).toBe(true);
    expect(source.trimEnd().endsWith(";") || source.trimEnd().endsWith(")")).toBe(true);
  });
});
