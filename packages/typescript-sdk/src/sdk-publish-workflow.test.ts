import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

// packages/typescript-sdk/src -> packages/typescript-sdk
const packageRoot: string = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "..",
);
// packages/typescript-sdk -> repo root
const repoRoot: string = resolve(packageRoot, "..", "..");
const workflowPath: string = resolve(
  repoRoot,
  ".github",
  "workflows",
  "sdk-publish.yml",
);

function readWorkflow(): string {
  return readFileSync(workflowPath, "utf8");
}

describe("sdk-publish GitHub Actions workflow", () => {
  it("exists at .github/workflows/sdk-publish.yml", () => {
    expect(existsSync(workflowPath)).toBe(true);
  });

  it("triggers on release published", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/on:/);
    expect(yaml).toMatch(/release:/);
    expect(yaml).toMatch(/types:\s*\[\s*published\s*\]/);
  });

  it("declares least-privilege permissions (contents:read, packages:write)", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/permissions:/);
    expect(yaml).toMatch(/contents:\s*read/);
    expect(yaml).toMatch(/packages:\s*write/);
  });

  it("runs on ubuntu-latest with the SDK as the working directory", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/runs-on:\s*ubuntu-latest/);
    expect(yaml).toMatch(/working-directory:\s*packages\/typescript-sdk/);
  });

  it("checks out the repository", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/uses:\s*actions\/checkout@v\d+/);
  });

  it("sets up node with the GitHub Packages registry", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/uses:\s*actions\/setup-node@v\d+/);
    expect(yaml).toMatch(/node-version:\s*['"]?20['"]?/);
    expect(yaml).toMatch(/registry-url:\s*['"]?https:\/\/npm\.pkg\.github\.com['"]?/);
  });

  it("installs, builds, and tests before publishing", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/npm ci/);
    expect(yaml).toMatch(/npm run build/);
    expect(yaml).toMatch(/npm (run )?test/);
  });

  it("publishes to GitHub Packages using the GITHUB_TOKEN", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/npm publish/);
    expect(yaml).toMatch(/NODE_AUTH_TOKEN:\s*\$\{\{\s*secrets\.GITHUB_TOKEN\s*\}\}/);
  });
});
