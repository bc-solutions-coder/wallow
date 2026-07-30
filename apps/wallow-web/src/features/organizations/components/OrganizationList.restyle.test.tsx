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

  it("wraps the list in the card surface", async () => {
    const list = await renderList(ORGS, "organizations-table");

    const surface = parentOf(list);
    expectTag(surface, "div");
    expectClasses(surface, "bg-card rounded-lg shadow-sm border border-border overflow-hidden");
  });

  it("keeps the list a ul and divides its rows", async () => {
    const list = await renderList(ORGS, "organizations-table");

    expectTag(list, "ul");
    expectClasses(list, "divide-y divide-border");
  });

  it("styles every organization row as a table row", async () => {
    await renderList(ORGS, "organizations-table");

    const rows = allByTestId("organization-item");
    expect(rows).toHaveLength(ORGS.length);
    for (const row of rows) {
      expectTag(row, "li");
      expectClasses(row, "flex items-center justify-between px-6 py-4 hover:bg-background/50");
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
    expectClasses(
      chip,
      "inline-block bg-accent text-accent-foreground text-xs font-medium px-2.5 py-0.5 rounded-full",
    );
  });

  it("renders the domain cell only for orgs that have one", async () => {
    await renderList(ORGS, "organizations-table");

    const [withDomain, withoutDomain] = allByTestId("organization-item");

    const domain = within(withDomain, '[data-testid="organization-item-domain"]');
    expect(domain.textContent).toBe("acme.io");
    expectClasses(domain, "text-sm text-foreground/70 font-mono");

    expect(withoutDomain.querySelector('[data-testid="organization-item-domain"]')).toBeNull();
  });

  it("presents the empty state as a centered card that keeps its sentence", async () => {
    const empty = await renderList([], "organizations-empty-state");

    expectClasses(empty, "bg-card rounded-lg shadow-sm border border-border p-12 text-center");
    // The organizations page carries the office emoji in the restored design
    // (the pig is the apps page); both use the same oversized-glyph block.
    expect(empty.textContent).toContain("🏢");

    const heading = within(empty, "h2");
    expect(heading.textContent).toBe("No organizations yet.");
    expectClasses(heading, "text-xl font-semibold text-foreground mb-2");
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
