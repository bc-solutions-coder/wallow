import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Theme-activation wiring for wallow-auth's document shell (Wallow-lrlm.1.2) —
 * the sibling of `apps/wallow-web/src/app/routes/__root.theme.test.ts`, asserting
 * the same contract on the other origin. The two apps are served from different
 * ports and a visitor crosses between them mid-login, so a theme wired into only
 * one of them shows up as the login screen flipping scheme.
 *
 * Asserted from the SOURCE rather than by rendering: `RootDocument` is not
 * exported (it reaches the router as `shellComponent`) and it renders a whole
 * `<html>/<head>/<body>` document, which `render()` cannot mount inside a test
 * container.
 *
 * What has to be true:
 *
 *   - `ThemeScript` in `<head>`, so the `.light`/`.dark` class lands on
 *     `document.documentElement` BEFORE first paint.
 *   - `ThemeProvider` wrapping the body content, so the shell's `ThemeToggle`
 *     reads and writes one source of truth.
 *   - Both fed `branding.defaultMode`, the resolution order's last resort.
 *   - `<html className={branding.defaultMode}>` stays as the SSR baseline the
 *     script corrects.
 */
const source: string = readFileSync(
  fileURLToPath(new URL("./__root.tsx", import.meta.url)),
  "utf8",
);

/** The `<head>` block, so "in the head" is asserted rather than "in the file". */
function headBlock(): string {
  const start: number = source.indexOf("<head>");
  const end: number = source.indexOf("</head>");
  expect(start, "<head> in RootDocument").toBeGreaterThan(-1);
  expect(end, "</head> in RootDocument").toBeGreaterThan(start);
  return source.slice(start, end);
}

/** The `<body>` block, likewise. */
function bodyBlock(): string {
  const start: number = source.indexOf("<body>");
  const end: number = source.indexOf("</body>");
  expect(start, "<body> in RootDocument").toBeGreaterThan(-1);
  expect(end, "</body> in RootDocument").toBeGreaterThan(start);
  return source.slice(start, end);
}

describe("wallow-auth root shell theme wiring", () => {
  it("imports the theme primitives from the shared catalog", () => {
    expect(source).toMatch(
      /import\s*\{[^}]*\bThemeProvider\b[^}]*\}\s*from\s*"@bc-solutions-coder\/ui"/su,
    );
    expect(source).toMatch(
      /import\s*\{[^}]*\bThemeScript\b[^}]*\}\s*from\s*"@bc-solutions-coder\/ui"/su,
    );
  });

  it("runs the pre-paint script inside <head>", () => {
    expect(headBlock()).toContain("<ThemeScript");
  });

  it("feeds the script the fork's default mode", () => {
    expect(headBlock()).toMatch(/<ThemeScript[^>]*defaultMode=\{branding\.defaultMode\}/su);
  });

  it("wraps the body content in the provider", () => {
    const body: string = bodyBlock();

    expect(body).toContain("<ThemeProvider");
    expect(body).toContain("</ThemeProvider>");
    expect(body.indexOf("<ThemeProvider")).toBeLessThan(body.indexOf("{children}"));
    expect(body.indexOf("</ThemeProvider>")).toBeGreaterThan(body.indexOf("{children}"));
  });

  it("feeds the provider the same default mode as the script", () => {
    expect(bodyBlock()).toMatch(/<ThemeProvider[^>]*defaultMode=\{branding\.defaultMode\}/su);
  });

  it("keeps the server-rendered default mode on <html>", () => {
    expect(source).toContain("className={branding.defaultMode}");
  });
});
