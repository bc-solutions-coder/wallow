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

  it("triggers on sdk-v* tag pushes only (not platform vX.Y.Z releases)", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/on:/);
    expect(yaml).toMatch(/push:/);
    expect(yaml).toMatch(/tags:/);
    expect(yaml).toMatch(/['"]?sdk-v\*['"]?/);
    // Must NOT be wired to release-please platform releases.
    expect(yaml).not.toMatch(/release:/);
    expect(yaml).not.toMatch(/types:\s*\[\s*published\s*\]/);
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

  it("sets up node with the GitHub Packages registry and repo-owner scope", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/uses:\s*actions\/setup-node@v\d+/);
    expect(yaml).toMatch(/node-version:\s*['"]?20['"]?/);
    expect(yaml).toMatch(/registry-url:\s*['"]?https:\/\/npm\.pkg\.github\.com['"]?/);
    expect(yaml).toMatch(/scope:\s*['"]?@bc-solutions-coder['"]?/);
  });

  it("supports manual dispatch with a required version input", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/workflow_dispatch:/);
    expect(yaml).toMatch(/inputs:/);
    expect(yaml).toMatch(/version:/);
    expect(yaml).toMatch(/required:\s*true/);
  });

  it("derives the version from the sdk-v tag prefix or dispatch input", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/npm version .*--no-git-tag-version/);
    // Tag push strips the sdk-v prefix; manual dispatch uses inputs.version.
    expect(yaml).toMatch(/#sdk-v/);
    expect(yaml).toMatch(/github\.ref_name/);
    expect(yaml).toMatch(/inputs\.version/);
    // Legacy release-based derivation must be gone.
    expect(yaml).not.toMatch(/github\.event\.release\.tag_name/);
  });

  it("fails the job when no version can be resolved", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/if \[ -z "\$VERSION" \]/);
    expect(yaml).toMatch(/exit 1/);
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
