import { readFileSync, statSync } from "node:fs";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { brandAssetsDir } from "./assets";
import { appIconUrl, forkBranding } from "./branding";

/**
 * The other half of the icon contract. `asset-urls.ts` decides what URL the
 * markup asks for; this decides that a file actually answers it, and that the
 * file lives HERE rather than being copied into each app — which is the whole
 * point of the package owning the asset.
 */
const packageRoot: string = fileURLToPath(new URL("../", import.meta.url));

/** The canonical icon at the repo root — the highest-resolution original. */
const canonicalIcon: string = fileURLToPath(
  new URL("../../../assets/piggy-icon.svg", import.meta.url),
);

interface Manifest {
  readonly files?: readonly string[];
  readonly exports?: Readonly<Record<string, unknown>>;
}

const manifest: Manifest = JSON.parse(
  readFileSync(join(packageRoot, "package.json"), "utf8"),
) as Manifest;

function isDirectory(path: string): boolean {
  try {
    return statSync(path).isDirectory();
  } catch {
    return false;
  }
}

describe("brandAssetsDir", () => {
  it("points at a real directory of assets", () => {
    expect(brandAssetsDir).not.toBe("");
    expect(isDirectory(brandAssetsDir)).toBe(true);
  });

  it("sits at the package root, so the built entry and the source agree on it", () => {
    // src/assets.ts and dist/assets.js are both one level under the package
    // root, so a module-relative "../assets" resolves identically in both
    // graphs. A cwd-relative path would not: consumers resolve this from their
    // own directory.
    expect(resolve(brandAssetsDir)).toBe(resolve(packageRoot, "assets"));
  });

  it("holds the icon api/branding.json names", () => {
    expect(statSync(join(brandAssetsDir, forkBranding.appIcon)).isFile()).toBe(true);
  });

  it("answers the URL the markup asks for", () => {
    // The two halves of the package have to line up: a consuming app copies this
    // directory to its served root, so every root-relative asset URL must name a
    // file in it.
    const servedPath: string = join(brandAssetsDir, appIconUrl.replace(/^\//u, ""));

    expect(statSync(servedPath).isFile()).toBe(true);
  });

  it("ships the canonical icon rather than a re-drawn copy", () => {
    const shipped: Buffer = readFileSync(join(brandAssetsDir, forkBranding.appIcon));

    expect(shipped.equals(readFileSync(canonicalIcon))).toBe(true);
  });
});

describe("the package manifest", () => {
  it("publishes the assets directory", () => {
    // Without this the assets resolve locally (where the whole repo is on disk)
    // and vanish for anyone consuming the published package.
    expect(manifest.files).toContain("assets");
  });

  it("exports the assets subpath separately from the main entry", () => {
    // Separate because this module reads node:url. The main entry is bundled
    // into consumers' browser builds and must not drag node: imports in with it.
    //
    // Named unhashed (Wallow-do5e): nothing here content-hashes without a build
    // manifest to read the hashed name back out of.
    expect(manifest.exports?.["./assets"]).toEqual({
      types: "./dist/assets.d.ts",
      import: "./dist/assets.js",
    });
  });
});
