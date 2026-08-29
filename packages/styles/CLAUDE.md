# packages/styles — @bc-solutions-coder/styles Agent Guide

Owns **fork branding**: the canonical branding types plus the shared Tailwind v4 entry.
`packages/styles/branding.json` is the only file a fork edits to rebrand — this package turns
it into theme CSS custom properties at render time.

## Four exports

| Entry                | Runs in | What it is                                                                                                                                                                                                                                                                                                                                 |
| -------------------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `.` (`src/index.ts`) | Browser | Branding types (`ForkBranding`, `ClientBranding`, `ResolvedBranding`, `ThemeColors`) plus `renderThemeStyle` / `toCssVars` / `mergeClientBranding`, the asset-URL surface (`toRootRelativeAssetUrl`, `toAppIconUrl`, `resolveForkBranding`, `appIconUrl`, `forkResolvedBranding`) and the fork links (`forkRepositoryUrl`, `forkDocsUrl`). |
| `./styles.css`       | CSS     | The shared Tailwind v4 entry — maps Tailwind tokens onto the plain custom properties the theme emits.                                                                                                                                                                                                                                      |
| `./assets`           | Node    | `brandAssetsDir` — the directory a consuming app's build copies to its served root.                                                                                                                                                                                                                                                        |
| `./vite`             | Node    | `wallowStyles()` — one call in an app's Vite `plugins` array wires `@tailwindcss/vite` + brand assets + the `virtual:wallow-theme.css` module.                                                                                                                                                                                             |

- **A consuming app must declare its own `@source` scan.** Tailwind v4 resolves `@source`
  relative to the declaring stylesheet, so a scan declared here would see _this_ package's
  files and ship a stylesheet that styles nothing.
- Node-only code (`./assets`, `./vite`) stays off the main entry so it never bundles into a
  browser build. Keep that split when adding surfaces.
- **`virtual:wallow-theme.css`** (`THEME_MODULE_ID`, served by `wallowStyles()`) is the theme
  as a STYLESHEET — `renderThemeStyle(forkResolvedBranding)` rendered per request. Without it,
  `styles.css`'s valueless custom properties leave `var(--sidebar)` invalid and a `bg-sidebar`
  element paints transparent. It is a virtual module (not a generated `theme.css` on disk) so a
  Vitest browser project can load the theme with a bare `import`, and because `pnpm check` runs
  `test` before `build` — a disk artifact would be stale exactly when it matters.
- **A base path is an argument here, never a read.** This package ships a PREBUILT bundle, so
  its own `import.meta.env.BASE_URL` freezes at `/` at package build time — an app served
  under a prefix must pass its prefix in (`toAppIconUrl(BASE_PATH)`,
  `resolveForkBranding(BASE_PATH)`, `mergeClientBranding(fork, client, BASE_PATH)`). The
  constants `appIconUrl` / `forkResolvedBranding` are the unprefixed resolutions, correct only
  for an app served at the origin root. `toRootRelativeAssetUrl` normalises any base-path
  shape (`/`, `/auth`, `/auth/`, `auth`), so apps can hand over Vite's raw
  `import.meta.env.BASE_URL`.
- **`forkRepositoryUrl` / `forkDocsUrl` are fork identity, so they live here.** Both fall back
  to an upstream constant when `branding.json` names neither — `branding.json` is
  `merge=ours`, so a fork replaces the file wholesale and a missing key must still resolve.
- **They are also the one branding value a DEPLOYMENT can override**, via
  `WALLOW_REPOSITORY_URL` / `WALLOW_DOCS_URL`. This package supplies three pure pieces and
  owns no wiring: `resolveForkLinks(env)` (env → `branding.json` → upstream, blank counts as
  unset), `forkLinksScript(links)` (the inline `<script>` source, `<` escaped because React
  does not escape a `<script>` text child), and `readInjectedForkLinks(scope)` (the value back
  off `globalThis`, `undefined` for anything malformed). The env record is a PARAMETER for the
  same prebuilt-bundle reason as the base path. Each app reads `process.env` in server-only
  middleware, states the result in `<head>` beside `ThemeScript`, and reads it back through a
  plain `shared/lib/fork-links.ts` accessor — no context, no hook, no hydration mismatch.
- `pnpm --filter @bc-solutions-coder/styles build` (Vite lib mode +
  `tsc -p tsconfig.build.json`); `test` / `typecheck` are the other scripts. Tests are
  node-environment vitest — no DOM here.
