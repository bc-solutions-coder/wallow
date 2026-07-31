/**
 * Barrel purity for `@bc-solutions-coder/testing` (Wallow-pu6a.5.1, Wallow-8ytl).
 *
 * The `.` entry is imported by every app's `vitest.config.ts` in a plain Node
 * process, at config-load time. `render-with-wallow.tsx` imports
 * `vitest-browser-react`, which evaluates `vitest/browser` at import and throws
 * outside browser mode; `contrast.ts` touches `document` and a canvas. Either
 * one on the barrel breaks every Vitest config in the workspace — not this
 * package's suite, every other package's.
 *
 * That failure is invisible to types and to lint (the import is legal, it just
 * cannot be evaluated in Node), which is why it is a spec.
 *
 * The seven specs that used to accompany it asserted the PLUMBING instead:
 * exports-map entries as exact `{ types, import }` objects, the literal entry
 * paths inside `vite.config.ts`, the `include`/`exclude` arrays of
 * `tsconfig.build.json`, the dependency bucket of `@bc-solutions-coder/sdk`,
 * the peer list, and `createVitestProjects` appearing in this package's own
 * `vitest.config.ts`. `pnpm check:exports` (publint + attw) covers this package
 * against the BUILT artifact, which is where a broken entry actually shows up;
 * a missing Vite entry or tsconfig include fails the consuming import outright.
 * Restating all of it as source text meant restructuring the entries was a
 * test-editing exercise first.
 */
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

const packageDir = join(dirname(fileURLToPath(import.meta.url)), "..");

describe("@bc-solutions-coder/testing barrel purity", () => {
  it("keeps the browser-only entries off the config-safe '.' barrel", () => {
    const barrel = readFileSync(join(packageDir, "src/index.ts"), "utf8");

    expect(barrel).not.toContain("render-with-wallow");
    expect(barrel).not.toContain("contrast");
  });
});
