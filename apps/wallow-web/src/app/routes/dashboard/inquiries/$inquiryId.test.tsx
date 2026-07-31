import { describe, expect, it } from "vitest";

import { getRouter } from "@app/router";
import { Route } from "./$inquiryId";

/**
 * The inquiry-detail route's structural contract: component, prefetch loader,
 * and router registration. The rendered page reads the `inquiryId` param, so
 * render coverage lives in `InquiryDetail.test.tsx`.
 */

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
