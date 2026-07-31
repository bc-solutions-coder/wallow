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
import { InquiryList } from "./InquiryList";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Restyle spec for the inquiries list (Wallow-urec.4.2), copying the `.4.1`
 * `AppList` worked example. It covers only the surface the restyle adds; which
 * state renders when, and the `inquiries-table` / `inquiry-item` /
 * `inquiry-item-status` / `inquiries-empty-state` / `inquiries-loading` testids,
 * stay pinned by the sibling `InquiryList.test.tsx`, which the restyle must not
 * edit.
 *
 * One shape difference from `AppList`: an inquiry row carries an OPTIONAL
 * company line under the name, so the row's left-hand side is a stacked identity
 * block (`flex flex-col`) rather than a single span. Extracting that block into
 * its own component is also what keeps the row inside oxlint's
 * `react/jsx-max-depth` budget.
 *
 * Wallow-lrlm.5.2 moves the remaining surfaces onto the catalog: the card + `ul`
 * are a `ListCard`, the status chip is a `Badge` and the empty card is an
 * `EmptyState` (the rows were already `ListRow`s). `ListCard name="inquiries"`
 * derives the shipped `inquiries-table` id, so no testid moves.
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

/**
 * Render the list seeded with `inquiries` (omit for the loading state) and
 * resolve the settled element named by `anchor` — the testid of the state under
 * test. `render()` returns before React commits, so every test must await this.
 */
async function renderList(inquiries: unknown[] | undefined, anchor: string): Promise<HTMLElement> {
  if (inquiries !== undefined) {
    harness.resolveJson(inquiries);
  }
  renderWithWallow(<InquiryList />, { harness });
  return waitForTestId(anchor);
}

describe("InquiryList (restyle)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the list as a catalog list card", async () => {
    const list = await renderList(INQUIRIES, "inquiries-table");

    expectListCard(list);
  });

  // SUPERSEDED BY Wallow-lrlm.4.1 — see the twin note in
  // `OrganizationList.restyle.test.tsx`. The row is the catalog `ListRow`
  // composed with a router `Link`, so it renders as an `<a>` carrying
  // `listRowRecipe()`; the link itself is pinned by
  // `InquiryList.navigation.test.tsx`.
  it("styles every inquiry row as a navigable table row", async () => {
    await renderList(INQUIRIES, "inquiries-table");

    const rows = allByTestId("inquiry-item");
    expect(rows).toHaveLength(INQUIRIES.length);
    for (const row of rows) {
      expectListRow(row, "a");
    }
  });

  it("stacks each row's name over its company", async () => {
    await renderList(INQUIRIES, "inquiries-table");

    const [first, second] = allByTestId("inquiry-item");

    const name = within(first, '[data-testid="inquiry-item-name"]');
    expect(name.textContent).toBe("Ada Lovelace");
    expectClasses(name, "text-sm font-medium text-card-foreground");
    expectClasses(parentOf(name), "flex flex-col");

    // Ada has no company, so the optional line stays absent rather than empty.
    expect(first.querySelector('[data-testid="inquiry-item-company"]')).toBeNull();

    const company = within(second, '[data-testid="inquiry-item-company"]');
    expect(company.textContent).toBe("Navy");
    expectClasses(company, "text-xs text-muted-foreground");
  });

  it("styles the status as a chip without changing its text", async () => {
    await renderList(INQUIRIES, "inquiries-table");

    const [first] = allByTestId("inquiry-item");

    const chip = within(first, '[data-testid="inquiry-item-status"]');
    expect(chip.textContent).toBe("New");
    expectBadge(chip, "neutral");
  });

  it("presents the empty state as a catalog empty state that keeps its copy", async () => {
    const empty = await renderList([], "inquiries-empty-state");

    expectEmptyState(empty, "inquiries-empty-state", {
      icon: "🐷",
      message: "No inquiries yet.",
      description: "Nothing has arrived here. New inquiries show up as soon as one is submitted.",
    });
  });

  it("centers the loading state without changing its wording", async () => {
    harness.pending();
    const loading = await renderList(undefined, "inquiries-loading");

    expect(loading.textContent).toBe("Loading inquiries…");
    expectClasses(loading, "text-center py-12");
  });

  it("styles the list with theme tokens only", async () => {
    const list = await renderList(INQUIRIES, "inquiries-table");

    expectTokenColorsOnly(parentOf(list));
  });

  it("styles the empty state with theme tokens only", async () => {
    const empty = await renderList([], "inquiries-empty-state");

    expectTokenColorsOnly(empty);
  });
});
