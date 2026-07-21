import { beforeEach, describe, expect, it, vi } from "vitest";

import { createRouter } from "../../../router";
import { Route } from "./$orgId";

/**
 * Route spec for the org-detail route (Wallow-8w1h.4.4). Covers the same two
 * contracts as the list route: (1) the route exposes a component + a prefetch
 * `loader`; (2) `src/router.tsx` registers it at `/dashboard/organizations/
 * $orgId` (bound manually — no dashboard layout route exists yet).
 *
 * The rendered page reads the `orgId` route param, so full render coverage
 * lives in OrganizationDetail.test.tsx; here we assert the route's structural
 * contract only.
 */

// The detail page mounts queries via OrganizationDetail; mock the facade so any
// module-load-time wiring stays inert.
const mocks = vi.hoisted(() => ({
  get: vi.fn(),
  members: vi.fn(),
  addMember: vi.fn(),
  removeMember: vi.fn(),
  archive: vi.fn(),
  reactivate: vi.fn(),
}));

vi.mock("../../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    organizations: {
      list: vi.fn(),
      get: mocks.get,
      create: vi.fn(),
      members: mocks.members,
      addMember: mocks.addMember,
      removeMember: mocks.removeMember,
      archive: mocks.archive,
      reactivate: mocks.reactivate,
    },
  }),
}));

describe("routes/dashboard/organizations/$orgId (route)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("prefetches org detail + members via a loader", () => {
    expect(Route.options.loader).toBeDefined();
  });
});

describe("routes/dashboard/organizations/$orgId (router registration)", () => {
  it("registers /dashboard/organizations/$orgId in the router tree", () => {
    const router = createRouter();
    const paths = Object.keys((router as { routesByPath: Record<string, unknown> }).routesByPath);
    expect(paths).toContain("/dashboard/organizations/$orgId");
  });
});
