#!/usr/bin/env node

/**
 * Reads branding.json and generates docfx/templates/wallow/public/main.css
 * for the DocFX docs site. Maps Wallow brand colors to Bootstrap 5 CSS variables.
 *
 * Usage: node scripts/generate-docs-theme.mjs [path/to/branding.json]
 */

import { readFileSync, writeFileSync, mkdirSync } from "fs";
import { resolve, dirname } from "path";

const brandingPath = resolve(process.argv[2] || "branding.json");
const outputPath = resolve("docfx/templates/wallow/public/main.css");

const branding = JSON.parse(readFileSync(brandingPath, "utf8"));
const theme = branding.theme;
const appName = branding.appName || "Wallow";

if (!theme?.light || !theme?.dark) {
  console.error("branding.json must have theme.light and theme.dark sections");
  process.exit(1);
}

const clampRgbChannel = (value) => Math.round(Math.max(0, Math.min(1, value)) * 255);

/**
 * Convert oklch() string to approximate sRGB for --bs-*-rgb variables.
 */
function oklchToRgb(oklchStr) {
  const match = oklchStr.match(/oklch\(\s*([\d.]+)\s+([\d.]+)\s+([\d.]+)\s*\)/);
  if (!match) {
    return null;
  }

  let [, L, C, h] = match.map(Number);
  h = (h * Math.PI) / 180;

  // OKLab from OKLCH
  const a = C * Math.cos(h);
  const b = C * Math.sin(h);

  // OKLab to linear sRGB (via LMS)
  const lPrime = L + 0.3963377774 * a + 0.2158037573 * b;
  const mPrime = L - 0.1055613458 * a - 0.0638541728 * b;
  const sPrime = L - 0.0894841775 * a - 1.291485548 * b;

  const l = lPrime * lPrime * lPrime;
  const m = mPrime * mPrime * mPrime;
  const s = sPrime * sPrime * sPrime;

  const r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
  const g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
  const bl = -0.0041960863 * l - 0.7034186147 * m + 1.707614701 * s;

  return `${clampRgbChannel(r)}, ${clampRgbChannel(g)}, ${clampRgbChannel(bl)}`;
}

function generateCssVars(mode, vars) {
  const primaryRgb = oklchToRgb(vars.primary);
  const bgRgb = oklchToRgb(vars.background);
  const fgRgb = oklchToRgb(vars.foreground);
  const secondaryRgb = oklchToRgb(vars.secondary);

  return `
  /* ${mode} mode — generated from branding.json */
  --bs-primary: ${vars.primary};
  --bs-primary-rgb: ${primaryRgb};
  --bs-link-color: ${vars.primary};
  --bs-link-color-rgb: ${primaryRgb};
  --bs-link-hover-color: ${vars.ring};
  --bs-body-bg: ${vars.background};
  --bs-body-bg-rgb: ${bgRgb};
  --bs-body-color: ${vars.foreground};
  --bs-body-color-rgb: ${fgRgb};
  --bs-heading-color: ${vars.foreground};
  --bs-emphasis-color: ${vars.foreground};
  --bs-emphasis-color-rgb: ${fgRgb};
  --bs-border-color: ${vars.border};
  --bs-secondary-bg: ${vars.secondary};
  --bs-secondary-bg-rgb: ${secondaryRgb};
  --bs-secondary-color: ${vars.secondaryForeground};
  --bs-tertiary-bg: ${vars.muted};
  --bs-tertiary-color: ${vars.mutedForeground};
  --bs-code-color: ${vars.accentForeground};`;
}

const css = `/* ==========================================================================
 * ${appName} Docs Theme — AUTO-GENERATED from branding.json
 * Do not edit manually. Run: node scripts/generate-docs-theme.mjs
 * ========================================================================== */

:root {
${generateCssVars("light", theme.light)}
}

[data-bs-theme="dark"] {
${generateCssVars("dark", theme.dark)}
}

/* Navbar */
.navbar {
  --bs-navbar-brand-color: ${theme.dark.foreground};
  --bs-navbar-brand-hover-color: ${theme.dark.foreground};
  --bs-navbar-color: ${theme.dark.foreground};
  --bs-navbar-hover-color: ${theme.dark.foreground};
  --bs-navbar-active-color: ${theme.dark.foreground};
  background-color: ${theme.dark.background} !important;
}

.navbar-brand {
  font-weight: 700;
  letter-spacing: 0.05em;
}

/* Code blocks */
pre {
  background-color: ${theme.light.card};
  border: 1px solid ${theme.light.border};
  border-radius: ${theme.light.radius};
}

[data-bs-theme="dark"] pre {
  background-color: ${theme.dark.card};
  border-color: ${theme.dark.border};
}

/* Alert/callout accent */
.alert-primary {
  --bs-alert-bg: ${theme.light.accent};
  --bs-alert-color: ${theme.light.accentForeground};
  --bs-alert-border-color: ${theme.light.border};
}

[data-bs-theme="dark"] .alert-primary {
  --bs-alert-bg: ${theme.dark.accent};
  --bs-alert-color: ${theme.dark.accentForeground};
  --bs-alert-border-color: ${theme.dark.border};
}
`;

mkdirSync(dirname(outputPath), { recursive: true });
writeFileSync(outputPath, css, "utf8");
console.log(`Generated ${outputPath} from ${brandingPath}`);
console.log(`  App: ${appName}`);
console.log(`  Default mode: ${theme.defaultMode || "light"}`);
