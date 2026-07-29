/**
 * Wiring guard for the two new entries added by Wallow-pu6a.5.1
 * (`./sdk-harness`, `./render-with-wallow`).
 *
 * The "correctness" of a new package
 * entry is its plumbing — an exports-map entry, a Vite lib entry so a bundle is
 * emitted, and a `tsconfig.build.json` include so declarations are. Miss any one
 * and consumers fail at import time with a build that "succeeded".
 *
 * The barrel-purity assertion is the load-bearing one: `@bc-solutions-coder/testing`'s
 * `.` entry is imported by every app's `vitest.config.ts` in a plain Node
 * process, and `render-with-wallow.tsx` imports `vitest-browser-react`, which
 * evaluates `vitest/browser` at import and throws outside browser mode. Adding
 * it to the barrel breaks every config in the workspace.
 */
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

const packageDir = join(dirname(fileURLToPath(import.meta.url)), "..");

function readPackageJson(): Record<string, unknown> {
  return JSON.parse(readFileSync(join(packageDir, "package.json"), "utf8")) as Record<
    string,
    unknown
  >;
}

function readText(relativePath: string): string {
  return readFileSync(join(packageDir, relativePath), "utf8");
}

function stripLineComments(text: string): string {
  return text.replaceAll(/^\s*\/\/.*$/gmu, "");
}

describe("@bc-solutions-coder/testing sdk test-seam entries", () => {
  it("exposes ./sdk-harness and ./render-with-wallow on the exports map", () => {
    const exportsMap = readPackageJson().exports as Record<
      string,
      { types?: string; import?: string }
    >;

    expect(exportsMap["./sdk-harness"]).toEqual({
      types: "./dist/sdk-harness.d.ts",
      import: "./dist/sdk-harness.js",
    });
    expect(exportsMap["./render-with-wallow"]).toEqual({
      types: "./dist/render-with-wallow.d.ts",
      import: "./dist/render-with-wallow.js",
    });
  });

  it("keeps render-with-wallow off the config-safe '.' barrel", () => {
    const barrel = readText("src/index.ts");

    expect(barrel).not.toContain("render-with-wallow");
  });

  it("depends on the SDK it builds instances of", () => {
    const pkg = readPackageJson();
    const deps = pkg.dependencies as Record<string, string>;

    // The harness hands a fake `fetch` to the REAL `createWallowSdk()`, so the
    // SDK is a genuine runtime dependency here, not a peer.
    expect(deps["@bc-solutions-coder/sdk"]).toBe("workspace:*");
  });

  it("declares the render peers renderWithWallow mounts against", () => {
    const peers = readPackageJson().peerDependencies as Record<string, string>;

    expect(peers).toHaveProperty("react");
    expect(peers).toHaveProperty("react-dom");
    expect(peers).toHaveProperty("@tanstack/react-query");
    expect(peers).toHaveProperty("@tanstack/react-router");
  });

  it("emits both entries from the Vite library build", () => {
    const viteConfig = readText("vite.config.ts");

    expect(viteConfig).toContain("src/sdk-harness.ts");
    expect(viteConfig).toContain("src/render-with-wallow.tsx");
  });

  it("emits declarations for both entries", () => {
    const buildConfig = JSON.parse(stripLineComments(readText("tsconfig.build.json"))) as {
      include?: string[];
      exclude?: string[];
    };

    expect(buildConfig.include).toContain("src/sdk-harness.ts");
    expect(buildConfig.include).toContain("src/render-with-wallow.tsx");
    // Browser specs are `.tsx`; the original exclude list only named `*.test.ts`,
    // which would drag render-with-wallow.test.tsx into the declaration program.
    expect(buildConfig.exclude).toContain("**/*.test.tsx");
  });

  it("runs its own specs on the shared node/browser project split", () => {
    // This package gained its first component spec with this task, so its
    // single-config `environment: "node"` setup no longer covers the suite. It
    // dogfoods `createVitestProjects` rather than hand-rolling a second config.
    const vitestConfig = readText("vitest.config.ts");

    expect(vitestConfig).toContain("createVitestProjects");
    expect(vitestConfig).toMatch(/projects\s*:/u);
  });
});
