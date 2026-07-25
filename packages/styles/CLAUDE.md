# packages/styles — @bc-solutions-coder/styles Agent Guide

Owns **fork branding**: the canonical branding types plus the shared Tailwind v4 entry.
`api/branding.json` is the only file a fork edits to rebrand — this package turns it into
theme CSS custom properties at render time.

## Four exports

| Entry | Runs in | What it is |
|-------|---------|-----------|
| `.` (`src/index.ts`) | Browser | Branding types (`ForkBranding`, `ClientBranding`, `ResolvedBranding`, `ThemeColors`) plus `renderThemeStyle` / `toCssVars` / `mergeClientBranding` and `toRootRelativeAssetUrl`. |
| `./styles.css` | CSS | The shared Tailwind v4 entry — maps Tailwind tokens onto the plain custom properties the theme emits. |
| `./assets` | Node | `brandAssetsDir` — the directory a consuming app's build copies to its served root. |
| `./vite` | Node | `wallowStyles()` — one call in an app's Vite `plugins` array wires `@tailwindcss/vite` + brand assets. |

- **A consuming app must declare its own `@source` scan.** Tailwind v4 resolves `@source`
  relative to the declaring stylesheet, so a scan declared here would see *this* package's
  files and ship a stylesheet that styles nothing.
- Node-only code (`./assets`, `./vite`) stays off the main entry so it never bundles into a
  browser build. Keep that split when adding surfaces.
- `pnpm --filter @bc-solutions-coder/styles build` (Vite lib mode + `tsc -p tsconfig.build.json`);
  `test` / `typecheck` are the other scripts. Tests are node-environment vitest — no DOM here.
