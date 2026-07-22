import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import configExport from "../vitest.config";

// Guard for Wallow-0q2s.1.3: apps/wallow-auth/vitest.config.ts is migrated off
// the hand-rolled two-project split onto @bc-solutions-coder/testing's shared
// `createVitestProjects` preset. This spec used to regex the raw config TEXT for
// the inline shape (`projects:`, `provider: "playwright"`, `chromium`); after the
// migration that shape lives INSIDE the preset, so those raw-text assertions no
// longer hold. It now asserts THROUGH the preset instead: it imports the app's
// resolved config default export and pins the emitted node/browser project shape
// (spec-to-project assignment preserved), plus checks the config delegates to the
// factory rather than hand-rolling the browser project.
//
// wallow-auth's sibling app (wallow-web) is migrated by Wallow-0q2s.1.4, which
// owns re-adding an equivalent wallow-web guard — this spec is scoped to
// wallow-auth only so the wallow-auth suite stays green independent of .1.4.

const here: string = dirname(fileURLToPath(import.meta.url));

// wallow-auth's ONE pure-logic `*.test.tsx`: `src/routes/index.test.tsx` asserts
// a route's `beforeLoad` redirect and renders no DOM, so it runs on node.
const nodeTsxSpec: string = "src/routes/index.test.tsx";

// The migrated config adopts @bc-solutions-coder/testing's shared
// `browserOptimizeDepsBaseline` (4 items). wallow-auth previously hand-rolled a
// 3-item list that OMITTED `react/jsx-runtime`; the preset baseline restores it.
const expectedBrowserOptimizeDeps: readonly string[] = [
  "vitest-browser-react",
  "react/jsx-runtime",
  "react/jsx-dev-runtime",
  "react-dom/client",
];

interface ProjectTest {
  name?: string;
  environment?: string;
  include?: string[];
  exclude?: string[];
  browser?: {
    enabled?: boolean;
    provider?: unknown;
    headless?: boolean;
    instances?: { browser?: string }[];
  };
}

interface ProjectEntry {
  test?: ProjectTest;
  optimizeDeps?: { include?: string[] };
}

interface ResolvedConfig {
  test?: { projects?: ProjectEntry[] };
}

const config = configExport as unknown as ResolvedConfig;
const projects: ProjectEntry[] = config.test?.projects ?? [];
const nodeProject: ProjectEntry | undefined = projects.find((p) => p.test?.name === "node");
const browserProject: ProjectEntry | undefined = projects.find((p) => p.test?.name === "browser");
const configText: string = readFileSync(resolve(here, "..", "vitest.config.ts"), "utf8");

describe("wallow-auth vitest.config delegates to the shared preset", () => {
  it("imports createVitestProjects from @bc-solutions-coder/testing", () => {
    expect(configText).toMatch(/@bc-solutions-coder\/testing/u);
    expect(configText).toMatch(/createVitestProjects\s*\(/u);
  });

  it("no longer hand-rolls the browser project literal (delegates it to the preset)", () => {
    // The `instances:` / bare `provider: playwright()` browser-project literal
    // now lives inside the preset, not the app config.
    expect(configText).not.toMatch(/instances\s*:/u);
    expect(configText).not.toMatch(/provider\s*:\s*playwright\(\)/u);
  });
});

describe("wallow-auth vitest.config emits the node/browser project split", () => {
  it("exposes exactly a node project and a browser project", () => {
    expect(projects).toHaveLength(2);
    expect(nodeProject).toBeDefined();
    expect(browserProject).toBeDefined();
  });

  it("routes lib specs + the one pure-logic tsx onto the node project", () => {
    expect(nodeProject?.test?.environment).toBe("node");
    expect(nodeProject?.test?.include).toEqual(["src/**/*.test.ts", nodeTsxSpec]);
  });

  it("routes component specs (*.test.tsx) onto the browser project", () => {
    expect(browserProject?.test?.include).toEqual(["src/**/*.test.tsx"]);
  });

  it("keeps the pure-logic tsx OUT of the browser project", () => {
    expect(browserProject?.test?.exclude).toContain(nodeTsxSpec);
    expect(browserProject?.test?.exclude).not.toContain("src/**/*.test.tsx");
  });

  it("runs the browser project in headless Chromium via a non-string provider", () => {
    const browser = browserProject?.test?.browser;
    expect(browser?.enabled).toBe(true);
    expect(browser?.headless).toBe(true);
    // Vitest 4 factory provider, NOT the v3 `"playwright"` string (which throws).
    expect(browser?.provider).toBeDefined();
    expect(typeof browser?.provider).not.toBe("string");
    expect(browser?.instances).toEqual([{ browser: "chromium" }]);
  });

  it("pre-bundles the shared browser optimizeDeps baseline", () => {
    expect(browserProject?.optimizeDeps?.include).toEqual([...expectedBrowserOptimizeDeps]);
  });
});
