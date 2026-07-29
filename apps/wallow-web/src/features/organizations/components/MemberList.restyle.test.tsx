import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { beforeEach, describe, expect, it } from "vitest";

import {
  allByTestId,
  byTestId,
  expectClasses,
  expectTag,
  expectTokenColorsOnly,
  parentOf,
  waitForTestId,
  within,
} from "../../../test/style-contract";
import { MemberList } from "./MemberList";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Restyle spec for the org-detail member list (Wallow-urec.4.3). It asserts only
 * the chrome the restyle adds; the list's behaviour (query states, add/remove
 * mutations, and the `organization-detail-members-table` /
 * `organization-detail-member-row` / `organization-members-*` /
 * `organization-member-*` testids) stays pinned by `MemberList.test.tsx`, which
 * the restyle must not edit.
 *
 * Like the apps list (Wallow-urec.4.1), the members table drops the `ui` `Card`
 * wrapper — its fixed `p-6 space-y-6` fights the recipe's `px-6 py-4` row cells —
 * and the section becomes a titled block: heading, add-member form, then the
 * table on its own card surface.
 *
 * New testids (pure additions — no spec pins a section heading or the per-row
 * email cell today): `organization-members-heading`, `organization-member-email`.
 */

const MEMBERS = [
  {
    id: "u1",
    email: "ada@acme.io",
    firstName: "Ada",
    lastName: "L",
    enabled: true,
    roles: ["Owner"],
  },
  {
    id: "u2",
    email: "bob@acme.io",
    firstName: "Bob",
    lastName: "R",
    enabled: true,
    roles: ["Member"],
  },
];

/**
 * Render the member list seeded with `members` (omit for the loading state) and
 * resolve the settled element named by `anchor` — the state under test.
 */
async function renderMembers(members: unknown[] | undefined, anchor: string): Promise<HTMLElement> {
  if (members !== undefined) {
    harness.resolveJson(members);
  }
  renderWithWallow(<MemberList orgId="o1" />, { harness });
  return waitForTestId(anchor);
}

describe("MemberList (restyle)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("titles the members section", async () => {
    await renderMembers(MEMBERS, "organization-detail-members-table");

    const heading = byTestId("organization-members-heading");
    expectTag(heading, "h2");
    expect(heading.textContent).toBe("Members");
    expectClasses(heading, "text-xl font-semibold text-foreground mb-4");
  });

  it("lays the add-member form out as one inline row", async () => {
    await renderMembers(MEMBERS, "organization-detail-members-table");

    const form = byTestId("organization-member-add-form");
    expectClasses(form, "flex items-end gap-3 mb-4");

    // The shared Button is `w-full` by default; inline beside the input it sizes
    // to its label instead of stretching.
    expectClasses(byTestId("organization-member-add-submit"), "w-auto");
  });

  it("wraps the members table in the card surface", async () => {
    const table = await renderMembers(MEMBERS, "organization-detail-members-table");

    expectTag(table, "ul");
    expectClasses(table, "divide-y divide-border");

    const surface = parentOf(table);
    expectTag(surface, "div");
    expectClasses(surface, "bg-card rounded-lg shadow-sm border border-border overflow-hidden");
  });

  it("styles every member row as a table row", async () => {
    await renderMembers(MEMBERS, "organization-detail-members-table");

    const rows = allByTestId("organization-detail-member-row");
    expect(rows).toHaveLength(MEMBERS.length);
    for (const row of rows) {
      expectTag(row, "li");
      expectClasses(row, "flex items-center justify-between px-6 py-4 hover:bg-background/50");
    }
  });

  it("renders each row's email cell and a compact remove action", async () => {
    await renderMembers(MEMBERS, "organization-detail-members-table");

    const [first] = allByTestId("organization-detail-member-row");

    const email = within(first, '[data-testid="organization-member-email"]');
    expect(email.textContent).toBe("ada@acme.io");
    expectClasses(email, "text-sm font-medium text-card-foreground");

    const remove = within(first, '[data-testid="organization-member-remove"]');
    expectClasses(remove, "w-auto");
    expect(remove.textContent?.trim()).toBe("Remove");
  });

  it("presents the empty state as a centered card that keeps its sentence", async () => {
    const empty = await renderMembers([], "organization-members-empty");

    expectClasses(empty, "bg-card rounded-lg shadow-sm border border-border p-8 text-center");
    expect(empty.textContent).toBe("No members yet.");
  });

  it("centers the loading state without changing its wording", async () => {
    harness.pending();
    const loading = await renderMembers(undefined, "organization-members-loading");

    expect(loading.textContent).toBe("Loading members…");
    expectClasses(loading, "text-center py-8");
  });

  it("styles the members section with theme tokens only", async () => {
    await renderMembers(MEMBERS, "organization-detail-members-table");

    expectTokenColorsOnly(parentOf(byTestId("organization-members-heading")));
  });
});
