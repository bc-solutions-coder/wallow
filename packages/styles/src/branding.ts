/**
 * Fork + per-client branding resolution, shared by every Wallow frontend.
 *
 * This is the TypeScript port of the branding/theme logic in the Blazor auth
 * app's `Components/Layout/AuthLayout.razor`. That layout injected the fork's
 * `BrandingOptions` (bound from `branding.json`, which lived under `api/` for
 * exactly that reason and now lives here, since no backend code ever read it)
 * plus an `IClientBrandingClient`, read the `client_id` query parameter, and — when
 * a client is identified — overlaid that OAuth client's own display name, tagline,
 * logo, and `ThemeJson` colours on top of the fork's.
 *
 * The port keeps that behaviour in one pure, testable function
 * ({@link mergeClientBranding}) so the React layout is left with rendering only.
 */
// `branding.json` is the ONE file a fork edits to rebrand, and it sits at this
// package's root — beside `styles.css` and `assets/`, the two other things a
// fork's identity is made of. It is deliberately not under `src/`: it is
// configuration a human edits, not a module.
//
// The `with { type: "json" }` attribute is required, not decorative. In-repo
// this module is resolved from source rather than from a prebuilt bundle that
// already inlined the JSON, so consumers whose loader is plain Node ESM —
// Storybook evaluating packages/ui/.storybook/main.ts, which reaches here
// through `@bc-solutions-coder/styles/vite` — import this file directly, and
// Node rejects a JSON import without the attribute (ERR_IMPORT_ATTRIBUTE_MISSING).
import forkBrandingJson from "../branding.json" with { type: "json" };
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

/** The fork's theme block (`theme` in `packages/styles/branding.json`). */
export interface ForkTheme {
  readonly defaultMode: string;
  readonly light: ThemeColors;
  readonly dark: ThemeColors;
}

/**
 * The fork's branding, i.e. the shape of `packages/styles/branding.json`. Mirrors the C#
 * `BrandingOptions`. `repositoryUrl` and `docsUrl` are optional here because the
 * JSON omits them when empty, where C# defaults them to `""`.
 */
export interface ForkBranding {
  readonly appName: string;
  readonly appIcon: string;
  readonly tagline: string;
  readonly repositoryUrl?: string;
  readonly docsUrl?: string;
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
 * The fork branding, read from this package's `branding.json` — the single source
 * of fork identity, and the one file a fork edits to rebrand.
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
 * `basePath` is the URL prefix the consuming app is served under (empty by
 * default). It reaches only the fork's icon, which is the one asset resolved
 * here: a client's `logoUrl` is an absolute URL on its own origin, where this
 * app's prefix means nothing.
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

/**
 * The fork's own branding, resolved with no client overlay, for an app served
 * under `basePath` (empty — the site root — by default).
 *
 * An app with a URL prefix must call this with that prefix rather than read
 * {@link forkResolvedBranding}: this package ships prebuilt, so it cannot see
 * the consumer's `import.meta.env.BASE_URL` and the constant is always the
 * unprefixed resolution.
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
 * The fork's repository — every rendered "source" link reads this rather than
 * deriving one URL from another, which is how a docs link drifts off the
 * canonical one.
 */
export const forkRepositoryUrl: string = forkBranding.repositoryUrl ?? UPSTREAM_REPOSITORY_URL;

/** The fork's documentation site. */
export const forkDocsUrl: string = forkBranding.docsUrl ?? UPSTREAM_DOCS_URL;

/** The fork's two outbound identity links, resolved together. */
export interface ForkLinks {
  readonly repositoryUrl: string;
  readonly docsUrl: string;
}

/** The environment variables {@link resolveForkLinks} reads, by name. */
export const FORK_REPOSITORY_URL_VAR = "WALLOW_REPOSITORY_URL";
export const FORK_DOCS_URL_VAR = "WALLOW_DOCS_URL";

/** {@link forkRepositoryUrl} and {@link forkDocsUrl} as one object. */
export const forkLinks: ForkLinks = {
  repositoryUrl: forkRepositoryUrl,
  docsUrl: forkDocsUrl,
};

/**
 * The fork's links for ONE deployment: `WALLOW_REPOSITORY_URL` /
 * `WALLOW_DOCS_URL` if the environment names them, else `branding.json`, else
 * the upstream constants.
 *
 * The environment layer exists because `branding.json` is baked at BUILD time
 * while one image is run in several environments — a staging docs site and a
 * production one, a private mirror and the public repo — and rebuilding to move
 * a link is not a deployment step anyone should need.
 *
 * The env record is a PARAMETER, exactly as a base path is (see the note on
 * {@link toRootRelativeAssetUrl}): this package ships a prebuilt bundle, so any
 * `process.env` or `import.meta.env` read inside it would answer with the
 * LIBRARY's build environment rather than the running app's. The caller reads
 * its own environment — for a Start app, in the server-only request middleware
 * — and passes the record in.
 *
 * A variable set to the empty string is treated as unset: that is what an
 * unsubstituted `WALLOW_DOCS_URL=` in a compose env file produces, and a link
 * with no href is worse than the default one.
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
 * The global property a server-rendered document publishes {@link resolveForkLinks}'s
 * answer on, so the browser can read back the same pair the SSR pass rendered.
 *
 * This is the whole crossing mechanism: the environment exists only on the
 * server, and a link whose href differs between the server render and the
 * hydrating one is a hydration mismatch. Only the BROWSER ever holds this
 * property — the server renders it as text into the document and never assigns
 * it, because a server global is shared by every concurrent request.
 */
/** `<` as a JavaScript string escape — the one character an inline script must not carry. */
const LT_ESCAPE = String.raw`\u003c`;

export const FORK_LINKS_GLOBAL_KEY = "__WALLOW_FORK_LINKS__";

/**
 * The source of the inline `<script>` that publishes one deployment's links,
 * rendered in `<head>` so it runs before hydration.
 *
 * The returned source contains no `<`: React does not escape a text child of
 * `<script>`, so an href containing `</script` would otherwise end the element
 * early. Escaping it to its
 * `\u003c` sequence keeps the JSON string literal valid and the element intact.
 */
export function forkLinksScript(links: ForkLinks): string {
  const payload: string = JSON.stringify({
    repositoryUrl: links.repositoryUrl,
    docsUrl: links.docsUrl,
  });
  return `window[${JSON.stringify(FORK_LINKS_GLOBAL_KEY)}]=${payload.replaceAll("<", LT_ESCAPE)};`;
}

/**
 * The links {@link forkLinksScript} published, read back off a scope —
 * `globalThis` in a browser — or `undefined` when nothing published any.
 *
 * `undefined` is the answer for anything that is not two non-blank strings, junk
 * included: the caller's fallback chain (the request's own resolution on the
 * server, the build-time pair elsewhere) is always a usable pair, so a
 * malformed global costs the deployment's override rather than the href.
 */
export function readInjectedForkLinks(scope: unknown): ForkLinks | undefined {
  if (typeof scope !== "object" || scope === null) {
    return undefined;
  }

  const injected: unknown = (scope as Record<string, unknown>)[FORK_LINKS_GLOBAL_KEY];
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
