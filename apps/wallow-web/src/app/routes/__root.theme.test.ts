import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Theme-activation wiring for wallow-web's document shell (Wallow-lrlm.1.2).
 *
 * The shell is asserted from its SOURCE rather than by rendering it, because
 * there is nothing to render: `RootDocument` is not exported — it reaches the
 * router as `shellComponent` — and it renders a whole `<html>/<head>/<body>`
 * document, which `render()` cannot mount inside a test container. Reading the
 * file is the same approach `docker-workspace-copies.test.ts` and
 * `brand-assets.test.ts` take to wiring that only exists as declarations.
 *
 * What has to be true, and why each part matters:
 *
 *   - `ThemeScript` in `<head>`. The `.light`/`.dark` class must be on
 *     `document.documentElement` BEFORE first paint; anything that runs after
 *     hydration produces the flash of wrong theme this task exists to remove.
 *   - `ThemeProvider` wrapping the body content, so `ThemeToggle` anywhere in
 *     the tree reads and writes one source of truth.
 *   - Both fed `branding.defaultMode` — the fork's `api/branding.json` value,
 *     which is the LAST resort in the resolution order (localStorage, then
 *     prefers-color-scheme, then this).
 *   - `<html className={branding.defaultMode}>` stays. It is the server's best
 *     guess and what makes the SSR markup themed at all; the script's job is to
 *     correct it before paint, not to replace it.
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

describe("wallow-web root shell theme wiring", () => {
  it("imports the theme primitives from the shared catalog", () => {
    // From @bc-solutions-coder/ui, never a per-app copy: wallow-auth's shell
    // gets the identical treatment from the identical components.
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
    // The provider has to be OUTSIDE `{children}`, or a page's own toggle sits
    // above the context it is meant to drive.
    expect(body.indexOf("<ThemeProvider")).toBeLessThan(body.indexOf("{children}"));
    expect(body.indexOf("</ThemeProvider>")).toBeGreaterThan(body.indexOf("{children}"));
  });

  it("feeds the provider the same default mode as the script", () => {
    expect(bodyBlock()).toMatch(/<ThemeProvider[^>]*defaultMode=\{branding\.defaultMode\}/su);
  });

  it("keeps the server-rendered default mode on <html>", () => {
    // The SSR baseline the script corrects. Dropping it would ship an unthemed
    // first paint to every visitor with JS disabled or still downloading.
    expect(source).toContain("className={branding.defaultMode}");
  });
});
