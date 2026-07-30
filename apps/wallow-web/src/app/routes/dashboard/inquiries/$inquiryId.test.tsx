import { describe, expect, it } from "vitest";

import { getRouter } from "@app/router";
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

// Nothing to mock: importing the route no longer configures anything. The
// module-global client this file used to neutralise — `getWallowSdk()` out of
// `src/lib/wallow-sdk` — is deleted (Wallow-pu6a.5.5), and the detail page now
// takes its client from the router context at render time.

describe("routes/dashboard/inquiries/$inquiryId (route)", () => {
  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("prefetches inquiry detail + comments via a loader", () => {
    expect(Route.options.loader).toBeDefined();
  });
});

describe("routes/dashboard/inquiries/$inquiryId (router registration)", () => {
  it("registers /dashboard/inquiries/$inquiryId in the router tree", () => {
    const router = getRouter();
    const paths = Object.keys(router.routesByPath);
    expect(paths).toContain("/dashboard/inquiries/$inquiryId");
  });
});
