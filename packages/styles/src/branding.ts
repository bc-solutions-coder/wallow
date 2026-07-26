/**
 * Fork + per-client branding resolution, shared by every Wallow frontend.
 *
 * This is the TypeScript port of the branding/theme logic in the Blazor auth
 * app's `Components/Layout/AuthLayout.razor`. That layout injects the fork's
 * `BrandingOptions` (bound from the repo-root `api/branding.json`) plus an
 * `IClientBrandingClient`, reads the `client_id` query parameter, and — when a
 * client is identified — overlays that OAuth client's own display name, tagline,
 * logo, and `ThemeJson` colours on top of the fork's.
 *
 * The port keeps that behaviour in one pure, testable function
 * ({@link mergeClientBranding}) so the React layout is left with rendering only.
 */
import forkBrandingJson from "../../../api/branding.json";
import { toRootRelativeAssetUrl } from "./asset-urls";

/** The two colour schemes a theme defines. */
export type ThemeMode = "light" | "dark";

/**
 * A theme colour set as authored in JSON: camelCase keys (`primaryForeground`)
 * mapped to CSS values (`oklch(...)`, `0.5rem`). Mirrors the C#
 * `ThemeColorSet`, but stays open-ended — a client's `ThemeJson` may carry
 * whatever keys it likes, and unknown keys are passed through as CSS variables
 * exactly as the Blazor layout passes them through.
 */
export type ThemeColors = Readonly<Record<string, string>>;

/** CSS custom properties, keyed by full variable name (`--primary-foreground`). */
export type CssVars = Readonly<Record<string, string>>;

/** The fork's theme block (`theme` in `api/branding.json`). */
export interface ForkTheme {
  readonly defaultMode: string;
  readonly light: ThemeColors;
  readonly dark: ThemeColors;
}

/**
 * The fork's branding, i.e. the shape of `api/branding.json`. Mirrors the C#
 * `BrandingOptions`. `repositoryUrl` is optional here because the JSON omits it
 * when empty, where C# defaults it to `""`.
 */
export interface ForkBranding {
  readonly appName: string;
  readonly appIcon: string;
  readonly tagline: string;
  readonly repositoryUrl?: string;
  readonly landingPage: { readonly enabled: boolean };
  readonly theme: ForkTheme;
}

/**
 * Per-client branding as returned by `GET /v1/identity/apps/{clientId}/branding`.
 * Structurally identical to the SDK's generated `ClientBrandingDto` and to the
 * C# `ClientBrandingResponse`; declared locally so this module stays free of
 * transport concerns and remains a pure function of its inputs.
 */
export interface ClientBranding {
  readonly clientId: string;
  readonly displayName: string;
  readonly tagline: string | null;
  readonly logoUrl: string | null;
  readonly themeJson: string | null;
}

/** Branding resolved for rendering: what the layout and document head consume. */
export interface ResolvedBranding {
  /** Heading text: the client's display name, else the fork's app name. */
  readonly name: string;
  /** Sub-heading, or `null` when there is none to show. */
  readonly tagline: string | null;
  /** Logo `src`, or `null` when there is none to show. */
  readonly logoUrl: string | null;
  /** Colour scheme applied when the document does not pick one explicitly. */
  readonly defaultMode: ThemeMode;
  /** Fork CSS variables per mode, overlaid with the client's `ThemeJson`. */
  readonly cssVars: { readonly light: CssVars; readonly dark: CssVars };
}

/**
 * The fork branding, read from the repo-root `api/branding.json` — the single
 * source of fork identity shared with the .NET apps (`api/CLAUDE.md`: "no source
 * changes are needed to rebrand").
 *
 * It is a static JSON *import*, not a runtime `fs` read, deliberately: Vite
 * inlines it at build/config time, so the same module resolves identically in
 * the SSR graph, in the browser bundle, and under Vitest, and the browser bundle
 * never pulls in `node:fs`.
 */
export const forkBranding: ForkBranding = forkBrandingJson;

/**
 * Convert a camelCase theme key to its CSS custom property name, mirroring the
 * Blazor layout's `ConvertToCssName` regex (`([a-z])([A-Z])` -> `$1-$2`,
 * lowercased): `primaryForeground` -> `--primary-foreground`, `radius` ->
 * `--radius`.
 */
export function toCssVarName(propertyName: string): string {
  return `--${propertyName.replaceAll(/(?<lower>[a-z])(?<upper>[A-Z])/gu, "$<lower>-$<upper>").toLowerCase()}`;
}

/**
 * Project a camelCase theme colour set onto CSS custom properties.
 *
 * Empty values are dropped rather than emitted as blank declarations, matching
 * the Blazor layout's `if (!string.IsNullOrEmpty(value))` guard.
 */
export function toCssVars(colors: ThemeColors): CssVars {
  const vars: Record<string, string> = {};
  for (const [key, value] of Object.entries(colors)) {
    if (value !== "") {
      vars[toCssVarName(key)] = value;
    }
  }
  return vars;
}

/**
 * Parse one mode's colours out of a client's `ThemeJson` — a JSON string whose
 * top level is keyed by mode (`{"light": {...}, "dark": {...}}`).
 *
 * Mirrors the Blazor layout's `ParseThemeColors`: a missing mode yields no
 * variables, and non-string values are skipped (C# reads them as `null` and
 * drops them via the empty-value guard). Unlike the Blazor version, malformed
 * JSON is caught rather than thrown: branding is decoration, and a bad theme
 * from one OAuth client must not fail the login page — it degrades to the fork
 * theme, exactly as an unreachable branding endpoint already does.
 */
export function parseThemeCssVars(themeJson: string, mode: ThemeMode): CssVars {
  let parsed: unknown;
  try {
    parsed = JSON.parse(themeJson);
  } catch {
    return {};
  }

  if (typeof parsed !== "object" || parsed === null) {
    return {};
  }

  const modeValue: unknown = (parsed as Record<string, unknown>)[mode];
  if (typeof modeValue !== "object" || modeValue === null) {
    return {};
  }

  const vars: Record<string, string> = {};
  for (const [key, value] of Object.entries(modeValue as Record<string, unknown>)) {
    if (typeof value === "string" && value !== "") {
      vars[toCssVarName(key)] = value;
    }
  }
  return vars;
}

/** Normalise the fork's `theme.defaultMode`, falling back to the C# default. */
function toThemeMode(defaultMode: string): ThemeMode {
  return defaultMode.toLowerCase() === "light" ? "light" : "dark";
}

/** Treat `null`/`undefined`/`""` alike, as C#'s `string.IsNullOrEmpty` does. */
function orNull(value: string | null | undefined): string | null {
  return value === null || value === undefined || value === "" ? null : value;
}

/**
 * Resolve the branding to render for a request: the fork's, overlaid with the
 * per-client branding when the `client_id` query parameter identified one.
 *
 * Semantics are taken from the Blazor layout:
 *  - No client (no `client_id`, or the branding fetch failed/404'd — the caller
 *    passes `null` either way): the fork's app name, tagline, and icon.
 *  - A client: its display name, and its tagline/logo *only if it set them*.
 *    They deliberately do NOT fall back to the fork's — a client branded as
 *    "Acme" showing Wallow's piggy icon and "Wallow in it" would misattribute
 *    the fork, so the layout renders neither instead.
 *
 * Themes differ from the identity fields: the client's `ThemeJson` is *overlaid*
 * on the fork's colours per mode, so a client that overrides only `primary`
 * keeps a coherent palette. (The Blazor layout emits only the client's variables
 * because its fork palette already ships in a static stylesheet; this app has no
 * such stylesheet, so the fork palette is the base layer here. Same rendered
 * result, one source.)
 */
export function mergeClientBranding(
  fork: ForkBranding,
  client: ClientBranding | null,
): ResolvedBranding {
  const forkLight: CssVars = toCssVars(fork.theme.light);
  const forkDark: CssVars = toCssVars(fork.theme.dark);
  const defaultMode: ThemeMode = toThemeMode(fork.theme.defaultMode);

  if (client === null) {
    return {
      name: fork.appName,
      tagline: orNull(fork.tagline),
      logoUrl: toRootRelativeAssetUrl(fork.appIcon),
      defaultMode,
      cssVars: { light: forkLight, dark: forkDark },
    };
  }

  const themeJson: string | null = orNull(client.themeJson);

  return {
    name: client.displayName,
    tagline: orNull(client.tagline),
    logoUrl: orNull(client.logoUrl),
    defaultMode,
    cssVars: {
      light:
        themeJson === null ? forkLight : { ...forkLight, ...parseThemeCssVars(themeJson, "light") },
      dark:
        themeJson === null ? forkDark : { ...forkDark, ...parseThemeCssVars(themeJson, "dark") },
    },
  };
}

/** Serialise one block's worth of declarations: `--name: value;` per line. */
function toDeclarations(vars: CssVars): string {
  return Object.entries(vars)
    .map(([name, value]: [string, string]): string => `  ${name}: ${value};`)
    .join("\n");
}

/**
 * Render resolved theme variables as a stylesheet for the document head,
 * mirroring the `<HeadContent>` block in the Blazor layout: light variables on
 * `:root`, dark variables on `.dark`.
 *
 * When the fork's default mode is dark, the dark variables are additionally
 * emitted on `:root` so the palette applies before any class is set — the Blazor
 * app gets this from its static stylesheet's own defaults.
 */
export function renderThemeStyle(resolved: ResolvedBranding): string {
  const blocks: string[] = [
    `:root {\n${toDeclarations(resolved.defaultMode === "dark" ? resolved.cssVars.dark : resolved.cssVars.light)}\n}`,
    `.dark {\n${toDeclarations(resolved.cssVars.dark)}\n}`,
    `.light {\n${toDeclarations(resolved.cssVars.light)}\n}`,
  ];
  return blocks.join("\n");
}

/** The fork's own branding, resolved with no client overlay. */
export const forkResolvedBranding: ResolvedBranding = mergeClientBranding(forkBranding, null);

/**
 * The fork's app icon at the site root — what to render, in place of
 * `forkBranding.appIcon`, wherever the icon or favicon is shown.
 */
export const appIconUrl: string = toRootRelativeAssetUrl(forkBranding.appIcon);
