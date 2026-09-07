import { readFileSync } from "node:fs";

import { describe, expect, it } from "vitest";

import {
  type ForkBranding,
  type ThemeColors,
  type ThemeMode,
  forkBranding,
  mergeClientBranding,
  renderThemeStyle,
  toCssVars,
} from "./branding";

/**
 * Verify that the authored Tailwind tokens reference the palette emitted by branding.ts. Consumers compile the stylesheet themselves, so these checks read its CSS declarations directly.
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

/**
 * The custom properties a `@theme` block indirects through `var(...)` —
 * including the ones inside a fallback chain, `var(--sidebar, var(--foreground))`,
 * which names two: the palette property the token wants and the one it degrades
 * to when a fork's `packages/styles/branding.json` omits it.
 */
function themedVarNames(tokens: Record<string, string>): readonly string[] {
  const matches: RegExpStringIterator<RegExpExecArray> = Object.values(tokens)
    .join(" ")
    .matchAll(/var\(\s*(?<name>--[\w-]+)/gu);
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

  it("hardcodes no palette, so packages/styles/branding.json stays the only place to rebrand", () => {
    // The shared stylesheet maps tokens; palette values come from branding.json at render time.
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
  // packages/styles/branding.json's theme would ship a custom property that renderThemeStyle
  // writes onto :root but no Tailwind token ever consumes — a silently dead
  // token. The recipe is two files in lockstep: a new semantic colour touches
  // packages/styles/branding.json's theme AND styles.css's @theme, nothing per-app.
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

/**
 * Sidebar, success, and warning tokens retain explicit fallback chains.
 * A fork palette can omit those keys, so the tokens must still resolve to existing colors.
 */
const forkSafeTokens: Readonly<Record<string, string>> = {
  "--color-sidebar": "var(--sidebar, var(--foreground))",
  "--color-sidebar-foreground": "var(--sidebar-foreground, var(--background))",
  "--color-sidebar-accent": "var(--sidebar-accent, var(--accent))",
  "--color-success": "var(--success, var(--primary))",
  "--color-success-foreground": "var(--success-foreground, var(--primary-foreground))",
  "--color-warning": "var(--warning, var(--primary))",
  "--color-warning-foreground": "var(--warning-foreground, var(--primary-foreground))",
};

/** The palette properties those tokens want: `--color-sidebar` -> `--sidebar`. */
const newVarNames: readonly string[] = Object.keys(forkSafeTokens).map((token: string): string =>
  token.replace("--color-", "--"),
);

/** The same colours as `packages/styles/branding.json` authors them, in camelCase. */
const newThemeKeys: ReadonlySet<string> = new Set([
  "sidebar",
  "sidebarForeground",
  "sidebarAccent",
  "success",
  "successForeground",
  "warning",
  "warningForeground",
]);

const themeModes: readonly ThemeMode[] = ["light", "dark"];

describe("the sidebar, success and warning semantic tokens", () => {
  for (const [token, chain] of Object.entries(forkSafeTokens)) {
    it(`maps ${token} through the palette with a fork-safe fallback`, () => {
      expect(themeTokens(sharedCss)[token]).toBe(chain);
    });
  }

  for (const mode of themeModes) {
    it(`is defined by packages/styles/branding.json's ${mode} theme`, () => {
      expect(Object.keys(toCssVars(forkBranding.theme[mode]))).toEqual(
        expect.arrayContaining([...newVarNames]),
      );
    });
  }

  it("leaves the pre-existing colour tokens on their plain, un-defaulted mapping", () => {
    // The optional semantic tokens use fallback chains; core palette tokens remain direct references.
    const untouched: readonly (readonly [string, string])[] = Object.entries(
      themeTokens(sharedCss),
    ).filter(
      ([name]: [string, string]): boolean =>
        name.startsWith("--color-") && !(name in forkSafeTokens),
    );

    expect(untouched.length).toBeGreaterThan(0);
    for (const [name, value] of untouched) {
      expect(value, `${name} must stay a plain var()`).toMatch(/^var\(--[\w-]+\)$/u);
    }
  });
});

describe("a fork whose packages/styles/branding.json predates these tokens", () => {
  /** That fork's palette: this fork's, minus every key it never received. */
  function withoutNewKeys(colors: ThemeColors): ThemeColors {
    return Object.fromEntries(
      Object.entries(colors).filter(([key]: [string, string]): boolean => !newThemeKeys.has(key)),
    );
  }

  const legacyFork: ForkBranding = {
    ...forkBranding,
    theme: {
      ...forkBranding.theme,
      light: withoutNewKeys(forkBranding.theme.light),
      dark: withoutNewKeys(forkBranding.theme.dark),
    },
  };

  for (const mode of themeModes) {
    it(`emits no custom property for the colours its ${mode} theme omits`, () => {
      const emitted: readonly string[] = Object.keys(toCssVars(legacyFork.theme[mode]));
      expect(emitted.filter((name: string): boolean => newVarNames.includes(name))).toEqual([]);
    });
  }

  it("renders a theme stylesheet carrying no declaration for them", () => {
    const rendered: string = renderThemeStyle(mergeClientBranding(legacyFork, null));
    for (const name of newVarNames) {
      expect(rendered).not.toMatch(new RegExp(`${name}\\s*:`, "u"));
    }
  });

  it("still resolves every new token, through a fallback its palette does define", () => {
    // A palette with missing optional keys still resolves a fallback color.
    const tokens: Record<string, string> = themeTokens(sharedCss);
    const legacyVars: readonly string[] = Object.keys(toCssVars(legacyFork.theme.light));

    for (const token of Object.keys(forkSafeTokens)) {
      const chain: RegExpMatchArray | null = (tokens[token] ?? "").match(
        /^var\(--[\w-]+,\s*var\((?<fallback>--[\w-]+)\)\)$/u,
      );
      expect(chain?.groups, `${token} must map through var(--x, var(--fallback))`).toBeDefined();
      expect(legacyVars).toContain(chain!.groups!["fallback"]);
    }
  });
});
