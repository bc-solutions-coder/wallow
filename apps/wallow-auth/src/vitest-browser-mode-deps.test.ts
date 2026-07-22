import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

// Repo-config guard for Wallow-xzha.2.1: the Vitest 4 + browser-mode dependency
// bump. This spec asserts the manifests/workflow the bump edits — it is a plain
// node-environment file guard (no jsdom, no DOM), the same pattern as
// service-identity.test.ts and sdk-publish-workflow.test.ts. The actual real-
// Chromium browser-mode config lands in Wallow-xzha.2.2, so this bead's red
// artifact verifies the deps + CI plumbing exist, not that a browser spec runs.

// apps/wallow-auth/src -> repo root (src -> wallow-auth -> apps -> repo).
const repoRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");

interface PackageJson {
  dependencies?: Record<string, string>;
  devDependencies?: Record<string, string>;
}

function readPackageJson(relativeDir: string): PackageJson {
  const raw: string = readFileSync(resolve(repoRoot, relativeDir, "package.json"), "utf8");
  return JSON.parse(raw) as PackageJson;
}

function devDep(pkg: PackageJson, name: string): string | undefined {
  return pkg.devDependencies?.[name];
}

const workspacePackages: readonly string[] = [
  "apps/wallow-auth",
  "apps/wallow-web",
  "packages/sdk",
  "packages/styles",
];

// Packages that carry component specs and therefore need the browser-mode stack.
const componentTestApps: readonly string[] = ["apps/wallow-auth", "apps/wallow-web"];

// Pure-TS packages that bump Vitest for workspace consistency but must NOT pull
// in the browser-mode packages (no DOM today).
const pureTsPackages: readonly string[] = ["packages/sdk", "packages/styles"];

const browserModePackages: readonly string[] = [
  "@vitest/browser-playwright",
  "playwright",
  "vitest-browser-react",
];

describe("Vitest 4 bump across the pnpm workspace", () => {
  it.each(workspacePackages)("%s pins vitest to ^4.1.10", (dir: string) => {
    const pkg: PackageJson = readPackageJson(dir);
    expect(devDep(pkg, "vitest")).toBe("^4.1.10");
  });

  it("no workspace package is left on the old vitest ^2 line", () => {
    for (const dir of workspacePackages) {
      const pkg: PackageJson = readPackageJson(dir);
      expect(devDep(pkg, "vitest")).not.toMatch(/^\^?2\./u);
    }
  });
});

describe("browser-mode devDependencies in the component-test apps", () => {
  it.each(componentTestApps)("%s declares @vitest/browser-playwright ^4.1.10", (dir: string) => {
    const pkg: PackageJson = readPackageJson(dir);
    expect(devDep(pkg, "@vitest/browser-playwright")).toBe("^4.1.10");
  });

  it.each(componentTestApps)(
    "%s declares the raw playwright automation package ^1.61.1",
    (dir: string) => {
      const pkg: PackageJson = readPackageJson(dir);
      expect(devDep(pkg, "playwright")).toBe("^1.61.1");
    },
  );

  it.each(componentTestApps)("%s declares vitest-browser-react ^2.2.0", (dir: string) => {
    const pkg: PackageJson = readPackageJson(dir);
    expect(devDep(pkg, "vitest-browser-react")).toBe("^2.2.0");
  });

  it.each(componentTestApps)(
    "%s does NOT list bare @vitest/browser (the provider package pulls it in)",
    (dir: string) => {
      const pkg: PackageJson = readPackageJson(dir);
      expect(devDep(pkg, "@vitest/browser")).toBeUndefined();
    },
  );
});

describe("component-test apps consume the shared @bc-solutions-coder/testing preset", () => {
  // Wallow-0q2s.1.3: wallow-auth migrates its vitest.config onto
  // @bc-solutions-coder/testing's createVitestProjects preset and therefore
  // depends on the workspace package. (wallow-web's equivalent dep is added by
  // .1.4; the browser-mode devDeps above stay because component specs still
  // import `render` from vitest-browser-react directly.)
  it("apps/wallow-auth declares @bc-solutions-coder/testing as a workspace dependency", () => {
    const pkg: PackageJson = readPackageJson("apps/wallow-auth");
    const dep: string | undefined =
      pkg.dependencies?.["@bc-solutions-coder/testing"] ??
      pkg.devDependencies?.["@bc-solutions-coder/testing"];
    expect(dep).toBe("workspace:*");
  });
});

describe("pure-TS packages stay off the browser-mode stack", () => {
  it.each(pureTsPackages)("%s adds no browser-mode packages", (dir: string) => {
    const pkg: PackageJson = readPackageJson(dir);
    for (const name of browserModePackages) {
      expect(devDep(pkg, name)).toBeUndefined();
    }
  });
});

describe("JS CI installs Chromium before running Vitest", () => {
  const workflow: string = readFileSync(
    resolve(repoRoot, ".github", "workflows", "js.yml"),
    "utf8",
  );

  it("runs `playwright install --with-deps chromium`", () => {
    expect(workflow).toMatch(/playwright install --with-deps chromium/u);
  });

  it("installs the browser before the Test step (not after)", () => {
    const installIdx: number = workflow.search(/playwright install --with-deps chromium/u);
    const testStepIdx: number = workflow.search(/run:\s*pnpm (run )?test\b/u);
    expect(installIdx).toBeGreaterThanOrEqual(0);
    expect(testStepIdx).toBeGreaterThanOrEqual(0);
    expect(installIdx).toBeLessThan(testStepIdx);
  });
});
