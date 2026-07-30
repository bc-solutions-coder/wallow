import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import type { AnyRouter } from "@tanstack/react-router";
import { userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import {
  allByTestId,
  byTestId,
  expectTag,
  parentOf,
  waitForTestId,
} from "@shared/testing/style-contract";
import { InquiryList } from "./InquiryList";

/**
 * Navigation spec for the inquiries list (Wallow-lrlm.4.1), the twin of
 * `OrganizationList.navigation.test.tsx` — see that file for why the row is the
 * anchor rather than something wrapped around one.
 *
 * SCOPE NOTE. The bead names "organizations, apps", but its acceptance criterion
 * is written generally: every list page whose items have a corresponding detail
 * route. `/dashboard/inquiries/$inquiryId` exists and `inquiry-item` rows are
 * the same dead-looking-clickable shape, so inquiries is covered here.
 * `/dashboard/apps/$id` does NOT exist in the route tree at all, so `app-item`
 * rows are deliberately left inert — wiring them would mean inventing a route,
 * which this bug-fix-only feature does not do.
 */

const INQUIRIES = [
  {
    id: "i1",
    name: "Ada Lovelace",
    email: "ada@example.com",
    company: null,
    projectType: "web-app",
    status: "New",
    createdAt: "2026-07-15T00:00:00Z",
  },
  {
    id: "i2",
    name: "Grace Hopper",
    email: "grace@example.com",
    company: "Navy",
    projectType: "consulting",
    status: "Contacted",
    createdAt: "2026-07-14T00:00:00Z",
  },
];

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/** Render the seeded list and hand back the router driving it. */
async function renderList(): Promise<AnyRouter> {
  harness.resolveJson(INQUIRIES);
  const { router } = renderWithWallow(<InquiryList />, { harness });
  await waitForTestId("inquiries-table");
  return router;
}

describe("InquiryList row navigation", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders every row as an anchor rather than an inert cell", async () => {
    await renderList();

    const rows = allByTestId("inquiry-item");
    expect(rows).toHaveLength(INQUIRIES.length);
    for (const row of rows) {
      expectTag(row, "a");
    }
  });

  it("points each row at its own inquiry's detail route", async () => {
    await renderList();

    const hrefs = allByTestId("inquiry-item").map((row) => row.getAttribute("href"));
    expect(hrefs).toEqual(["/dashboard/inquiries/i1", "/dashboard/inquiries/i2"]);
  });

  it("makes the whole row the link, not a cell inside it", async () => {
    await renderList();
    const list = byTestId("inquiries-table");

    for (const row of allByTestId("inquiry-item")) {
      expect(parentOf(row)).toBe(list);
      expect(row.querySelector("a")).toBeNull();
    }
  });

  it("keeps every shipped row testid on the anchor and its cells", async () => {
    await renderList();

    const [first, second] = allByTestId("inquiry-item");

    expect(first.querySelector('[data-testid="inquiry-item-name"]')?.textContent).toBe(
      "Ada Lovelace",
    );
    expect(first.querySelector('[data-testid="inquiry-item-status"]')?.textContent).toBe("New");
    // Ada has no company, so the optional line stays absent rather than empty.
    expect(first.querySelector('[data-testid="inquiry-item-company"]')).toBeNull();
    expect(second.querySelector('[data-testid="inquiry-item-company"]')?.textContent).toBe("Navy");
  });

  it("navigates through the router when a row is clicked", async () => {
    const router = await renderList();

    await userEvent.click(allByTestId("inquiry-item")[1] as HTMLElement);

    await expect.poll(() => router.state.location.pathname).toBe("/dashboard/inquiries/i2");
  });

  it("reaches each row by keyboard", async () => {
    await renderList();

    const [first] = allByTestId("inquiry-item");
    (first as HTMLElement).focus();

    expect(document.activeElement).toBe(first);
  });
});
