/**
 * Re-export of the shared branding/theme module, which now lives in
 * `@bc-solutions-coder/styles` alongside the Tailwind entry whose tokens these
 * variables feed (Wallow-ffpq.1.1). Kept as a shim so this app's existing
 * imports of `~/lib/branding` keep resolving.
 */
export {
  appIconUrl,
  type ClientBranding,
  type CssVars,
  forkBranding,
  type ForkBranding,
  forkResolvedBranding,
  type ForkTheme,
  mergeClientBranding,
  parseThemeCssVars,
  renderThemeStyle,
  type ResolvedBranding,
  type ThemeColors,
  type ThemeMode,
  toCssVarName,
  toCssVars,
  toRootRelativeAssetUrl,
} from "@bc-solutions-coder/styles";
