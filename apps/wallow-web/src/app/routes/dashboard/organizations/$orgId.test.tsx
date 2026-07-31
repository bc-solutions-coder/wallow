import { describe, expect, it } from "vitest";

import { getRouter } from "@app/router";
import { Route } from "./$orgId";

/**
 * The org-detail route's structural contract: component, prefetch loader, and
 * router registration. The rendered page reads the `orgId` param, so render
 * coverage lives in `OrganizationDetail.test.tsx`.
 */

describe("routes/dashboard/organizations/$orgId (route)", () => {
  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("prefetches org detail + members via a loader", () => {
    expect(Route.options.loader).toBeDefined();
  });
});

describe("routes/dashboard/organizations/$orgId (router registration)", () => {
  it("registers /dashboard/organizations/$orgId in the router tree", () => {
    const router = getRouter();
    const paths = Object.keys(router.routesByPath);
    expect(paths).toContain("/dashboard/organizations/$orgId");
  });
});
