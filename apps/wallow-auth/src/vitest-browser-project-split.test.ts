import { readdirSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

// Repo-config guard for Wallow-xzha.2.2: the Vitest multi-project split that
// routes component specs (*.test.tsx) into a real-Chromium browser project while
// lib specs (*.test.ts) stay on node. Plain node-environment file guard — same
// pattern as vitest-browser-mode-deps.test.ts — asserting the config shape the
// split introduces, complementing the behavioral browser-mode-smoke.test.tsx
// specs that prove a spec actually runs in Chromium.
//
// Today it FAILS: both apps' vitest.config.ts run a single `environment: "node"`
// project with the combined `src/**/*.test.{ts,tsx}` include and declare no
// browser project, playwright provider, or chromium instance.

// apps/wallow-auth/src -> repo root (src -> wallow-auth -> apps -> repo).
const repoRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");

// Both apps carry component specs and must gain the browser/node project split.
const splitApps: readonly string[] = ["apps/wallow-auth", "apps/wallow-web"];

// Concatenate every Vitest config file in an app dir — the root vitest.config.ts
// plus any split-out browser config (e.g. vitest.browser.config.ts) — so the
// guard holds whether the projects live inline in one config or across files.
function readVitestConfigSurface(appDir: string): string {
  const dir: string = resolve(repoRoot, appDir);
  const configFiles: string[] = readdirSync(dir).filter((name: string) =>
    /^vitest.*\.config\.m?[jt]s$/u.test(name),
  );
  return configFiles.map((name: string) => readFileSync(resolve(dir, name), "utf8")).join("\n");
}

describe("Vitest multi-project split (browser vs node)", () => {
  it.each(splitApps)("%s declares a Vitest projects split", (appDir: string) => {
    const surface: string = readVitestConfigSurface(appDir);
    expect(surface).toMatch(/projects\s*:/u);
  });

  it.each(splitApps)("%s declares a real-Chromium browser project", (appDir: string) => {
    const surface: string = readVitestConfigSurface(appDir);
    expect(surface).toMatch(/browser\s*:/u);
    expect(surface).toMatch(/enabled\s*:\s*true/u);
    expect(surface).toMatch(/provider\s*:\s*["'`]playwright["'`]/u);
    expect(surface).toMatch(/chromium/u);
  });

  it.each(splitApps)(
    "%s routes component specs (*.test.tsx) into the browser project",
    (appDir: string) => {
      const surface: string = readVitestConfigSurface(appDir);
      // A tsx-only include glob, distinct from the current combined
      // `*.test.{ts,tsx}` (which never contains the literal `.test.tsx`).
      expect(surface).toMatch(/\.test\.tsx/u);
    },
  );
});
