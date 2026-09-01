# packages/styles — @bc-solutions-coder/styles Agent Guide

Owns **fork branding**: `packages/styles/branding.json` is the only file a fork edits to
rebrand — this package turns it into theme CSS custom properties at render time.

- **A consuming app must declare its own `@source` scan.** Tailwind v4 resolves `@source`
  relative to the declaring stylesheet, so a scan declared here would see this package's
  files and ship a stylesheet that styles nothing.
- Node-only code (`./assets`, `./vite`) stays off the main entry so it never bundles into a
  browser build — keep that split when adding surfaces.
- **`virtual:wallow-theme.css`** (`THEME_MODULE_ID`, served by `wallowStyles()`) is the theme
  as a STYLESHEET, rendered per request. Without it `styles.css`'s custom properties are
  valueless — `var(--sidebar)` is invalid and a `bg-sidebar` element paints transparent. It
  is a virtual module (not a generated `theme.css` on disk) so a Vitest browser project can
  load the theme with a bare `import`.
- **A base path is an argument here, never a read.** The package ships a PREBUILT bundle, so
  its own `import.meta.env.BASE_URL` freezes at `/` at package build time — an app served
  under a prefix passes its prefix in (`toAppIconUrl(BASE_PATH)`,
  `resolveForkBranding(BASE_PATH)`, `mergeClientBranding(fork, client, BASE_PATH)`). The
  constants `appIconUrl`/`forkResolvedBranding` are correct only at the origin root.
  `toRootRelativeAssetUrl` normalises any base shape (`/`, `/auth`, `/auth/`, `auth`).
- **Fork links** (`forkRepositoryUrl`/`forkDocsUrl`): resolution is env → `branding.json` →
  upstream constant, blank counting as unset — `branding.json` is `merge=ours`, so a fork
  replaces the file wholesale and a missing key must still resolve. The env record is a
  PARAMETER for the same prebuilt-bundle reason as the base path. Three pure pieces, no
  wiring: `resolveForkLinks(env)`, `forkLinksScript(links)` (the inline `<script>` source,
  `<` escaped because React does not escape a `<script>` text child — the escape/read-back
  mechanism itself is `@bc-solutions-coder/env/published-global`), and
  `readInjectedForkLinks(scope)` (`undefined` for anything malformed).
