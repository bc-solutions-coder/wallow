/**
 * Acceptance guard for Wallow-pu6a.5.1: wallow-auth's component specs drive the
 * REAL SDK through `@bc-solutions-coder/testing/sdk-harness`, never a module
 * mock of the SDK.
 *
 * Why a guard rather than trust: `vi.mock("@bc-solutions-coder/sdk", ...)`
 * replaces the client surface with a hand-written object carrying whichever
 * methods the screen under test happens to call, and the spec stops covering the
 * pipeline it claims to — request serialization, the CSRF interceptor, error
 * shaping and the query cache are all mocked away, so a screen can pass its spec
 * and fail in the browser. The app's own `lib/wallow-auth-sdk.ts` facade, which
 * these specs used to mock instead, is DELETED as of Wallow-pu6a.5.5; the
 * specifier stays in the pattern below so a re-introduction is caught.
 *
 * This is the same rule `.claude/rules/TESTING.md` already states for
 * `@bc-solutions-coder/ui` ("never mock it — a component that is awkward to
 * drive is telling you the component or the spec is wrong"), applied to the SDK.
 *
 * Node project: it reads spec files off disk and never mounts anything.
 */
import { readdirSync, readFileSync } from "node:fs";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

const srcDir = dirname(fileURLToPath(import.meta.url));

/**
 * `vi.mock("<specifier>")` for the CLIENT SDK surface a screen talks to: the
 * app's facade module, the SDK barrel, and the SDK query layer.
 *
 * `@bc-solutions-coder/sdk/server/*` is deliberately NOT matched. Those are the
 * h3 passthrough/BFF presets, mocked by server-route specs that have nothing to
 * do with how a screen reaches the API; this rule is about the browser seam.
 */
const FORBIDDEN_MOCK =
  /vi\.mock\(\s*["'`]([^"'`]*wallow-auth-sdk[^"'`]*|@bc-solutions-coder\/sdk(?:\/query)?)["'`]/gu;

/** Where the rule applies: the screen specs — everything under `features/` and `app/routes/`. */
const SCOPED_DIRS = ["features", "app/routes"];

function isInScope(relativePath: string): boolean {
  return SCOPED_DIRS.some((dir) => relativePath.startsWith(`${dir}/`));
}

function specFiles(): string[] {
  // `withFileTypes` + `isFile()` matters here: Vitest browser mode writes failure
  // screenshots into `src/__screenshots__/<spec>.test.tsx/`, so a name-only
  // filter picks up DIRECTORIES ending in `.test.tsx` and `readFileSync` throws
  // EISDIR.
  return readdirSync(srcDir, { recursive: true, withFileTypes: true })
    .filter(
      (entry) =>
        entry.isFile() && (entry.name.endsWith(".test.ts") || entry.name.endsWith(".test.tsx")),
    )
    .map((entry) => join(entry.parentPath, entry.name))
    .filter((file) => isInScope(relative(srcDir, file)));
}

/** Every forbidden `vi.mock` specifier in scope, as `<relative path> -> <specifier>`. */
function offenders(): string[] {
  const found: string[] = [];
  for (const file of specFiles()) {
    const source = readFileSync(file, "utf8");
    for (const match of source.matchAll(FORBIDDEN_MOCK)) {
      found.push(`${relative(srcDir, file)} -> ${match[1]}`);
    }
  }
  return found.toSorted();
}

describe("wallow-auth SDK test seam", () => {
  it("finds screen specs to check", () => {
    // Guards the guard: a broken walk would make every assertion below vacuous.
    expect(specFiles().length).toBeGreaterThan(0);
  });

  it("mocks neither the SDK package nor the app's SDK facade in any screen spec", () => {
    expect(offenders()).toEqual([]);
  });

  it("uses the shared harness in the screen specs that exercise SDK-backed screens", () => {
    const harnessUsers = specFiles().filter((file) =>
      readFileSync(file, "utf8").includes("@bc-solutions-coder/testing/sdk-harness"),
    );

    expect(harnessUsers.length).toBeGreaterThan(0);
  });
});
