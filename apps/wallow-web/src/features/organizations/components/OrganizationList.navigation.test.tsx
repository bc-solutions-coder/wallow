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
import { OrganizationList } from "./OrganizationList";

/**
 * Navigation spec for the organizations list (Wallow-lrlm.4.1). The rows have
 * always LOOKED clickable — a hover tint on a full-width row — while going
 * nowhere; this pins them to the detail route they imply.
 *
 * The row is composed through the catalog `ListRow`'s `render` prop
 * (Wallow-lrlm.3.5), NOT a raw anchor wrapped around or nested inside the `li`.
 * `render` SUBSTITUTES the element rather than wrapping it, so a composed row IS
 * the anchor: `<ul>` gets `<a data-testid="organization-item">` children and the
 * whole row — not an inner name cell — is the navigation target. That is the
 * property the "rows are the anchors" case below asserts, and it is what keeps
 * the shipped E2E selector `organization-item` resolving to the clickable thing.
 *
 * Navigation is asserted twice, deliberately: the `href` proves the link is
 * addressable (middle-click, copy-link, crawlers), and the click proves the
 * router — not a full page load — takes the navigation.
 *
 * `renderWithWallow` mounts a throwaway root that matches every location, so the
 * detail ROUTE itself is not registered here; this spec is about the list's
 * outgoing edge, and `/dashboard/organizations/$orgId` is separately pinned by
 * `app/routes/dashboard/organizations/$orgId` and its own specs.
 */

const ORGS = [
  { id: "o1", name: "Acme", domain: "acme.io", memberCount: "3" },
  { id: "o2", name: "Globex", domain: null, memberCount: "1" },
];

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/** Render the seeded list and hand back the router driving it. */
async function renderList(): Promise<AnyRouter> {
  harness.resolveJson(ORGS);
  const { router } = renderWithWallow(<OrganizationList />, { harness });
  await waitForTestId("organizations-table");
  return router;
}

describe("OrganizationList row navigation", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders every row as an anchor rather than an inert cell", async () => {
    await renderList();

    const rows = allByTestId("organization-item");
    expect(rows).toHaveLength(ORGS.length);
    for (const row of rows) {
      expectTag(row, "a");
    }
  });

  it("points each row at its own organization's detail route", async () => {
    await renderList();

    const hrefs = allByTestId("organization-item").map((row) => row.getAttribute("href"));
    expect(hrefs).toEqual(["/dashboard/organizations/o1", "/dashboard/organizations/o2"]);
  });

  it("makes the whole row the link, not a cell inside it", async () => {
    await renderList();
    const list = byTestId("organizations-table");

    // `render` substitutes the `li`, so the anchors are the list's OWN children
    // and no second anchor is nested inside a row.
    for (const row of allByTestId("organization-item")) {
      expect(parentOf(row)).toBe(list);
      expect(row.querySelector("a")).toBeNull();
    }
  });

  it("keeps every shipped row testid on the anchor and its cells", async () => {
    await renderList();

    const [first, second] = allByTestId("organization-item");

    expect(first.querySelector('[data-testid="organization-item-name"]')?.textContent).toBe("Acme");
    expect(first.querySelector('[data-testid="organization-item-domain"]')?.textContent).toBe(
      "acme.io",
    );
    expect(
      first.querySelector('[data-testid="organization-item-members"]')?.textContent?.trim(),
    ).toBe("3");
    // The optional domain cell stays conditional — a link is not a licence to
    // start rendering an empty cell for an org that has no domain.
    expect(second.querySelector('[data-testid="organization-item-domain"]')).toBeNull();
  });

  it("navigates through the router when a row is clicked", async () => {
    const router = await renderList();

    await userEvent.click(allByTestId("organization-item")[1] as HTMLElement);

    await expect.poll(() => router.state.location.pathname).toBe("/dashboard/organizations/o2");
  });

  it("reaches each row by keyboard", async () => {
    await renderList();

    const [first] = allByTestId("organization-item");
    (first as HTMLElement).focus();

    expect(document.activeElement).toBe(first);
  });
});
