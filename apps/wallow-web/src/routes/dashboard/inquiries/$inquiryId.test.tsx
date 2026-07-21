import { beforeEach, describe, expect, it, vi } from "vitest";

import { createRouter } from "../../../router";
import { Route } from "./$inquiryId";

/**
 * Route spec for the inquiry-detail route (Wallow-8w1h.7.4). Mirrors the
 * organization-detail route's structural contract: (1) the route exposes a
 * component + a prefetch `loader`; (2) `src/router.tsx` registers it at
 * `/dashboard/inquiries/$inquiryId` (bound manually — no dashboard layout route
 * exists yet).
 *
 * The rendered page reads the `inquiryId` route param, so full render coverage
 * lives in InquiryDetail.test.tsx; here we assert the route's structural
 * contract only.
 */

// The detail page mounts queries via InquiryDetail; mock the facade so any
// module-load-time wiring stays inert.
const mocks = vi.hoisted(() => ({
  get: vi.fn(),
  comments: vi.fn(),
  addComment: vi.fn(),
  setStatus: vi.fn(),
}));

vi.mock("../../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    inquiries: {
      list: vi.fn(),
      create: vi.fn(),
      get: mocks.get,
      comments: mocks.comments,
      addComment: mocks.addComment,
      setStatus: mocks.setStatus,
    },
  }),
}));

describe("routes/dashboard/inquiries/$inquiryId (route)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("prefetches inquiry detail + comments via a loader", () => {
    expect(Route.options.loader).toBeDefined();
  });
});

describe("routes/dashboard/inquiries/$inquiryId (router registration)", () => {
  it("registers /dashboard/inquiries/$inquiryId in the router tree", () => {
    const router = createRouter();
    const paths = Object.keys((router as { routesByPath: Record<string, unknown> }).routesByPath);
    expect(paths).toContain("/dashboard/inquiries/$inquiryId");
  });
});
