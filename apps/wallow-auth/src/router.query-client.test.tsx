import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

import { QueryClient, useQueryClient } from "@tanstack/react-query";
import { renderToString } from "react-dom/server";
import { describe, expect, it } from "vitest";

import { createRouter } from "./router";

/**
 * Single shared client (Wallow-evd5.3.4). Loaders reach the QueryClient through
 * the router context; components reach it through a `QueryClientProvider`. Those
 * must be the SAME instance per request, or data a loader prefetched never
 * reaches the component that consumes it — each side silently reads its own
 * cache.
 *
 * wallow-auth shipped two clients: one minted here in `createRouter()` for the
 * context, and a second minted by `routes/__root.tsx` for its own provider. The
 * fix (already shipped for wallow-web) is for the router to own the provider
 * through its `Wrap` render-prop, minting exactly one client that is both the
 * context client and the React-tree client.
 */
describe("createRouter (single shared query client)", () => {
  it("exposes a QueryClient on the router context", () => {
    const router = createRouter();

    expect(router.options.context?.queryClient).toBeInstanceOf(QueryClient);
  });

  it("wraps the app in a QueryClientProvider using the router-context client", () => {
    const router = createRouter();

    const Wrap = router.options.Wrap;
    if (Wrap === undefined) {
      throw new Error("router exposes no Wrap, so no QueryClientProvider spans the app");
    }

    let providedClient: QueryClient | undefined;
    function Probe(): null {
      providedClient = useQueryClient();
      return null;
    }

    renderToString(
      <Wrap>
        <Probe />
      </Wrap>,
    );

    expect(providedClient).toBe(router.options.context?.queryClient);
  });
});

/**
 * The behavioural specs above pin the two clients wallow-auth had *at the time*
 * onto one instance; this one pins the count, so a third provider minted
 * anywhere else in the app is red on arrival rather than a cache split nobody
 * notices. Test files are exempt — a spec that needs a throwaway client (e.g.
 * `routes/__root.test.tsx`'s SSR harness) is not an app-wide provider.
 */
const srcDir: string = fileURLToPath(new URL(".", import.meta.url));

function nonTestSourceFiles(): string[] {
  return readdirSync(srcDir, { recursive: true, encoding: "utf8" })
    .filter((entry: string): boolean => /\.tsx?$/u.test(entry) && !/\.test\.tsx?$/u.test(entry))
    .map((entry: string): string => join(srcDir, entry));
}

function callSitesIn(file: string): number {
  return [...readFileSync(file, "utf8").matchAll(/createQueryClient\(/gu)].length;
}

describe("wallow-auth query client call sites", () => {
  it("mints exactly one QueryClient across non-test source", () => {
    const callSites: Record<string, number> = {};
    for (const file of nonTestSourceFiles()) {
      const count: number = callSitesIn(file);
      if (count > 0) {
        callSites[file.slice(srcDir.length)] = count;
      }
    }

    expect(callSites).toEqual({ "router.tsx": 1 });
  });
});
