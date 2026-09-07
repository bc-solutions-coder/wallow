/**
 * Resolve fork and per-client branding into display fields and CSS custom properties. Fork
 * defaults come from this package branding.json; client values are supplied by the caller.
 */
// Fork configuration stays at the package root beside its stylesheet and assets.
// The JSON import attribute also permits direct loading through Node ESM configuration tools.
import forkBrandingJson from "../branding.json" with { type: "json" };
import {
  publishedGlobalScript,
  readPublishedGlobal,
} from "@bc-solutions-coder/env/published-global";
import { toRootRelativeAssetUrl } from "./asset-urls";

/** The two colour schemes a theme defines. */
export type ThemeMode = "light" | "dark";

/**
 * Theme keys mapped to CSS values, such as primaryForeground to oklch(...). Keys remain open-ended
 * and become CSS custom properties; values are not CSS-sanitized.
 */
export type ThemeColors = Readonly<Record<string, string>>;

/** CSS custom properties, keyed by full variable name (`--primary-foreground`). */
export type CssVars = Readonly<Record<string, string>>;

/** The fork's theme block (`theme` in `packages/styles/branding.json`). */
export interface ForkTheme {
  /**
   * Initial mode; case-insensitive light selects light and other values select dark.
   */
  readonly defaultMode: string;
  /**
   * Theme values used for the light mode.
   */
  readonly light: ThemeColors;
  /**
   * Theme values used for the dark mode.
   */
  readonly dark: ThemeColors;
}

/**
 * Fork display settings and theme as authored in packages/styles/branding.json. Optional
 * repositoryUrl and docsUrl use upstream defaults when absent; the raw constants retain explicitly
 * empty strings.
 */
export interface ForkBranding {
  /**
   * Fork display name when no client branding is supplied.
   */
  readonly appName: string;
  /**
   * Fork icon reference resolved under the consuming app base path.
   */
  readonly appIcon: string;
  /**
   * Fork subtitle; an empty string resolves to no tagline.
   */
  readonly tagline: string;
  /**
   * Optional repository link; absence selects the upstream repository.
   */
  readonly repositoryUrl?: string;
  /**
   * Optional documentation link; absence selects upstream documentation.
   */
  readonly docsUrl?: string;
  /**
   * Fork preference for whether the consuming app offers its landing page.
   */
  readonly landingPage: { readonly enabled: boolean };
  /**
   * Default mode and palettes for both color schemes.
   */
  readonly theme: ForkTheme;
}

/**
 * Per-client display fields and serialized theme supplied by the API caller. Declared
 * independently of the SDK so branding resolution does not depend on transport code.
 */
export interface ClientBranding {
  /**
   * OIDC client identifier associated with these display settings.
   */
  readonly clientId: string;
  /**
   * Client display name, used instead of the fork name.
   */
  readonly displayName: string;
  /**
   * Client subtitle; null or an empty string hides it.
   */
  readonly tagline: string | null;
  /**
   * Client logo reference, passed through without applying the app base path.
   */
  readonly logoUrl: string | null;
  /**
   * Optional JSON mode palettes overlaid on the fork theme.
   */
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
 * Fork configuration imported from the package branding.json. The bundler includes it in the
 * browser and SSR output without a runtime filesystem read.
 */
export const forkBranding: ForkBranding = forkBrandingJson;

/**
 * Convert a camelCase key into a lowercase CSS custom property name. For example,
 * primaryForeground becomes --primary-foreground. Only lowercase-to-uppercase boundaries insert a
 * hyphen.
 */
export function toCssVarName(propertyName: string): string {
  return `--${propertyName.replaceAll(/(?<lower>[a-z])(?<upper>[A-Z])/gu, "$<lower>-$<upper>").toLowerCase()}`;
}

/**
 * Map theme keys to CSS custom property names, omitting exactly empty string values. Whitespace
 * values are preserved. Neither names nor values are CSS-sanitized.
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
 * Read one mode from JSON shaped as { light: {...}, dark: {...} }. Returns CSS variables for
 * nonempty string values and skips other values. Invalid JSON or a missing mode returns an empty
 * object; this validates the shape, not CSS safety.
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

/**
 * Use light for a case-insensitive light value; all other values select dark.
 */
function toThemeMode(defaultMode: string): ThemeMode {
  return defaultMode.toLowerCase() === "light" ? "light" : "dark";
}

/**
 * Convert null, undefined, and an empty string to null; preserve other strings.
 */
function orNull(value: string | null | undefined): string | null {
  return value === null || value === undefined || value === "" ? null : value;
}

/**
 * Resolve display fields and theme variables from fork defaults and an optional client. A null
 * client uses the fork name, tagline, and base-path-aware icon. A client replaces those display
 * fields, with empty tagline and logo becoming null, while its theme variables overlay fork
 * variables separately for each mode.
 *
 * @param fork Fork defaults, usually forkBranding.
 *
 * @param client Retrieved client branding, or null when absent or unavailable.
 *
 * @param basePath App URL prefix, applied only to the fork icon; client logo URLs remain
 * unchanged.
 */
export function mergeClientBranding(
  fork: ForkBranding,
  client: ClientBranding | null,
  basePath: string = "",
): ResolvedBranding {
  const forkLight: CssVars = toCssVars(fork.theme.light);
  const forkDark: CssVars = toCssVars(fork.theme.dark);
  const defaultMode: ThemeMode = toThemeMode(fork.theme.defaultMode);

  if (client === null) {
    return {
      name: fork.appName,
      tagline: orNull(fork.tagline),
      logoUrl: toRootRelativeAssetUrl(fork.appIcon, basePath),
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
 * Render CSS for :root, .dark, and .light from resolved branding. The root uses defaultMode, while
 * explicit classes select their respective palettes. Names and values are interpolated verbatim;
 * use trusted theme data when inserting the result into a style element.
 */
export function renderThemeStyle(resolved: ResolvedBranding): string {
  const blocks: string[] = [
    `:root {\n${toDeclarations(resolved.defaultMode === "dark" ? resolved.cssVars.dark : resolved.cssVars.light)}\n}`,
    `.dark {\n${toDeclarations(resolved.cssVars.dark)}\n}`,
    `.light {\n${toDeclarations(resolved.cssVars.light)}\n}`,
  ];
  return blocks.join("\n");
}

/**
 * Resolve the package fork branding without client overrides. Pass the consuming app URL prefix to
 * root the icon correctly; the forkResolvedBranding constant uses the origin root.
 */
export function resolveForkBranding(basePath: string = ""): ResolvedBranding {
  return mergeClientBranding(forkBranding, null, basePath);
}

/**
 * The fork's app icon under `basePath` — what to render, in place of
 * `forkBranding.appIcon`, wherever the icon or favicon is shown by an app served
 * under a URL prefix.
 */
export function toAppIconUrl(basePath: string = ""): string {
  return toRootRelativeAssetUrl(forkBranding.appIcon, basePath);
}

/** The fork's own branding, resolved with no client overlay, at the site root. */
export const forkResolvedBranding: ResolvedBranding = resolveForkBranding();

/**
 * The fork's app icon at the site root — what to render, in place of
 * `forkBranding.appIcon`, wherever the icon or favicon is shown by an app served
 * at the origin root. Under a URL prefix, call {@link toAppIconUrl} instead.
 */
export const appIconUrl: string = toAppIconUrl();

/**
 * Where the two outbound identity links point when a fork's `branding.json`
 * omits them.
 *
 * The package needs its own copy rather than reading the JSON's values as
 * defaults: `branding.json` is `merge=ours` in `.gitattributes`, so a fork
 * REPLACES it wholesale and an omitted field arrives as `undefined` rather than
 * as this repo's value.
 */
const UPSTREAM_REPOSITORY_URL: string = "https://github.com/bc-solutions-coder/wallow";
const UPSTREAM_DOCS_URL: string = "https://bc-solutions-coder.github.io/wallow/";

/**
 * Build-time repository link from branding.json, or the upstream repository when absent.
 * Explicitly empty strings are retained; use resolveForkLinks for deployment overrides.
 */
export const forkRepositoryUrl: string = forkBranding.repositoryUrl ?? UPSTREAM_REPOSITORY_URL;

/**
 * Build-time documentation link from branding.json, or upstream documentation when absent.
 * Explicitly empty strings are retained.
 */
export const forkDocsUrl: string = forkBranding.docsUrl ?? UPSTREAM_DOCS_URL;

/** The fork's two outbound identity links, resolved together. */
export interface ForkLinks {
  /**
   * Repository URL for rendered source links.
   */
  readonly repositoryUrl: string;
  /**
   * Documentation URL for rendered help links.
   */
  readonly docsUrl: string;
}

/** The environment variables {@link resolveForkLinks} reads, by name. */
export const FORK_REPOSITORY_URL_VAR = "WALLOW_REPOSITORY_URL";
/**
 * Environment variable name for the deployment documentation URL override.
 */
export const FORK_DOCS_URL_VAR = "WALLOW_DOCS_URL";

/** {@link forkRepositoryUrl} and {@link forkDocsUrl} as one object. */
export const forkLinks: ForkLinks = {
  repositoryUrl: forkRepositoryUrl,
  docsUrl: forkDocsUrl,
};

/**
 * Resolve repository and documentation links using nonblank environment overrides, then the
 * package fork-link constants. Overrides are trimmed. The constants use branding.json values when
 * present and upstream URLs when absent; explicitly empty JSON values are preserved.
 */
export function resolveForkLinks(
  env: Readonly<Record<string, string | undefined>> = {},
): ForkLinks {
  return {
    repositoryUrl: firstNonEmpty(env[FORK_REPOSITORY_URL_VAR], forkRepositoryUrl),
    docsUrl: firstNonEmpty(env[FORK_DOCS_URL_VAR], forkDocsUrl),
  };
}

/** The first value that is a non-blank string, else the fallback. */
function firstNonEmpty(value: string | undefined, fallback: string): string {
  return value !== undefined && value.trim() !== "" ? value.trim() : fallback;
}

/**
 * Browser global property used to share resolved fork links between SSR and hydration.
 */
export const FORK_LINKS_GLOBAL_KEY = "__WALLOW_FORK_LINKS__";

/**
 * Return escaped inline script source that publishes repository and documentation URLs before
 * hydration. The server renders the source without assigning its own global state.
 */
export function forkLinksScript(links: ForkLinks): string {
  return publishedGlobalScript(FORK_LINKS_GLOBAL_KEY, {
    repositoryUrl: links.repositoryUrl,
    docsUrl: links.docsUrl,
  });
}

/**
 * Read the browser-published link pair from a supplied scope such as globalThis. Returns undefined
 * unless both properties are nonblank strings. Returned strings are not trimmed or URL-validated.
 */
export function readInjectedForkLinks(scope: unknown): ForkLinks | undefined {
  const injected: unknown = readPublishedGlobal(FORK_LINKS_GLOBAL_KEY, scope);
  if (typeof injected !== "object" || injected === null) {
    return undefined;
  }

  const { repositoryUrl, docsUrl } = injected as Record<string, unknown>;
  if (typeof repositoryUrl !== "string" || typeof docsUrl !== "string") {
    return undefined;
  }
  if (repositoryUrl.trim() === "" || docsUrl.trim() === "") {
    return undefined;
  }

  return { repositoryUrl, docsUrl };
}
