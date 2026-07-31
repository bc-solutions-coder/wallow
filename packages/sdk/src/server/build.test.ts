import { execFileSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { beforeAll, describe, expect, it } from "vitest";

const packageRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const distDir: string = resolve(packageRoot, "dist");

interface SubpathExport {
  types: string;
  import: string;
}

interface PackageManifest {
  exports: Record<string, SubpathExport>;
  sideEffects?: boolean;
}

function readManifest(): PackageManifest {
  return JSON.parse(readFileSync(resolve(packageRoot, "package.json"), "utf8")) as PackageManifest;
}

describe("the multi-entry declaration build", () => {
  beforeAll(() => {
    execFileSync("npm", ["run", "build"], {
      cwd: packageRoot,
      stdio: "inherit",
    });
  }, 180_000);

  it("emits the browser entrypoint js + declaration", () => {
    expect(existsSync(resolve(distDir, "index.js"))).toBe(true);
    expect(existsSync(resolve(distDir, "index.d.ts"))).toBe(true);
  });

  it("emits the server subpath entrypoint js + declaration (nested, not flat)", () => {
    expect(existsSync(resolve(distDir, "server", "index.js"))).toBe(true);
    expect(existsSync(resolve(distDir, "server", "index.d.ts"))).toBe(true);
    // The old flat layout must NOT be produced.
    expect(existsSync(resolve(distDir, "server.js"))).toBe(false);
    expect(existsSync(resolve(distDir, "server.d.ts"))).toBe(false);
  });
});

/*
 * Two specs used to sit here asserting `exports["."]` and `exports["./server"]`
 * equalled an exact `./dist/...` pair. Both are gone, and neither invariant went
 * with them. The nested-not-flat emit layout is asserted directly above, against
 * the files the build actually produces; whether a consumer's resolver reaches
 * them is publint + @arethetypeswrong/cli's job (`pnpm check:exports`), against
 * the packed tarball. What the assertions additionally pinned — that the map in
 * the working tree names `dist/` — is no longer true and should not be: in-repo
 * every entry points at `src/` so apps resolve from source with no prebuilt
 * dist, and the `dist/` map is applied at publish time from
 * `publishConfig.exports`.
 */

/**
 * The passthrough preset ships as its OWN subpath (Wallow-pu6a.3.7) so an app
 * that only reverse-proxies never pulls `openid-client` and the BFF handler
 * graph into its server bundle — which it would if the preset were re-exported
 * from `./server`. `sideEffects: false` is what lets a bundler drop the parts of
 * this package an app does not import at all.
 */
describe("passthrough subpath packaging", () => {
  it("declares ./server/passthrough as a subpath of its own, not an alias of ./server", () => {
    const manifest: PackageManifest = readManifest();

    // SEPARATION is the invariant — a subpath that exists and resolves
    // somewhere other than the BFF entry. Where it resolves is not asserted
    // here: in-repo it points at `src/` and the published `dist/` map comes from
    // `publishConfig.exports`, so an exact-path assertion would pin the wrong
    // half of the contract. `pnpm check:exports` covers resolution.
    expect(manifest.exports["./server/passthrough"]).toBeDefined();
    expect(manifest.exports["./server/passthrough"]).not.toEqual(manifest.exports["./server"]);
  });

  it("declares sideEffects: false so unused entries are tree-shakeable", () => {
    const manifest: PackageManifest = readManifest();
    expect(manifest.sideEffects).toBe(false);
  });

  it("emits the passthrough entry js + declaration", () => {
    expect(existsSync(resolve(distDir, "server", "passthrough.js"))).toBe(true);
    expect(existsSync(resolve(distDir, "server", "passthrough.d.ts"))).toBe(true);
  });
});
