import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { createRouter } from "./router";

/**
 * Router simplification contract for wallow-web (Wallow-w6s6.1.4, T1.4).
 *
 * T1.2 committed the generated `src/routeTree.gen.ts`; this task rewrites
 * `src/router.tsx` to consume it. The hand-maintained construction — a stack of
 * `Route.update({ id, path, getParentRoute })` blocks that manually reparented
 * the dashboard verticals — is replaced by importing the generated `routeTree`
 * and handing it straight to `createRouter({ routeTree, context })`, plus the
 * standard `Register` type augmentation TanStack expects.
 *
 * Two halves:
 *   1. Source assertions (fs, no execution) pin the simplified SHAPE: the
 *      generated-tree import, the absence of every `.update()` block, and the
 *      `declare module '@tanstack/react-router'` Register augmentation.
 *   2. A behavioral assertion boots `createRouter()` and checks the router
 *      registers the full generated route set — including the trailing-slash
 *      index ids (`/dashboard/organizations/`, `/dashboard/apps/`,
 *      `/dashboard/inquiries/`) that the file-based codegen emits and the old
 *      manual reparenting did not.
 */

// apps/wallow-web/src -> app root (src -> wallow-web).
const appRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..");

function readRouterSource(): string {
  return readFileSync(resolve(appRoot, "src", "router.tsx"), "utf8");
}

describe("wallow-web src/router.tsx (simplified to the generated tree)", () => {
  it("imports the routeTree from the generated ./routeTree.gen module", () => {
    const source: string = readRouterSource();
    expect(source).toMatch(
      /import\s*\{[^}]*\brouteTree\b[^}]*\}\s*from\s*["']\.\/routeTree\.gen["']/u,
    );
  });

  it("no longer hand-maintains the tree via Route.update() blocks", () => {
    const source: string = readRouterSource();
    // The whole point of codegen: the manual `.update({ id, path, getParentRoute })`
    // reparenting stack is gone. UpdatableRouteOptions legitimately rejects those
    // calls, which is why the tree must come from the generator instead.
    expect(source).not.toMatch(/\.update\(/u);
  });

  it("no longer hand-assembles the tree via rootRoute.addChildren()", () => {
    const source: string = readRouterSource();
    expect(source).not.toMatch(/\.addChildren\(/u);
  });

  it("declares the @tanstack/react-router Register augmentation", () => {
    const source: string = readRouterSource();
    expect(source).toMatch(/declare\s+module\s+["']@tanstack\/react-router["']/u);
    expect(source).toMatch(/interface\s+Register\b/u);
    expect(source).toMatch(/router:\s*ReturnType<typeof\s+createRouter>/u);
  });
});

describe("wallow-web createRouter (registers the generated route set)", () => {
  // The full id set the file-based generator emits (see src/routeTree.gen.ts
  // FileRoutesById) — index routes carry a trailing slash, which the old manual
  // `path: "organizations"` reparenting did not produce.
  const expectedRouteIds: string[] = [
    "/",
    "/bff-demo",
    "/dashboard",
    "/dashboard/settings",
    "/dashboard/apps/register",
    "/dashboard/inquiries/$inquiryId",
    "/dashboard/organizations/$orgId",
    "/dashboard/apps/",
    "/dashboard/inquiries/",
    "/dashboard/organizations/",
  ];

  it("registers every route id from the generated tree", () => {
    const router = createRouter();
    const registeredIds: string[] = Object.keys(router.routesById);
    for (const id of expectedRouteIds) {
      expect(registeredIds).toContain(id);
    }
  });
});
