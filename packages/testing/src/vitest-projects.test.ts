import { configDefaults } from "vitest/config";
import { describe, expect, it } from "vitest";

import { browserOptimizeDepsBaseline } from "./browser-optimize-deps";
import { createVitestProjects, ssrSpecGlob } from "./vitest-projects";

// Unit guard for Wallow-0q2s.1.2: createVitestProjects emits the shared node +
// real-Chromium browser project pair that apps/wallow-auth/vitest.config.ts and
// apps/wallow-web/vitest.config.ts hand-roll today.
//
// These specs assert MEMBERSHIP, not exact arrays. An `include`/`exclude`
// pinned with `toEqual` makes adding one glob to the preset a test-editing
// exercise before it can be a preset-editing one, while proving nothing beyond
// "this glob is routed to this project" — which `toContain` proves too. The
// contract callers depend on is that each project receives its globs and that
// overrides layer ON TOP of the preset's rather than replacing them; both
// survive a longer list, and both are still asserted below.
//
// The provider spec that used to sit here asserted `provider` was not the
// string `"playwright"` (the Vitest 3 spelling, which throws in v4). It was
// unfalsifiable in practice: the browser projects in this workspace cannot boot
// at all with a bad provider, so every browser-mode run in `pnpm test` already
// proves it — and it would have to be rewritten on any future Vitest major that
// changes the provider shape again.

describe("createVitestProjects — default (no options)", () => {
  it("returns a node + browser project pair", () => {
    const { node, browser } = createVitestProjects();

    expect(node).toBeDefined();
    expect(browser).toBeDefined();
  });

  it("configures the node project for pure-logic specs", () => {
    const { node } = createVitestProjects();

    expect(node.test.name).toBe("node");
    expect(node.test.environment).toBe("node");
    expect(node.test.include).toContain("src/**/*.test.ts");
    for (const excluded of configDefaults.exclude) {
      expect(node.test.exclude).toContain(excluded);
    }
  });

  it("configures the browser project for component specs in headless Chromium", () => {
    const { browser } = createVitestProjects();

    expect(browser.test.name).toBe("browser");
    expect(browser.test.include).toContain("src/**/*.test.tsx");
    for (const excluded of configDefaults.exclude) {
      expect(browser.test.exclude).toContain(excluded);
    }

    expect(browser.test.browser.enabled).toBe(true);
    expect(browser.test.browser.headless).toBe(true);
    expect(browser.test.browser.instances).toEqual([{ browser: "chromium" }]);
  });

  it("pre-bundles the shared browser optimizeDeps baseline", () => {
    const { browser } = createVitestProjects();

    for (const dep of browserOptimizeDepsBaseline) {
      expect(browser.optimizeDeps.include).toContain(dep);
    }
  });
});

describe("createVitestProjects — nodeTsxSpecs routing", () => {
  // The convention IS the default, so the routing contract is asserted on a
  // caller that passes nothing — the shape every consumer now uses. Both apps
  // used to hand-list their SSR specs here and in their own configs.
  it("routes the *.ssr.test.tsx convention onto node by default", () => {
    const { node, browser } = createVitestProjects();

    expect(node.test.include).toContain(ssrSpecGlob);
    expect(browser.test.exclude).toContain(ssrSpecGlob);
    expect(browser.test.include).not.toContain(ssrSpecGlob);
  });

  it("still honours an explicit nodeTsxSpecs list", () => {
    const nodeTsxSpecs = ["src/routes/__root.test.tsx", "src/router.test.tsx"];
    const { node, browser } = createVitestProjects({ nodeTsxSpecs });

    expect(node.test.include).toContain("src/**/*.test.ts");
    for (const spec of nodeTsxSpecs) {
      expect(node.test.include).toContain(spec);
      expect(browser.test.exclude).toContain(spec);
      expect(browser.test.include).not.toContain(spec);
    }
    // An explicit list REPLACES the convention rather than adding to it, so a
    // caller who opts out is not silently still matching `*.ssr.test.tsx`.
    expect(node.test.include).not.toContain(ssrSpecGlob);
    // The default vitest excludes are still layered in alongside the node specs.
    for (const excluded of configDefaults.exclude) {
      expect(browser.test.exclude).toContain(excluded);
    }
  });

  it("keeps the browser include glob unchanged regardless of node-tsx specs", () => {
    const { browser } = createVitestProjects({ nodeTsxSpecs: ["src/router.test.tsx"] });

    expect(browser.test.include).toContain("src/**/*.test.tsx");
  });
});

// `browserPlugins` / `browserSetupFiles` get no spec here on purpose. They are
// pass-throughs, and breaking one is LOUD rather than silent: with no stylesheet
// a ui control measures 0x0 and every spec that clicks it hangs to Playwright's
// actionability timeout. The two apps' `shared/testing/browser-styles-wiring.test.ts`
// already name the wiring at the consumer end, where a failure can say which half
// went missing.

describe("createVitestProjects — extraBrowserOptimizeDeps", () => {
  it("appends app-specific optimizeDeps onto the shared baseline", () => {
    const extra = ["@tanstack/react-query", "@tanstack/react-router"];
    const { browser } = createVitestProjects({ extraBrowserOptimizeDeps: extra });

    for (const dep of browserOptimizeDepsBaseline) {
      expect(browser.optimizeDeps.include).toContain(dep);
    }
    for (const dep of extra) {
      expect(browser.optimizeDeps.include).toContain(dep);
    }
  });
});

describe("createVitestProjects — nodeProjectOverrides pass-through", () => {
  const overrides = {
    resolve: { alias: { "openid-client": "/resolved/openid-client/index.js" } },
    test: { server: { deps: { inline: [/packages[/\\]sdk/u] } } },
  };

  it("deep-merges app-local overrides into the node project", () => {
    const { node } = createVitestProjects({ nodeProjectOverrides: overrides });

    // Top-level override key is applied.
    expect(node.resolve).toEqual(overrides.resolve);
    // Nested test-level override is merged WITHOUT clobbering the preset fields.
    expect(node.test.server).toEqual(overrides.test.server);
    expect(node.test.name).toBe("node");
    expect(node.test.environment).toBe("node");
    expect(node.test.include).toContain("src/**/*.test.ts");
  });

  it("concatenates array fields onto the preset's, matching vitest's mergeConfig", () => {
    const extraExclude = "src/legacy/**";
    const { node } = createVitestProjects({
      nodeProjectOverrides: { test: { exclude: [extraExclude] } },
    });

    // Concatenated, not replaced: the caller's entry AND the preset's survive.
    expect(node.test.exclude).toContain(extraExclude);
    for (const excluded of configDefaults.exclude) {
      expect(node.test.exclude).toContain(excluded);
    }
  });

  it("does not leak node overrides into the browser project", () => {
    const { browser } = createVitestProjects({ nodeProjectOverrides: overrides });

    expect(browser).not.toHaveProperty("resolve");
    expect(browser.test).not.toHaveProperty("server");
  });
});
