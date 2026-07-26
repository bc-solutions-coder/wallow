/**
 * Public entry for `@bc-solutions-coder/styles`.
 *
 * The package has two faces:
 *  - this TypeScript entry, which turns `api/branding.json` into the theme CSS
 *    custom properties a consuming app renders into its document head; and
 *  - the `./styles.css` export, the shared Tailwind v4 entry, which a consuming
 *    app `@import`s and then `@source`s its own component directory from.
 *
 * The brand assets themselves have a third face, the node-only `./assets`
 * subpath: it names the directory a consuming app's build copies to its served
 * root, and stays off this entry because this one is bundled for the browser.
 */
export { toRootRelativeAssetUrl } from "./asset-urls";
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
} from "./branding";
