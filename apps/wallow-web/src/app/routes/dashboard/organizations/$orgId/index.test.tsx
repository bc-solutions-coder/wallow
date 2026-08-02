import { describe, expect, it } from "vitest";

import { getRouter } from "@app/router";
import { Route } from "./index";

/**
 * The org-detail route's structural contract: component, prefetch loader, and
 * its registration in the directory form. `$orgId/` holds no `route.tsx`, so it
 * nests the path and contributes no layout — the index route's id carries a
 * trailing slash and no bare `$orgId` route exists to blank future siblings.
 * The rendered page reads the `orgId` param, so render coverage lives in
 * `OrganizationDetail.test.tsx`.
 */

describe("routes/dashboard/organizations/$orgId/ (route)", () => {
  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("prefetches org detail + members via a loader", () => {
    expect(Route.options.loader).toBeDefined();
  });
});

describe("routes/dashboard/organizations/$orgId/ (router registration)", () => {
  it("registers the index route under the $orgId directory", () => {
    const router = getRouter();
    const ids = Object.keys(router.routesById);
    expect(ids).toContain("/dashboard/organizations/$orgId/");
  });

  it("leaves /dashboard/organizations/$orgId addressable by existing links", () => {
    const router = getRouter();
    const paths = Object.keys(router.routesByPath);
    expect(paths).toContain("/dashboard/organizations/$orgId");
  });

  it("contributes no $orgId layout route", () => {
    const router = getRouter();
    const ids = Object.keys(router.routesById);
    expect(ids, "a $orgId layout would render an Outlet in place of every child").not.toContain(
      "/dashboard/organizations/$orgId",
    );
  });
});
