#!/usr/bin/env node
// Verifies the normalized package.json script surface across the workspace.
// Root gets the full standardized script set (build/test/typecheck/lint/lint:fix/
// format/format:check/check); every workspace member exposes the standard member
// script set. Exits non-zero if any expected script key is missing or mismatched.
//
// Usage: node scripts/check-workspace-scripts.mjs

import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = join(import.meta.dirname, "..");

function loadScripts(relPath) {
  const abs = join(repoRoot, relPath);
  const pkg = JSON.parse(readFileSync(abs, "utf8"));
  return pkg.scripts ?? {};
}

// Exact expected values for the root package.json scripts block.
const ROOT_EXPECTED = {
  build: "pnpm -r build",
  test: "pnpm -r test",
  typecheck: "pnpm -r typecheck",
  lint: "oxlint apps packages --deny-warnings",
  "lint:fix": "oxlint apps packages --fix",
  format:
    "oxfmt --write apps packages package.json pnpm-workspace.yaml tsconfig.base.json .oxlintrc.json .oxfmtrc.json",
  "format:check":
    "oxfmt --check apps packages package.json pnpm-workspace.yaml tsconfig.base.json .oxlintrc.json .oxfmtrc.json",
  check: "pnpm format:check && pnpm lint && pnpm typecheck && pnpm test && pnpm build",
};

// Root backend scripts must be preserved (uncommitted, do NOT clobber).
const ROOT_PRESERVED = ["backend", "backend:infra", "backend:infra:down"];

// Standard member script keys each workspace member must expose.
const SDK_REQUIRED = ["build", "test", "test:watch", "typecheck"];
const APP_REQUIRED = ["dev", "build", "start", "typecheck", "test", "test:watch"];

const failures = [];
let checks = 0;

function assertExact(scope, scripts, key, expected) {
  checks++;
  if (scripts[key] !== expected) {
    failures.push(
      `${scope}: script "${key}" = ${JSON.stringify(scripts[key])}, expected ${JSON.stringify(expected)}`,
    );
  }
}

function assertPresent(scope, scripts, key) {
  checks++;
  if (typeof scripts[key] !== "string" || scripts[key].length === 0) {
    failures.push(`${scope}: missing required script "${key}"`);
  }
}

const rootScripts = loadScripts("package.json");
for (const [key, expected] of Object.entries(ROOT_EXPECTED)) {
  assertExact("root", rootScripts, key, expected);
}
for (const key of ROOT_PRESERVED) {
  assertPresent("root", rootScripts, key);
}

const sdkScripts = loadScripts("packages/sdk/package.json");
for (const key of SDK_REQUIRED) {
  assertPresent("packages/sdk", sdkScripts, key);
}

const appScripts = loadScripts("apps/wallow-web/package.json");
for (const key of APP_REQUIRED) {
  assertPresent("apps/wallow-web", appScripts, key);
}

if (failures.length > 0) {
  console.error(`FAIL: ${failures.length}/${checks} workspace script checks failed:`);
  for (const f of failures) {
    console.error(`  - ${f}`);
  }
  process.exit(1);
}

console.log(`PASS: all ${checks} workspace script checks passed.`);
