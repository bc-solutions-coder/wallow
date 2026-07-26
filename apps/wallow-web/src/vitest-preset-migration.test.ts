import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import webVitestConfig from "../vitest.config";

/**
 * Migration guard for Wallow-0q2s.1.4: apps/wallow-web/vitest.config.ts must be
 * built from `@bc-solutions-coder/testing`'s `createVitestProjects` preset rather
 * than hand-rolling the node + real-Chromium two-project split inline.
 *
 * The app config is expected to collapse to app-local knobs only — the 7-entry
 * `nodeTsxSpecs` list, the `@tanstack/*` `extraBrowserOptimizeDeps`, and the
 * app-local `nodeProjectOverrides` (the BFF seam: `resolve.alias['openid-client']`
 * + `server.deps.inline`) — plus one `createVitestProjects` call. The browser
 * provider machinery (`@vitest/browser-playwright`, `headless`, `chromium`
 * instances) moves entirely into the preset.
 *
 * CRITICAL preserved behavior (bead acceptance): bff-server.test.ts module-mocks
 * `openid-client`, so after the migration the emitted node project MUST still
 * carry the `openid-client` alias and the inlined workspace SDK, or the mock stops
 * intercepting. The behavioral block below pins that seam through the preset.
 *
 * Today the STRUCTURAL assertions FAIL: the config hand-rolls the split and
 * imports `@vitest/browser-playwright` directly, and package.json does not yet
 * depend on `@bc-solutions-coder/testing`. They go green only once the config is
 * rewritten onto the preset.
 */

interface PackageJson {
  dependencies?: Record<string, string>;
  devDependencies?: Record<string, string>;
}

interface EmittedNodeProject {
  resolve?: { alias?: Record<string, unknown> };
  test: {
    include: string[];
    server?: { deps?: { inline?: unknown[] } };
  };
}

interface EmittedBrowserProject {
  optimizeDeps: { include: string[] };
}

interface EmittedVitestConfig {
  test: { projects: [EmittedNodeProject, EmittedBrowserProject] };
}

const configPath: string = fileURLToPath(new URL("../vitest.config.ts", import.meta.url));
const configSource: string = readFileSync(configPath, "utf8");

const packageJsonPath: string = fileURLToPath(new URL("../package.json", import.meta.url));
const packageJsonRaw: string = readFileSync(packageJsonPath, "utf8");
const packageJson: PackageJson = JSON.parse(packageJsonRaw) as PackageJson;

// The 7 pure-logic / SSR `*.test.tsx` specs that must stay on the node project.
const nodeTsxSpecs: readonly string[] = [
  "src/router.test.tsx",
  "src/routes/index.test.tsx",
  "src/routes/__root.test.tsx",
  "src/routes/__root.branding.test.tsx",
  "src/routes/__root.hydration.test.tsx",
  "src/routes/__root.provider.test.tsx",
  "src/routes/dashboard/route.test.tsx",
];

// The browser render/runtime libraries wallow-web pre-bundles beyond the shared
// baseline — the exact set the migration carries via `extraBrowserOptimizeDeps`.
const requiredBrowserOptimizeDeps: readonly string[] = [
  "vitest-browser-react",
  "react",
  "react/jsx-runtime",
  "react/jsx-dev-runtime",
  "react-dom",
  "react-dom/client",
  "@tanstack/react-query",
  "@tanstack/react-router",
  "@tanstack/react-form",
];

function nodeProject(): EmittedNodeProject {
  return (webVitestConfig as EmittedVitestConfig).test.projects[0];
}

function browserProject(): EmittedBrowserProject {
  return (webVitestConfig as EmittedVitestConfig).test.projects[1];
}

describe("wallow-web depends on the shared @bc-solutions-coder/testing preset", () => {
  it("declares @bc-solutions-coder/testing as a workspace dependency", () => {
    const range: string | undefined =
      packageJson.dependencies?.["@bc-solutions-coder/testing"] ??
      packageJson.devDependencies?.["@bc-solutions-coder/testing"];
    expect(range).toBeDefined();
    expect(range).toMatch(/^workspace:/u);
  });
});

describe("wallow-web vitest.config.ts is built from createVitestProjects", () => {
  it("imports createVitestProjects from @bc-solutions-coder/testing", () => {
    expect(configSource).toMatch(
      /import\s*\{[^}]*\bcreateVitestProjects\b[^}]*\}\s*from\s*["'`]@bc-solutions-coder\/testing["'`]/u,
    );
  });

  it("calls the createVitestProjects factory", () => {
    expect(configSource).toMatch(/createVitestProjects\s*\(/u);
  });

  it("passes the app-local BFF knobs through nodeProjectOverrides", () => {
    expect(configSource).toMatch(/nodeProjectOverrides/u);
  });

  it("no longer imports the browser provider directly (the preset owns it)", () => {
    expect(configSource).not.toMatch(/from\s*["'`]@vitest\/browser-playwright["'`]/u);
  });

  it("no longer inlines the browser instance machinery (the preset owns it)", () => {
    expect(configSource).not.toMatch(/headless\s*:/u);
    expect(configSource).not.toMatch(/instances\s*:/u);
  });
});

describe("wallow-web preset output preserves the two-project split", () => {
  it("emits exactly the node and browser projects", () => {
    expect((webVitestConfig as EmittedVitestConfig).test.projects).toHaveLength(2);
  });

  it("keeps every node-tsx spec on the node project", () => {
    const include: string[] = nodeProject().test.include;
    expect(include).toContain("src/**/*.test.ts");
    expect(include).toEqual(expect.arrayContaining([...nodeTsxSpecs]));
  });

  it("preserves the openid-client alias so bff-server.test.ts's mock intercepts", () => {
    const alias: Record<string, unknown> | undefined = nodeProject().resolve?.alias;
    expect(alias?.["openid-client"]).toBeDefined();
    expect(typeof alias?.["openid-client"]).toBe("string");
  });

  it("keeps the workspace SDK inlined so the openid-client mock reaches it", () => {
    const inline: unknown[] | undefined = nodeProject().test.server?.deps?.inline;
    expect(inline).toBeDefined();
    const inlinesSdk: boolean = (inline ?? []).some(
      (entry: unknown) => entry instanceof RegExp && entry.test("node_modules/packages/sdk/x"),
    );
    expect(inlinesSdk).toBe(true);
  });

  it("pre-bundles every browser render/runtime library wallow-web renders", () => {
    const include: string[] = browserProject().optimizeDeps.include;
    expect(include).toEqual(expect.arrayContaining([...requiredBrowserOptimizeDeps]));
  });
});
