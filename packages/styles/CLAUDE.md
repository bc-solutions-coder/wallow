# packages/styles — @bc-solutions-coder/styles Agent Guide

Owns **fork branding**: the canonical branding types plus the shared Tailwind v4 entry.
`packages/styles/branding.json` is the only file a fork edits to rebrand — this package turns it into
theme CSS custom properties at render time.

## Four exports

| Entry                | Runs in | What it is                                                                                                                                                                                                                                                                                                                                                |
| -------------------- | ------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `.` (`src/index.ts`) | Browser | Branding types (`ForkBranding`, `ClientBranding`, `ResolvedBranding`, `ThemeColors`) plus `renderThemeStyle` / `toCssVars` / `mergeClientBranding`, the asset-URL surface (`toRootRelativeAssetUrl`, `toAppIconUrl`, `resolveForkBranding`, `appIconUrl`, `forkResolvedBranding`) and the fork's two outbound links (`forkRepositoryUrl`, `forkDocsUrl`). |
| `./styles.css`       | CSS     | The shared Tailwind v4 entry — maps Tailwind tokens onto the plain custom properties the theme emits.                                                                                                                                                                                                                                                     |
| `./assets`           | Node    | `brandAssetsDir` — the directory a consuming app's build copies to its served root.                                                                                                                                                                                                                                                                       |
| `./vite`             | Node    | `wallowStyles()` — one call in an app's Vite `plugins` array wires `@tailwindcss/vite` + brand assets + the `virtual:wallow-theme.css` module.                                                                                                                                                                                                            |

- **A consuming app must declare its own `@source` scan.** Tailwind v4 resolves `@source`
  relative to the declaring stylesheet, so a scan declared here would see _this_ package's
  files and ship a stylesheet that styles nothing.
- Node-only code (`./assets`, `./vite`) stays off the main entry so it never bundles into a
  browser build. Keep that split when adding surfaces.
- **`virtual:wallow-theme.css`** (`THEME_MODULE_ID`, served by `wallowStyles()`) is the theme as a
  STYLESHEET — `renderThemeStyle(forkResolvedBranding)` rendered per request. `styles.css` maps every
  colour token onto a valueless custom property, so without it `var(--sidebar)` is
  invalid-at-computed-value-time and a `bg-sidebar` element paints transparent. It exists so a Vitest
  browser project can load the theme with a bare `import` and no JS: importing this LINKED package
  from a setup file makes Vite re-optimize mid-run and reload the page with a second copy of the
  router. It is deliberately not a generated `theme.css` on disk — the root `pnpm check` runs `test`
  before `build`, so an artifact would be stale exactly when it matters.
- **A base path is an argument here, never a read.** This package ships a PREBUILT bundle, so
  its own `import.meta.env.BASE_URL` freezes at `/` when the package is built — an app served
  under a prefix must pass that prefix in (`toAppIconUrl(BASE_PATH)`,
  `resolveForkBranding(BASE_PATH)`, `mergeClientBranding(fork, client, BASE_PATH)`). The
  constants `appIconUrl` / `forkResolvedBranding` are the unprefixed resolutions and are
  correct only for an app served at the origin root. `toRootRelativeAssetUrl` normalises
  whatever shape the base path arrives in (`/`, `/auth`, `/auth/`, `auth`), so an app can hand
  over Vite's raw `import.meta.env.BASE_URL`.
- **`forkRepositoryUrl` / `forkDocsUrl` are fork identity, so they live here rather than in an
  app.** Both fall back to an upstream constant when `branding.json` names neither, which is what
  makes them safe: `branding.json` is `merge=ours`, so a fork replaces the file wholesale and a
  missing key must still resolve to something.
- `pnpm --filter @bc-solutions-coder/styles build` (Vite lib mode + `tsc -p tsconfig.build.json`);
  `test` / `typecheck` are the other scripts. Tests are node-environment vitest — no DOM here.
