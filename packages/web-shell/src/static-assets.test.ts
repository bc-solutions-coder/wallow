import { mkdir, mkdtemp, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";

import { beforeAll, describe, expect, it } from "vitest";

import { createStaticAssetReader, type StaticAsset, type StaticAssetReader } from "./static-assets";

/**
 * Consolidated from the identical apps/{wallow-auth,wallow-web} suites
 * (Wallow-0q2s.8.2). The reader is a function of a directory on disk, so the
 * suite drives a real one (a stand-in for `dist/client`, built once per run)
 * rather than mocking `node:fs` — the traversal guard in particular is only
 * meaningful against real path resolution.
 */
let rootDir: string;
let read: StaticAssetReader;

beforeAll(async (): Promise<void> => {
  rootDir = await mkdtemp(join(tmpdir(), "wallow-web-shell-static-"));
  await writeFile(join(rootDir, "client.js"), "console.log('hydrate');");
  await writeFile(join(rootDir, "client.js.map"), '{"version":3}');
  await writeFile(join(rootDir, "styles.css"), ":root{}");
  await writeFile(join(rootDir, "favicon.ico"), "");
  await writeFile(join(rootDir, "logo.piggy"), "oink");
  await mkdir(join(rootDir, "assets"));
  await writeFile(join(rootDir, "assets", "vendor.js"), "export {};");
  // A sibling of the root, i.e. the kind of file a traversal would reach for.
  await writeFile(join(rootDir, "..", "wallow-web-shell-static-secret.txt"), "secret");

  read = createStaticAssetReader(rootDir);
});

describe("createStaticAssetReader", () => {
  it("serves the hydration entry at the path the document shell hardcodes", async () => {
    // routes/__root.tsx emits <script type="module" src="/client.js">, so this
    // exact request path must resolve or the app never hydrates.
    const asset: StaticAsset | undefined = await read("/client.js");

    expect(asset?.contents.toString()).toBe("console.log('hydrate');");
    expect(asset?.contentType).toBe("text/javascript; charset=utf-8");
  });

  it("serves nested assets emitted alongside the entry", async () => {
    const asset: StaticAsset | undefined = await read("/assets/vendor.js");

    expect(asset?.contents.toString()).toBe("export {};");
  });

  it("maps the content types the client build emits", async () => {
    await expect(read("/client.js.map")).resolves.toMatchObject({
      contentType: "application/json; charset=utf-8",
    });
    await expect(read("/styles.css")).resolves.toMatchObject({
      contentType: "text/css; charset=utf-8",
    });
    await expect(read("/favicon.ico")).resolves.toMatchObject({ contentType: "image/x-icon" });
  });

  it("falls back to a generic content type for extensions it does not know", async () => {
    await expect(read("/logo.piggy")).resolves.toMatchObject({
      contentType: "application/octet-stream",
    });
  });

  it("yields nothing for a path with no file behind it", async () => {
    // Undefined — not a 404 — because the caller falls through to router SSR,
    // which owns the real 404-vs-200-vs-redirect decision for every app route.
    // Covers both apps' route shapes (wallow-auth's /login, wallow-web's
    // /dashboard/apps) — neither is a file under the asset root.
    await expect(read("/login")).resolves.toBeUndefined();
    await expect(read("/dashboard/apps")).resolves.toBeUndefined();
  });

  it("yields nothing for the root path so the router renders it", async () => {
    // `/` is a router-owned route (wallow-auth's `/` -> `/login` redirect,
    // wallow-web's public home page), not a directory read.
    await expect(read("/")).resolves.toBeUndefined();
  });

  it("yields nothing for a directory that exists", async () => {
    await expect(read("/assets")).resolves.toBeUndefined();
  });

  it("refuses to read outside the asset root", async () => {
    await expect(read("/../wallow-web-shell-static-secret.txt")).resolves.toBeUndefined();
  });
});
