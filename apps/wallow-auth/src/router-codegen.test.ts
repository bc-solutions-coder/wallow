import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Router-simplification contract for wallow-auth (Wallow-w6s6.1.3, T1.3).
 *
 * Once T1.2 committed `src/routeTree.gen.ts`, the hand-maintained router
 * construction in `router.tsx` — a chain of `Route.update({ id, path,
 * getParentRoute })` blocks that patched the missing generated tree by hand,
 * plus a manual `rootRoute.addChildren([...])` — is dead weight. This task
 * replaces it with `createRouter({ routeTree, context })` importing the
 * generated tree, plus the standard TanStack `declare module` Register
 * augmentation.
 *
 * These assertions read `router.tsx` as source text (never a static import) so
 * they neither boot Vite nor force tsc to resolve the route `.tsx` files (which
 * both apps' tsconfigs still exclude — that is F2's job). The behavioural gate
 * for this task is `e2e/routes.spec.ts`; these tests pin the source-shape change
 * the simplification is defined by.
 */

// apps/wallow-auth/src -> the router entry it simplifies.
const srcDir: string = dirname(fileURLToPath(import.meta.url));

function readRouterSource(): string {
  return readFileSync(resolve(srcDir, "router.tsx"), "utf8");
}

describe("wallow-auth router.tsx (generated-tree simplification, T1.3)", () => {
  it("imports the generated route tree from ./routeTree.gen", () => {
    const source: string = readRouterSource();
    expect(source).toMatch(
      /import\s*\{[^}]*\brouteTree\b[^}]*\}\s*from\s*["']\.\/routeTree\.gen["']/u,
    );
  });

  it("drops the per-route imports the manual assembly needed", () => {
    // The generated tree imports every route file itself; router.tsx no longer
    // pulls individual routes (or __root) in from ./routes/*.
    const source: string = readRouterSource();
    expect(source).not.toMatch(/from\s*["']\.\/routes\//u);
  });

  it("deletes the Route.update() workaround blocks", () => {
    // The .update({ id, path, getParentRoute }) calls existed only to patch the
    // absent generated tree; UpdatableRouteOptions rejects them and codegen makes
    // them unnecessary.
    const source: string = readRouterSource();
    expect(source).not.toMatch(/\.update\(/u);
  });

  it("no longer hand-assembles the tree via rootRoute.addChildren", () => {
    const source: string = readRouterSource();
    expect(source).not.toMatch(/\baddChildren\(/u);
  });

  it("registers the router type via the declare module augmentation", () => {
    // Standard TanStack Register augmentation so route APIs are typed against
    // this app's router.
    const source: string = readRouterSource();
    expect(source).toMatch(/declare\s+module\s+["']@tanstack\/react-router["']/u);
    expect(source).toMatch(/interface\s+Register\b/u);
    expect(source).toMatch(/router:\s*ReturnType<typeof\s+createRouter>/u);
  });

  it("still builds the router from the imported tree and queryClient context", () => {
    // The construction call is the invariant the simplification must preserve:
    // createTanStackRouter with the (now imported) routeTree and queryClient
    // context.
    const source: string = readRouterSource();
    expect(source).toMatch(/createTanStackRouter\(/u);
    expect(source).toMatch(/context:\s*\{\s*queryClient\s*\}/u);
  });
});
