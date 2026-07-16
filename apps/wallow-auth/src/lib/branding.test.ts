import { describe, expect, it } from "vitest";

import {
  type ClientBranding,
  type ForkBranding,
  forkBranding,
  forkResolvedBranding,
  mergeClientBranding,
  parseThemeCssVars,
  renderThemeStyle,
  type ResolvedBranding,
  toCssVarName,
} from "./branding";

/**
 * A minimal stand-in for `api/branding.json`. The merge tests drive this rather
 * than the real fork branding so they assert the merge *rules* and stay green
 * when the fork rebrands (which is the whole point of `api/branding.json`). The
 * few tests that must pin real fork values say so explicitly.
 */
const testFork: ForkBranding = {
  appName: "Testwallow",
  appIcon: "test-icon.svg",
  tagline: "Test in it",
  repositoryUrl: "https://example.test/repo",
  landingPage: { enabled: true },
  theme: {
    defaultMode: "dark",
    light: { background: "#fff", primaryForeground: "#000", radius: "0.5rem" },
    dark: { background: "#000", primaryForeground: "#fff", radius: "0.5rem" },
  },
};

function makeClient(overrides: Partial<ClientBranding> = {}): ClientBranding {
  return {
    clientId: "acme-web",
    displayName: "Acme",
    tagline: null,
    logoUrl: null,
    themeJson: null,
    ...overrides,
  };
}

describe("toCssVarName", () => {
  it("converts camelCase keys to kebab-case custom properties", () => {
    expect(toCssVarName("primaryForeground")).toBe("--primary-foreground");
  });

  it("passes single-word keys through unchanged", () => {
    expect(toCssVarName("radius")).toBe("--radius");
  });
});

describe("parseThemeCssVars", () => {
  const themeJson: string = JSON.stringify({
    light: { primary: "#111", primaryForeground: "#eee" },
    dark: { primary: "#eee" },
  });

  it("reads the requested mode's colours as CSS variables", () => {
    expect(parseThemeCssVars(themeJson, "light")).toEqual({
      "--primary": "#111",
      "--primary-foreground": "#eee",
    });
    expect(parseThemeCssVars(themeJson, "dark")).toEqual({ "--primary": "#eee" });
  });

  it("yields nothing for a mode the theme does not define", () => {
    expect(parseThemeCssVars(JSON.stringify({ light: { primary: "#111" } }), "dark")).toEqual({});
  });

  it("skips empty and non-string values", () => {
    const messy: string = JSON.stringify({
      light: { primary: "#111", secondary: "", radius: 4, accent: null },
    });
    expect(parseThemeCssVars(messy, "light")).toEqual({ "--primary": "#111" });
  });

  it("degrades to no variables on malformed JSON rather than throwing", () => {
    expect(parseThemeCssVars("{not json", "light")).toEqual({});
    expect(parseThemeCssVars('"a string"', "light")).toEqual({});
  });
});

describe("mergeClientBranding without a client", () => {
  const resolved: ResolvedBranding = mergeClientBranding(testFork, null);

  it("resolves the fork's identity", () => {
    expect(resolved.name).toBe("Testwallow");
    expect(resolved.tagline).toBe("Test in it");
    expect(resolved.logoUrl).toBe("test-icon.svg");
  });

  it("resolves the fork's palette for both modes", () => {
    expect(resolved.cssVars.light).toEqual({
      "--background": "#fff",
      "--primary-foreground": "#000",
      "--radius": "0.5rem",
    });
    expect(resolved.cssVars.dark).toEqual({
      "--background": "#000",
      "--primary-foreground": "#fff",
      "--radius": "0.5rem",
    });
  });

  it("takes the default mode from the fork theme", () => {
    expect(resolved.defaultMode).toBe("dark");
    expect(
      mergeClientBranding({ ...testFork, theme: { ...testFork.theme, defaultMode: "light" } }, null)
        .defaultMode,
    ).toBe("light");
  });
});

describe("mergeClientBranding with a client", () => {
  it("prefers the client's display name over the fork's app name", () => {
    expect(mergeClientBranding(testFork, makeClient()).name).toBe("Acme");
  });

  it("uses the client's tagline and logo when it set them", () => {
    const resolved: ResolvedBranding = mergeClientBranding(
      testFork,
      makeClient({ tagline: "Acme things", logoUrl: "https://cdn.test/acme.svg" }),
    );
    expect(resolved.tagline).toBe("Acme things");
    expect(resolved.logoUrl).toBe("https://cdn.test/acme.svg");
  });

  it("shows no tagline or logo rather than falling back to the fork's", () => {
    // A client branded "Acme" must never render Wallow's icon/tagline — that
    // would misattribute the fork. Mirrors AuthLayout.razor's client branch.
    const resolved: ResolvedBranding = mergeClientBranding(testFork, makeClient());
    expect(resolved.tagline).toBeNull();
    expect(resolved.logoUrl).toBeNull();
  });

  it("treats empty-string tagline and logo as absent", () => {
    const resolved: ResolvedBranding = mergeClientBranding(
      testFork,
      makeClient({ tagline: "", logoUrl: "" }),
    );
    expect(resolved.tagline).toBeNull();
    expect(resolved.logoUrl).toBeNull();
  });

  it("overlays the client's ThemeJson on the fork palette per mode", () => {
    const resolved: ResolvedBranding = mergeClientBranding(
      testFork,
      makeClient({
        themeJson: JSON.stringify({
          light: { primaryForeground: "#123" },
          dark: { background: "#321" },
        }),
      }),
    );

    // Overridden key wins; untouched fork keys survive.
    expect(resolved.cssVars.light).toEqual({
      "--background": "#fff",
      "--primary-foreground": "#123",
      "--radius": "0.5rem",
    });
    expect(resolved.cssVars.dark).toEqual({
      "--background": "#321",
      "--primary-foreground": "#fff",
      "--radius": "0.5rem",
    });
  });

  it("keeps the fork palette when the client has no theme", () => {
    const resolved: ResolvedBranding = mergeClientBranding(testFork, makeClient());
    expect(resolved.cssVars.light).toEqual(mergeClientBranding(testFork, null).cssVars.light);
    expect(resolved.cssVars.dark).toEqual(mergeClientBranding(testFork, null).cssVars.dark);
  });

  it("keeps the fork palette when the client's theme is unparseable", () => {
    const resolved: ResolvedBranding = mergeClientBranding(
      testFork,
      makeClient({ themeJson: "{not json" }),
    );
    expect(resolved.cssVars.dark).toEqual(mergeClientBranding(testFork, null).cssVars.dark);
  });
});

describe("renderThemeStyle", () => {
  const resolved: ResolvedBranding = mergeClientBranding(testFork, null);
  const css: string = renderThemeStyle(resolved);

  it("emits the dark palette on :root when dark is the default mode", () => {
    expect(css).toMatch(/:root \{[^}]*--background: #000;/u);
  });

  it("emits the light palette on :root when light is the default mode", () => {
    const lightFirst: string = renderThemeStyle(
      mergeClientBranding(
        { ...testFork, theme: { ...testFork.theme, defaultMode: "light" } },
        null,
      ),
    );
    expect(lightFirst).toMatch(/:root \{[^}]*--background: #fff;/u);
  });

  it("emits both mode classes so either can be selected explicitly", () => {
    expect(css).toMatch(/\.dark \{[^}]*--background: #000;/u);
    expect(css).toMatch(/\.light \{[^}]*--background: #fff;/u);
  });
});

describe("forkBranding read from api/branding.json", () => {
  // Pins the real repo-root file: this is the contract the .NET apps share.
  it("loads the fork identity at build time", () => {
    expect(forkBranding.appName).toBe("Wallow");
    expect(forkBranding.appIcon).toBe("piggy-icon.svg");
    expect(forkBranding.tagline).toBe("Wallow in it");
  });

  it("loads both theme palettes with the fork's default mode", () => {
    expect(forkBranding.theme.defaultMode).toBe("dark");
    expect(Object.keys(forkBranding.theme.light).length).toBeGreaterThan(0);
    expect(Object.keys(forkBranding.theme.dark).length).toBeGreaterThan(0);
  });

  it("exposes the fork's own resolved branding for the layout to default to", () => {
    expect(forkResolvedBranding.name).toBe("Wallow");
    expect(forkResolvedBranding.defaultMode).toBe("dark");
    expect(forkResolvedBranding.cssVars.dark["--primary-foreground"]).toBe("oklch(0.14 0.015 50)");
  });
});
