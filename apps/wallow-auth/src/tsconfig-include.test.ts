import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Spec (Wallow-w6s6.2.1): every React component in this app must be typechecked.
 *
 * The app's tsconfig `include` list historically carried only `src/**\/*.ts`,
 * which silently excludes every `.tsx` component and route from `tsc --noEmit`
 * (`pnpm typecheck`). Nothing catches type errors in the UI until they blow up
 * at runtime. This pins the include glob so the coverage cannot be dropped again
 * without a red test — the exact regression this task exists to close.
 *
 * The tsconfig is JSONC (leading `//` doc-comments), so strip comments before
 * `JSON.parse`. These files use only line comments and no `//` inside any string
 * value, so a line-comment strip is sufficient and safe.
 */
const TSX_INCLUDE_GLOB = "src/**/*.tsx";

interface TsConfigShape {
  readonly include?: readonly string[];
}

function readTsConfigInclude(relativeToTestDir: string): readonly string[] {
  const path: string = fileURLToPath(new URL(relativeToTestDir, import.meta.url));
  const raw: string = readFileSync(path, "utf8");
  const withoutLineComments: string = raw.replaceAll(/(^|\s)\/\/.*$/gm, "$1");
  const parsed: TsConfigShape = JSON.parse(withoutLineComments) as TsConfigShape;
  return parsed.include ?? [];
}

describe("wallow-auth tsconfig typecheck coverage", () => {
  it("includes src/**/*.tsx so every component and route is typechecked", () => {
    // Path is relative to this test file (apps/wallow-auth/src/), so `../` reaches
    // the app root tsconfig.json.
    expect(readTsConfigInclude("../tsconfig.json")).toContain(TSX_INCLUDE_GLOB);
  });
});
