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
import { AppList } from "./AppList";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Restyle spec for the apps list (Wallow-urec.4.1) — the WORKED EXAMPLE for
 * Phase 4. It covers only the surface the restyle adds; the list's behaviour
 * (which state renders when, and the `apps-table` / `app-item` /
 * `apps-empty-state` / `apps-loading` testids) stays pinned by the sibling
 * `AppList.test.tsx`, which the restyle must not edit.
 *
 * Two contracts the other Phase 4 restyles must copy:
 *   1. The `ul`/`li` list survives — the recipe styles the `li` as a table row
 *      rather than converting the list to a `<table>`, because the specs pin the
 *      list testids.
 *   2. The empty state grows into the recipe's centered card (pig emoji, heading,
 *      body copy) but keeps its existing sentence, "No apps yet.", VERBATIM as
 *      the card's heading. A restyle adds chrome; it never rewrites copy.
 *
 * Wallow-lrlm.5.2 moves all three surfaces onto the catalog: the card + `ul` are
 * a `ListCard`, the rows are `ListRow`s, the type chip is a `Badge` and the empty
 * card is an `EmptyState`. Unlike the other lists this one had NOT been migrated
 * at all, so both halves change here. The testids are unchanged — `ListCard
 * name="apps"` derives `apps-table` and `ListRow name="app"` derives `app-item`.
 *
 * The rows stay plain `li`s rather than becoming links: there is no app-detail
 * route to navigate to. `ListRow` without `render` resolves to its default `li`.
 */

const APPS = [
  {
    clientId: "c1",
    displayName: "Acme App",
    clientType: "public",
    redirectUris: [],
    createdAt: null,
  },
  {
    clientId: "c2",
    displayName: "Globex App",
    clientType: "confidential",
    redirectUris: ["https://globex.io/cb"],
    createdAt: "2026-07-01T00:00:00Z",
  },
];

/**
 * Render the list seeded with `apps` (omit for the loading state) and resolve
 * the settled element named by `anchor` — the testid of the state under test.
 */
async function renderList(apps: unknown[] | undefined, anchor: string): Promise<HTMLElement> {
  if (apps !== undefined) {
    harness.resolveJson(apps);
  }
  renderWithWallow(<AppList />, { harness });
  return waitForTestId(anchor);
}

describe("AppList (restyle)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the list as a catalog list card", async () => {
    const list = await renderList(APPS, "apps-table");

    expectListCard(list);
  });

  it("styles every app row as a table row", async () => {
    await renderList(APPS, "apps-table");

    const rows = allByTestId("app-item");
    expect(rows).toHaveLength(APPS.length);
    for (const row of rows) {
      expectListRow(row, "li");
    }
  });

  it("renders each row's name and a type chip", async () => {
    await renderList(APPS, "apps-table");

    const [first] = allByTestId("app-item");

    const name = within(first, '[data-testid="app-item-name"]');
    expect(name.textContent).toBe("Acme App");
    expectClasses(name, "text-sm font-medium text-card-foreground");

    const chip = within(first, '[data-testid="app-item-type"]');
    expect(chip.textContent?.trim()).toBe("public");
    expectBadge(chip, "neutral");
  });

  it("presents the empty state as a catalog empty state that keeps its copy", async () => {
    const empty = await renderList([], "apps-empty-state");

    expectEmptyState(empty, "apps-empty-state", {
      icon: "🐷",
      message: "No apps yet.",
      description:
        "Nothing has been registered here. Get started by creating your first application.",
    });
  });

  it("centers the loading state without changing its wording", async () => {
    harness.pending();
    const loading = await renderList(undefined, "apps-loading");

    expect(loading.textContent).toBe("Loading apps…");
    expectClasses(loading, "text-center py-12");
  });

  it("styles the list with theme tokens only", async () => {
    const list = await renderList(APPS, "apps-table");

    expectTokenColorsOnly(parentOf(list));
  });

  it("styles the empty state with theme tokens only", async () => {
    const empty = await renderList([], "apps-empty-state");

    expectTokenColorsOnly(empty);
  });
});
