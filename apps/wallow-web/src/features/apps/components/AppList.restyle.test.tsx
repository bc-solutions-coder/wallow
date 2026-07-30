import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { beforeEach, describe, expect, it } from "vitest";

import {
  allByTestId,
  expectClasses,
  expectTag,
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

  it("wraps the list in the card surface", async () => {
    const list = await renderList(APPS, "apps-table");

    const surface = parentOf(list);
    expectTag(surface, "div");
    expectClasses(surface, "bg-card rounded-lg shadow-sm border border-border overflow-hidden");
  });

  it("keeps the list a ul and divides its rows", async () => {
    const list = await renderList(APPS, "apps-table");

    expectTag(list, "ul");
    expectClasses(list, "divide-y divide-border");
  });

  it("styles every app row as a table row", async () => {
    await renderList(APPS, "apps-table");

    const rows = allByTestId("app-item");
    expect(rows).toHaveLength(APPS.length);
    for (const row of rows) {
      expectTag(row, "li");
      expectClasses(row, "flex items-center justify-between px-6 py-4 hover:bg-background/50");
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
    expectClasses(
      chip,
      "inline-block bg-accent text-accent-foreground text-xs font-medium px-2.5 py-0.5 rounded-full",
    );
  });

  it("presents the empty state as a centered card that keeps its sentence", async () => {
    const empty = await renderList([], "apps-empty-state");

    expectClasses(empty, "bg-card rounded-lg shadow-sm border border-border p-12 text-center");
    expect(empty.textContent).toContain("🐷");

    const heading = within(empty, "h2");
    expect(heading.textContent).toBe("No apps yet.");
    expectClasses(heading, "text-xl font-semibold text-foreground mb-2");
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
