import { existsSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Build-configuration contract for wallow-web (Wallow-ffpq.3.2).
 *
 * wallow-web's `vite.config.ts` still targets the old BFF-demo entry
 * (`src/app.ts` -> `public/app.js`) rather than the real app's client bundle,
 * and there is no `vite.ssr.config.ts` at all — so `pnpm build` neither bundles
 * the hydration entry (`src/client.tsx`, added by Wallow-ffpq.3.1) nor produces
 * an SSR bundle. This suite pins the build outputs that the SSR document shell
 * (`routes/__root.tsx`, which hardcodes `<script src="/client.js">`) and the
 * production host (Wallow-ffpq.3.3's `server.ts`) depend on, mirroring the
 * already-solved split in apps/wallow-auth (`vite.config.ts` = client bundle,
 * `vite.ssr.config.ts` = server bundle, `pnpm build` runs both).
 *
 * The config files are loaded through a runtime-computed specifier so tsc does
 * not statically resolve `vite.ssr.config.ts` before it exists — the assertion
 * that it exists is the point.
 */

interface EntryBuildConfig {
  build?: {
    outDir?: string;
    rollupOptions?: {
      input?: unknown;
      output?: { entryFileNames?: string };
    };
  };
}

interface SsrBuildConfig {
  build?: {
    ssr?: string;
    outDir?: string;
  };
}

async function loadConfigDefault<T>(relativePath: string): Promise<T> {
  const href = new URL(relativePath, import.meta.url).href;
  const loaded = (await import(href)) as { default: T };
  return loaded.default;
}

describe("wallow-web vite.config.ts (client bundle)", () => {
  it("bundles the real client entry, not the BFF-demo entry", async () => {
    const config = await loadConfigDefault<EntryBuildConfig>("../vite.config.ts");
    const input = config.build?.rollupOptions?.input;

    expect(typeof input).toBe("string");
    // The hydration entry the SSR shell loads, added by Wallow-ffpq.3.1.
    expect(String(input).endsWith("/src/client.tsx")).toBe(true);
    // The stale BFF-demo entry must no longer be the build input.
    expect(String(input).endsWith("/src/app.ts")).toBe(false);
  });

  it("emits a stable, unhashed client.js into dist/client", async () => {
    const config = await loadConfigDefault<EntryBuildConfig>("../vite.config.ts");

    // server.ts serves dist/client and __root.tsx hardcodes `/client.js`, so the
    // emitted filename must be pinned (not Vite's default hashed asset).
    expect(config.build?.rollupOptions?.output?.entryFileNames).toBe("client.js");
    expect(config.build?.outDir).toBe("dist/client");
    // Not the old esbuild-era output (public/app.js).
    expect(config.build?.outDir).not.toBe("public");
  });
});

describe("wallow-web vite.ssr.config.ts (server bundle)", () => {
  const ssrConfigPath = fileURLToPath(new URL("../vite.ssr.config.ts", import.meta.url));

  it("exists as the SSR half of the build", () => {
    expect(existsSync(ssrConfigPath)).toBe(true);
  });

  it("bundles the SSR render entry into dist/server", async () => {
    const config = await loadConfigDefault<SsrBuildConfig>("../vite.ssr.config.ts");

    // The whole render tree (router, root shell, routes) is reachable from
    // src/ssr.tsx, so bundling it proves the server-render pipeline compiles.
    expect(config.build?.ssr).toBe("src/ssr.tsx");
    expect(config.build?.outDir).toBe("dist/server");
  });
});
