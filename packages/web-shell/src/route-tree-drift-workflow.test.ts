import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * CI route-tree drift guard for Wallow-w6s6.1.5 (T1.5).
 *
 * Once T1.2 made `routes:generate` deterministic and T1.3/T1.4 switched both
 * apps' routers onto the committed `routeTree.gen.ts`, the generated trees can
 * silently drift from their `src/routes/**` sources whenever a developer edits a
 * route file without re-running the generator. This workflow closes that gap the
 * same way `.github/workflows/openapi-drift.yml` guards the OpenAPI snapshot:
 * regenerate in CI and fail on `git diff --exit-code`.
 *
 * Route-tree generation is PURE JS/TS — no API, Postgres, or Valkey — so unlike
 * openapi-drift.yml this workflow must NOT boot any backend services. These specs
 * pin that shape plus the trigger paths, the least-privilege permissions, and the
 * regenerate-then-diff-both-trees core.
 *
 * The workflow YAML is read through `fs` as text (never parsed/imported) so the
 * assertions describe the committed file exactly; they fail until the green phase
 * creates `.github/workflows/route-tree-drift.yml`.
 */

// packages/web-shell/src -> packages/web-shell -> packages -> repo root.
const repoRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
const workflowPath: string = resolve(repoRoot, ".github", "workflows", "route-tree-drift.yml");

function readWorkflow(): string {
  return existsSync(workflowPath) ? readFileSync(workflowPath, "utf8") : "";
}

describe("route-tree-drift GitHub Actions workflow", () => {
  it("exists at .github/workflows/route-tree-drift.yml", () => {
    expect(existsSync(workflowPath)).toBe(true);
  });

  it("triggers on pull_request and push to main", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/on:/);
    expect(yaml).toMatch(/pull_request:/);
    expect(yaml).toMatch(/push:/);
    expect(yaml).toMatch(/branches:\s*\[\s*main\s*\]/);
  });

  it("scopes trigger paths to route sources, generated trees, tsr configs, and web-shell seams", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/paths:/);
    // Editing a route source or its generated tree in either app must trip the check.
    expect(yaml).toMatch(/apps\/wallow-\*\/src\/routes\/\*\*/);
    expect(yaml).toMatch(/apps\/wallow-\*\/src\/routeTree\.gen\.ts/);
    // tsr.config.json changes the codegen output, so it is in scope too.
    expect(yaml).toMatch(/apps\/wallow-\*\/tsr\.config\.json/);
    // The web-shell seams that wire the tanstackRouter plugin also affect the tree.
    expect(yaml).toMatch(/packages\/web-shell\/src\/server\//);
    // The workflow guards itself.
    expect(yaml).toMatch(/\.github\/workflows\/route-tree-drift\.yml/);
  });

  it("declares least-privilege permissions (contents: read)", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/permissions:/);
    expect(yaml).toMatch(/contents:\s*read/);
  });

  it("runs the drift job on ubuntu-latest", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/runs-on:\s*ubuntu-latest/);
  });

  it("is pure JS/TS — boots no backend services (unlike openapi-drift.yml)", () => {
    const yaml: string = readWorkflow();
    // Route-tree generation needs no database, cache, or .NET runtime.
    expect(yaml).not.toMatch(/services:/);
    expect(yaml).not.toMatch(/postgres/i);
    expect(yaml).not.toMatch(/valkey/i);
    expect(yaml).not.toMatch(/setup-dotnet/);
  });

  it("checks out the repository", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/uses:\s*actions\/checkout@v\d+/);
  });

  it("enables corepack before setting up node", () => {
    const yaml: string = readWorkflow();
    const corepackIndex: number = readWorkflow().search(/corepack enable/);
    const setupNodeIndex: number = readWorkflow().search(/uses:\s*actions\/setup-node@v\d+/);
    expect(corepackIndex).toBeGreaterThanOrEqual(0);
    expect(setupNodeIndex).toBeGreaterThanOrEqual(0);
    expect(corepackIndex).toBeLessThan(setupNodeIndex);
    expect(yaml).toMatch(/corepack enable/);
  });

  it("sets up node from .nvmrc with the pnpm cache", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/uses:\s*actions\/setup-node@v\d+/);
    expect(yaml).toMatch(/node-version-file:\s*['"]?\.nvmrc['"]?/);
    expect(yaml).toMatch(/cache:\s*['"]?pnpm['"]?/);
  });

  it("installs workspace dependencies with a frozen lockfile", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/pnpm install --frozen-lockfile/);
  });

  it("regenerates both apps' route trees via routes:generate", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/pnpm --filter @bc-solutions-coder\/wallow-auth (run )?routes:generate/);
    expect(yaml).toMatch(/pnpm --filter @bc-solutions-coder\/wallow-web (run )?routes:generate/);
  });

  it("fails on drift via git diff --exit-code against both routeTree.gen.ts files with a helpful ::error:: message", () => {
    const yaml: string = readWorkflow();
    expect(yaml).toMatch(/git diff --exit-code/);
    expect(yaml).toMatch(/apps\/wallow-auth\/src\/routeTree\.gen\.ts/);
    expect(yaml).toMatch(/apps\/wallow-web\/src\/routeTree\.gen\.ts/);
    expect(yaml).toMatch(/::error::/);
  });
});
