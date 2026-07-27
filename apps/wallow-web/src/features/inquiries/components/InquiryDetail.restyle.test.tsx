import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it } from "vitest";

import { expectCatalogSelect } from "../../../test/catalog-select";
import { installSdkClientMock } from "../../../test/sdk-client-mock";
import {
  allByTestId,
  byTestId,
  expectClasses,
  expectTag,
  expectTokenColorsOnly,
  waitForTestId,
  within,
} from "../../../test/style-contract";
import { InquiryDetail } from "./InquiryDetail";

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
 */

/** Same control recipe the create-inquiry form adopts (see its restyle spec). */
const CONTROL =
  "w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring";

const CHIP =
  "inline-block bg-accent text-accent-foreground text-xs font-medium px-2.5 py-0.5 rounded-full";

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

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Number.POSITIVE_INFINITY },
      mutations: { retry: false },
    },
  });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

/**
 * Render a loaded inquiry with `comments` in cache and resolve the settled
 * element named by `anchor` — always a PRE-EXISTING testid, so a red-phase run
 * fails on the missing recipe class rather than timing out on the mount gate.
 */
async function renderDetail(comments: unknown[], anchor: string): Promise<HTMLElement> {
  const client = newClient();
  client.setQueryData(["inquiries", "i1"], INQUIRY);
  client.setQueryData(["inquiries", "i1", "comments"], comments);
  renderWithClient(client, <InquiryDetail inquiryId="i1" />);
  return waitForTestId(anchor);
}

describe("InquiryDetail (restyle)", () => {
  beforeEach(() => {
    installSdkClientMock();
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
      "inline-block text-sm text-foreground/60 hover:text-foreground no-underline mb-4",
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
    expectClasses(email, "text-sm text-foreground/60");
  });

  it("styles the current status as a chip without changing its text", async () => {
    await renderDetail(TWO_COMMENTS, "inquiry-detail-heading");

    const status = byTestId("inquiry-detail-status");
    expect(status.textContent).toBe("New");
    expectClasses(status, CHIP);
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
    expectClasses(table, "divide-y divide-border rounded-md border border-border overflow-hidden");
  });

  it("styles every comment row with its author and body", async () => {
    await renderDetail(TWO_COMMENTS, "inquiry-comments-table");

    const rows = allByTestId("inquiry-comment-row");
    expect(rows).toHaveLength(TWO_COMMENTS.length);
    for (const row of rows) {
      expectTag(row, "li");
      expectClasses(row, "flex items-center gap-3 px-4 py-3 hover:bg-background/50");
    }

    const [first] = rows;

    const author = within(first, '[data-testid="inquiry-comment-author"]');
    expect(author.textContent).toBe("Grace");
    expectClasses(author, "text-xs font-medium text-foreground/60");

    const body = within(first, '[data-testid="inquiry-comment-body"]');
    expect(body.textContent).toBe("First contact made.");
    expectClasses(body, "text-sm text-card-foreground");
  });

  it("marks an internal comment with a chip, keeping its wording", async () => {
    await renderDetail(TWO_COMMENTS, "inquiry-comments-table");

    const [, second] = allByTestId("inquiry-comment-row");

    const flag = within(second, '[data-testid="inquiry-comment-internal-flag"]');
    expect(flag.textContent).toBe("(internal)");
    expectClasses(flag, CHIP);
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
