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
} from "../../../test/style-contract";
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

  it("wraps the list in the card surface", async () => {
    const list = await renderList(INQUIRIES, "inquiries-table");

    const surface = parentOf(list);
    expectTag(surface, "div");
    expectClasses(surface, "bg-card rounded-lg shadow-sm border border-border overflow-hidden");
  });

  it("keeps the list a ul and divides its rows", async () => {
    const list = await renderList(INQUIRIES, "inquiries-table");

    expectTag(list, "ul");
    expectClasses(list, "divide-y divide-border");
  });

  it("styles every inquiry row as a table row", async () => {
    await renderList(INQUIRIES, "inquiries-table");

    const rows = allByTestId("inquiry-item");
    expect(rows).toHaveLength(INQUIRIES.length);
    for (const row of rows) {
      expectTag(row, "li");
      expectClasses(row, "flex items-center justify-between px-6 py-4 hover:bg-background/50");
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
    expectClasses(company, "text-xs text-foreground/60");
  });

  it("styles the status as a chip without changing its text", async () => {
    await renderList(INQUIRIES, "inquiries-table");

    const [first] = allByTestId("inquiry-item");

    const chip = within(first, '[data-testid="inquiry-item-status"]');
    expect(chip.textContent).toBe("New");
    expectClasses(
      chip,
      "inline-block bg-accent text-accent-foreground text-xs font-medium px-2.5 py-0.5 rounded-full",
    );
  });

  it("presents the empty state as a centered card that keeps its sentence", async () => {
    const empty = await renderList([], "inquiries-empty-state");

    expectClasses(empty, "bg-card rounded-lg shadow-sm border border-border p-12 text-center");
    expect(empty.textContent).toContain("🐷");

    const heading = within(empty, "h2");
    expect(heading.textContent).toBe("No inquiries yet.");
    expectClasses(heading, "text-xl font-semibold text-foreground mb-2");
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
