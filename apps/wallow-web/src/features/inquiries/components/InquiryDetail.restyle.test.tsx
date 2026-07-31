import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { routeHarness } from "@shared/testing/harness-routes";
import { beforeEach, describe, expect, it } from "vitest";

import { expectCatalogSelect } from "@shared/testing/catalog-select";
import {
  allByTestId,
  byTestId,
  expectBadge,
  expectClasses,
  expectTag,
  expectTokenColorsOnly,
  parentOf,
  waitForTestId,
  within,
  LIST_CARD_LIST,
} from "@shared/testing/style-contract";
import { InquiryDetail } from "./InquiryDetail";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Restyle spec for the inquiry-detail body (Wallow-urec.4.2). Every behavioural
 * contract — the loading / not-found / error branches, the status mutation, the
 * comment thread and its add form — stays pinned by the sibling
 * `InquiryDetail.test.tsx`, which the restyle must not edit.
 *
 * The detail page is the recipe's NARROW column, so its chrome is the padded
 * card surface rather than `.4.1`'s edge-to-edge list card: the comment thread
 * is a bordered sub-list INSIDE that card, not a second page-level card. The old
 * Blazor markup has no inquiry-detail page to port, so the classes here are the
 * parent bead's recipe applied to this page's parts.
 *
 * `inquiry-detail-card` and the per-comment `inquiry-comment-author` /
 * `inquiry-comment-body` / `inquiry-comment-internal-flag` testids are pure
 * additions — nothing pinned them before, and the row's `(internal)` marker
 * needs a handle to become a chip. Note the marker is `-internal-flag`, not
 * `-internal`: `inquiry-comment-internal` is already the add-form's checkbox.
 *
 * Wallow-lrlm.5.2 moves the comment thread onto `ListCard`/`ListRow` and both
 * chips onto `Badge`. This is the ONE list in the app that overrides the catalog
 * recipe rather than adopting it wholesale, and deliberately: the thread is a
 * sub-list inside the detail card, so it keeps the tighter `rounded-md` frame
 * and the tighter, left-aligned `px-4 py-3` cell. Both are expressed as caller
 * `className` overrides that tailwind-merge collapses against the recipe, which
 * is what the catalog's `className` prop is for — the alternative is a second
 * recipe in the catalog for a shape one screen uses. Flagged on the bead.
 *
 * The row id becomes `inquiry-comment-item`: `ListRow` derives `{name}-item` and
 * the derivation cannot be overridden. No e2e spec references the old
 * `inquiry-comment-row`.
 *
 * `inquiry-comments-empty` stays a `MutedText` and is NOT an `EmptyState`: it
 * renders no surface of its own, sitting bare inside the detail card, so giving
 * it the catalog's card would put a card inside a card. That sentence is F5.T3's
 * (`Text`) to place, not this task's.
 */

/**
 * Same control recipe the create-inquiry form adopts (see its restyle spec).
 *
 * NARROWED IN Wallow-lrlm.5.5, exactly as `CreateInquiryForm.restyle.test.tsx`
 * (Wallow-ov6w.4.3) narrowed its own copy of this constant: the three focus
 * utilities it used to carry (`focus:outline-none focus:ring-2 focus:ring-ring`)
 * described the HAND-ROLLED `<textarea>` this restyle wrote, not the shared look
 * it promised. The add-comment control is now the catalog `Textarea`, whose
 * recipe is `inputRecipe`'s string verbatim (`packages/ui`'s stated compat
 * guarantee, asserted there as an EXACT class set) plus `min-h-20` / `resize-y`
 * — and it carries no `focus:*` utility at all. The focus treatment therefore
 * comes from the catalog recipe and the browser's own outline, not from a class
 * this spec owns; adding a ring here would demand the multi-line control alone
 * carry a ring the `Input`s beside it have never had. That belongs to
 * `packages/ui` and to both recipes at once, not to a form migration.
 *
 * What replaces the dropped pin: `InquiryDetail.forms.test.tsx` asserts the
 * comment textarea carries `min-h-20` / `resize-y`, the two utilities only the
 * catalog recipe adds, so the control cannot silently go back to a hand-copied
 * string.
 */
const CONTROL =
  "w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground";

/** What the thread's `ListCard` override leaves behind: a flat, tighter frame. */
const SUB_LIST_SURFACE = "bg-card border border-border overflow-hidden rounded-md shadow-none";

/** What the thread's `ListRow` override leaves behind: a tighter, packed cell. */
const SUB_LIST_ROW =
  "flex items-center outline-none motion-safe:transition-colors hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring justify-start gap-3 px-4 py-3";

const INQUIRY = {
  id: "i1",
  name: "Ada Lovelace",
  email: "ada@example.com",
  company: "Analytical Engines",
  projectType: "web-app",
  status: "New",
  createdAt: "2026-07-15T00:00:00Z",
};

const TWO_COMMENTS = [
  {
    id: "c1",
    inquiryId: "i1",
    authorId: "u1",
    authorName: "Grace",
    content: "First contact made.",
    isInternal: false,
    createdAt: "2026-07-15T01:00:00Z",
  },
  {
    id: "c2",
    inquiryId: "i1",
    authorId: "u2",
    authorName: "Alan",
    content: "Internal note.",
    isInternal: true,
    createdAt: "2026-07-15T02:00:00Z",
  },
];

/**
 * Render a loaded inquiry with `comments` in cache and resolve the settled
 * element named by `anchor` — always a PRE-EXISTING testid, so a red-phase run
 * fails on the missing recipe class rather than timing out on the mount gate.
 */
async function renderDetail(comments: unknown[], anchor: string): Promise<HTMLElement> {
  routeHarness(harness, {
    "GET /v1/inquiries/i1": INQUIRY,
    "GET /v1/inquiries/i1/comments": comments,
  });
  renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
  return waitForTestId(anchor);
}

describe("InquiryDetail (restyle)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("seats the detail on the padded card surface", async () => {
    await renderDetail(TWO_COMMENTS, "inquiry-detail-heading");

    const card = byTestId("inquiry-detail-card");
    expectTag(card, "div");
    expectClasses(card, "bg-card rounded-lg shadow-sm border border-border p-8");
  });

  it("styles the back link as a quiet inline link", async () => {
    await renderDetail(TWO_COMMENTS, "inquiry-detail-heading");

    const link = byTestId("inquiry-detail-back-link");
    expectClasses(
      link,
      "inline-block text-sm text-muted-foreground hover:text-foreground no-underline mb-4",
    );
    // Regression guard: the same link, to the same place, with the same words.
    expect(link.getAttribute("href")).toBe("/dashboard/inquiries");
    expect(link.textContent?.trim()).toBe("Back to inquiries");
  });

  it("titles the page with the inquiry name", async () => {
    const heading = await renderDetail(TWO_COMMENTS, "inquiry-detail-heading");

    expectTag(heading, "h1");
    expect(heading.textContent).toBe("Ada Lovelace");
    expectClasses(heading, "text-3xl font-bold text-foreground");
  });

  it("subtitles the heading with the contact email", async () => {
    await renderDetail(TWO_COMMENTS, "inquiry-detail-heading");

    const email = byTestId("inquiry-detail-email");
    expect(email.textContent).toBe("ada@example.com");
    expectClasses(email, "text-sm text-muted-foreground");
  });

  // The chip was a `div` here and a `span` on the list rows — the same pill in
  // two elements. `Badge` is a `span`, so the detail's becomes one too.
  it("styles the current status as a chip without changing its text", async () => {
    await renderDetail(TWO_COMMENTS, "inquiry-detail-heading");

    const status = byTestId("inquiry-detail-status");
    expect(status.textContent).toBe("New");
    expectBadge(status, "neutral");
  });

  it("styles the status select like the shared text input", async () => {
    await renderDetail(TWO_COMMENTS, "inquiry-detail-heading");

    // Post-migration (Wallow-m5aq.5.3) the status control is a catalog `Select`,
    // so the shared control look arrives from its trigger recipe instead of this
    // page hand-copying the input's class string. The overlap the restyle
    // promised is asserted; the recipe's own additions are not this spec's
    // business.
    expectCatalogSelect("inquiry-status-select");
    expectClasses(
      byTestId("inquiry-status-select"),
      "w-full rounded-md border bg-background px-3 py-2 text-sm text-foreground",
    );
  });

  it("frames the comment thread as a bordered sub-list", async () => {
    const table = await renderDetail(TWO_COMMENTS, "inquiry-comments-table");

    expectTag(table, "ul");
    expectClasses(table, LIST_CARD_LIST);

    // The catalog surface, overridden down to a flat sub-list frame. Asserting
    // the overrides ARRIVED is only half of it: the classes they replace must be
    // gone, or tailwind-merge silently did nothing and this is a page-level card.
    const surface = parentOf(table);
    expectTag(surface, "div");
    expectClasses(surface, SUB_LIST_SURFACE);
    expect([...surface.classList]).not.toContain("rounded-lg");
    expect([...surface.classList]).not.toContain("shadow-sm");
  });

  it("styles every comment row with its author and body", async () => {
    await renderDetail(TWO_COMMENTS, "inquiry-comments-table");

    const rows = allByTestId("inquiry-comment-item");
    expect(rows).toHaveLength(TWO_COMMENTS.length);
    for (const row of rows) {
      expectTag(row, "li");
      expectClasses(row, SUB_LIST_ROW);
      expect([...row.classList]).not.toContain("justify-between");
    }

    const [first] = rows;

    const author = within(first, '[data-testid="inquiry-comment-author"]');
    expect(author.textContent).toBe("Grace");
    expectClasses(author, "text-xs font-medium text-muted-foreground");

    const body = within(first, '[data-testid="inquiry-comment-body"]');
    expect(body.textContent).toBe("First contact made.");
    expectClasses(body, "text-sm text-card-foreground");
  });

  it("marks an internal comment with a chip, keeping its wording", async () => {
    await renderDetail(TWO_COMMENTS, "inquiry-comments-table");

    const [, second] = allByTestId("inquiry-comment-item");

    const flag = within(second, '[data-testid="inquiry-comment-internal-flag"]');
    expect(flag.textContent).toBe("(internal)");
    expectBadge(flag, "neutral");
  });

  it("centers the empty comment thread without changing its wording", async () => {
    const empty = await renderDetail([], "inquiry-comments-empty");

    expect(empty.textContent).toBe("No comments yet.");
    expectClasses(empty, "text-center py-6");
  });

  it("styles the add-comment form and its textarea", async () => {
    await renderDetail(TWO_COMMENTS, "inquiry-comment-form");

    expectClasses(byTestId("inquiry-comment-form"), "space-y-3");

    const content = byTestId("inquiry-comment-content");
    expectTag(content, "textarea");
    expectClasses(content, CONTROL);
  });

  it("styles the detail with theme tokens only", async () => {
    await renderDetail(TWO_COMMENTS, "inquiry-detail-heading");

    expectTokenColorsOnly(byTestId("inquiry-detail-card"));
  });
});
