/**
 * Built-client asset reads for the wallow-web standalone host (Wallow-ffpq.3.3).
 *
 * `dev-server.ts` serves the browser bundle out of Vite's middlewares, straight
 * from its module graph. The standalone host (`server.ts`) has no Vite: it runs
 * against the PRE-BUILT `dist/client` that `pnpm build` emits, so it needs its
 * own read-file-and-name-the-type step before falling through to router SSR.
 *
 * Ports apps/wallow-auth/src/lib/static-assets.ts's containment + content-type
 * logic verbatim (it is generic — no auth-specific behaviour), returning
 * `undefined` on a miss rather than a 404 so the caller can hand the path to the
 * router, which owns the real 404-vs-200-vs-redirect decision for every app
 * route.
 */
import type { Stats } from "node:fs";
import { readFile, stat } from "node:fs/promises";
import { join, normalize, sep } from "node:path";

/** A file resolved out of the asset root, ready to write to the response. */
export interface StaticAsset {
  readonly contents: Buffer;
  readonly contentType: string;
}

/**
 * Resolves a request pathname to a built asset, or `undefined` when the asset
 * root does not own that path (the caller hands the path to the router, which
 * owns the real 404-vs-200-vs-redirect decision).
 */
export type StaticAssetReader = (pathname: string) => Promise<StaticAsset | undefined>;

/**
 * Content types for what the client build can emit. `vite.config.ts` pins the
 * entry to `client.js` and asset names to `[name][extname]`, so this covers the
 * emitted set today; anything else is served as an opaque download rather than
 * guessed at.
 */
const contentTypes: Readonly<Record<string, string>> = {
  ".css": "text/css; charset=utf-8",
  ".html": "text/html; charset=utf-8",
  ".ico": "image/x-icon",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".map": "application/json; charset=utf-8",
  ".svg": "image/svg+xml",
  ".woff2": "font/woff2",
};

const DEFAULT_CONTENT_TYPE = "application/octet-stream";

/** Name the type from the file extension, defaulting rather than sniffing. */
function toContentType(resolved: string): string {
  const dot: number = resolved.lastIndexOf(".");
  const slash: number = resolved.lastIndexOf(sep);
  if (dot <= slash) {
    return DEFAULT_CONTENT_TYPE;
  }
  return contentTypes[resolved.slice(dot)] ?? DEFAULT_CONTENT_TYPE;
}

/**
 * Build a reader over `rootDir` (the host passes `dist/client`).
 *
 * The reader resolves nothing outside `rootDir`: request pathnames reaching the
 * host are already normalised by `URL` parsing, but the containment check is
 * cheap and this is the one place in the host that turns a request path into a
 * filesystem read.
 */
export function createStaticAssetReader(rootDir: string): StaticAssetReader {
  const root: string = normalize(rootDir);
  const prefix: string = root.endsWith(sep) ? root : `${root}${sep}`;

  return async (pathname: string): Promise<StaticAsset | undefined> => {
    const relative: string = pathname.replace(/^\/+/u, "");
    if (relative === "") {
      return undefined;
    }

    const resolved: string = normalize(join(root, relative));
    if (!resolved.startsWith(prefix)) {
      return undefined;
    }

    try {
      // Directories exist but are not assets — `stat` first so a request for one
      // falls through to SSR instead of surfacing a read error.
      const stats: Stats = await stat(resolved);
      if (!stats.isFile()) {
        return undefined;
      }
      return { contents: await readFile(resolved), contentType: toContentType(resolved) };
    } catch {
      return undefined;
    }
  };
}
