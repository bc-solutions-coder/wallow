import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { beforeEach, describe, expect, it } from "vitest";

import {
  allByTestId,
  expectBadge,
  expectClasses,
  expectEmptyState,
  expectListCard,
  expectListRow,
  expectTokenColorsOnly,
  parentOf,
  waitForTestId,
  within,
} from "@shared/testing/style-contract";
import { OrganizationList } from "./OrganizationList";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Restyle spec for the organizations list (Wallow-urec.4.3), following the
 * worked example in `features/apps/components/AppList.restyle.test.tsx`. It
 * covers only the surface the restyle adds; the list's behaviour (which state
 * renders when, and the `organizations-table` / `organization-item` /
 * `organizations-empty-state` / `organizations-loading` testids) stays pinned by
 * the sibling `OrganizationList.test.tsx`, which the restyle must not edit.
 *
 * Two contracts carried over from the worked example:
 *   1. The `ul`/`li` list survives — the recipe styles the `li` as a table row
 *      rather than converting the list to the Blazor original's `<table>`,
 *      because the specs pin the list testids.
 *   2. The empty state grows into the recipe's centered card but keeps its
 *      existing sentence, "No organizations yet.", VERBATIM as the card heading.
 *
 * Wallow-lrlm.5.2 moves all three surfaces onto the catalog: the card + `ul` are
 * a `ListCard`, the rows are `ListRow`s, the member count is a `Badge` and the
 * empty card is an `EmptyState`. The assertions below therefore name the catalog
 * recipes (`expectListCard`, `expectListRow`, `expectBadge`, `expectEmptyState`)
 * rather than repeating class strings this app would then have to maintain. The
 * testids are unchanged — `ListCard name="organizations"` derives
 * `organizations-table` and `ListRow name="organization"` derives
 * `organization-item`, which is exactly what shipped.
 *
 * The domain cell renders only when the org HAS a domain (`domain === null`
 * renders nothing today). That conditional is behaviour, not chrome, so the
 * restyle preserves it: `ORGS[1]` has no domain and its row must stay
 * domain-less rather than gaining the Blazor original's em-dash placeholder.
 */

const ORGS = [
  { id: "o1", name: "Acme", domain: "acme.io", memberCount: "3" },
  { id: "o2", name: "Globex", domain: null, memberCount: "1" },
];

/**
 * Render the list seeded with `orgs` (omit for the loading state) and resolve
 * the settled element named by `anchor` — the testid of the state under test.
 */
async function renderList(orgs: unknown[] | undefined, anchor: string): Promise<HTMLElement> {
  if (orgs !== undefined) {
    harness.resolveJson(orgs);
  }
  renderWithWallow(<OrganizationList />, { harness });
  return waitForTestId(anchor);
}

describe("OrganizationList (restyle)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the list as a catalog list card", async () => {
    const list = await renderList(ORGS, "organizations-table");

    expectListCard(list);
  });

  // SUPERSEDED BY Wallow-lrlm.4.1. The row used to be an inert `li` carrying the
  // recipe inline; it is now the catalog `ListRow` composed with a router `Link`,
  // so `render` substitutes an `<a>` for the `li` and the classes come from
  // `listRowRecipe()`. Two deliberate departures ride along, both decided in
  // Wallow-lrlm.3.5: `hover:bg-background/50` becomes `hover:bg-muted` (this epic
  // erases opacity-suffixed colours in favour of the real token) and the row
  // gains the catalog focus indicator, because a navigable row is focusable.
  // The row's outgoing link is pinned by `OrganizationList.navigation.test.tsx`.
  it("styles every organization row as a navigable table row", async () => {
    await renderList(ORGS, "organizations-table");

    const rows = allByTestId("organization-item");
    expect(rows).toHaveLength(ORGS.length);
    for (const row of rows) {
      expectListRow(row, "a");
    }
  });

  it("renders each row's name and a member-count chip", async () => {
    await renderList(ORGS, "organizations-table");

    const [first] = allByTestId("organization-item");

    const name = within(first, '[data-testid="organization-item-name"]');
    expect(name.textContent).toBe("Acme");
    expectClasses(name, "text-sm font-medium text-card-foreground");

    const chip = within(first, '[data-testid="organization-item-members"]');
    expect(chip.textContent?.trim()).toBe("3");
    expectBadge(chip, "neutral");
  });

  it("renders the domain cell only for orgs that have one", async () => {
    await renderList(ORGS, "organizations-table");

    const [withDomain, withoutDomain] = allByTestId("organization-item");

    const domain = within(withDomain, '[data-testid="organization-item-domain"]');
    expect(domain.textContent).toBe("acme.io");
    expectClasses(domain, "text-sm text-muted-foreground font-mono");

    expect(withoutDomain.querySelector('[data-testid="organization-item-domain"]')).toBeNull();
  });

  it("presents the empty state as a catalog empty state that keeps its copy", async () => {
    const empty = await renderList([], "organizations-empty-state");

    // The organizations page carries the office emoji in the restored design
    // (the pig is the apps page); both now sit in `EmptyState`'s icon slot.
    expectEmptyState(empty, "organizations-empty-state", {
      icon: "🏢",
      message: "No organizations yet.",
      description: "Nothing belongs here yet. Get started by creating your first organization.",
    });
  });

  it("centers the loading state without changing its wording", async () => {
    harness.pending();
    const loading = await renderList(undefined, "organizations-loading");

    expect(loading.textContent).toBe("Loading organizations…");
    expectClasses(loading, "text-center py-12");
  });

  it("styles the list with theme tokens only", async () => {
    const list = await renderList(ORGS, "organizations-table");

    expectTokenColorsOnly(parentOf(list));
  });

  it("styles the empty state with theme tokens only", async () => {
    const empty = await renderList([], "organizations-empty-state");

    expectTokenColorsOnly(empty);
  });
});
