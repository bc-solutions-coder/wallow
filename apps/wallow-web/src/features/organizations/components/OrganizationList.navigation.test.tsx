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
} from "@bc-solutions-coder/testing/locators";
import { OrganizationList } from "./OrganizationList";

/**
 * The organizations list's outgoing navigation: every row IS the link.
 *
 * `ListRow`'s `render` prop SUBSTITUTES the element rather than wrapping it, so
 * the `<ul>` gets `<a>` children and the whole row, not an inner cell, is the
 * target — which keeps the E2E selector `organization-item` on the clickable
 * thing. The `href` proves it addressable; the click proves the router takes it.
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
    // The domain cell stays conditional: an org without a domain renders none.
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
