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
}

function readManifest(): PackageManifest {
  return JSON.parse(readFileSync(resolve(packageRoot, "package.json"), "utf8")) as PackageManifest;
}

describe("tsup dual-entry build", () => {
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

describe("package.json exports match tsup output", () => {
  it("root subpath resolves to dist/index", () => {
    const manifest: PackageManifest = readManifest();
    expect(manifest.exports["."]).toEqual({
      types: "./dist/index.d.ts",
      import: "./dist/index.js",
    });
  });

  it("server subpath resolves to nested dist/server/index (not flat dist/server.js)", () => {
    const manifest: PackageManifest = readManifest();
    expect(manifest.exports["./server"]).toEqual({
      types: "./dist/server/index.d.ts",
      import: "./dist/server/index.js",
    });
  });
});
