import {
  createSdkHarness,
  failsWith,
  neverSettles,
  routeHarness,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { isApiFailure } from "@bc-solutions-coder/api-errors";
import type { UnhandledFailure } from "@bc-solutions-coder/query";

import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { chooseOption } from "@bc-solutions-coder/testing/catalog-select";
import { expectSwept } from "@bc-solutions-coder/testing/invalidation";
import { inquiriesGetByIdQueryKey, inquiriesGetCommentsQueryKey } from "../api";
import { InquiryDetail } from "./InquiryDetail";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Behaviour spec for the inquiry-detail page body: the inquiry fields, the
 * status change, the comment thread, and adding a comment.
 *
 * Every state is driven by ANSWERING the detail and comments reads
 * (`routeHarness`), not by seeding a cache key. Error and pending branches are
 * scoped to ONE operation (`failsWith` / `neverSettles`) — the reads behind the
 * failing control still have to succeed for it to be on screen.
 *
 * The status control is a catalog `Select`, so picking a status goes through
 * `chooseOption`; `userEvent.selectOptions` drives only an `HTMLSelectElement`.
 */

/** JSON `Response` body for a path-aware `harness.respond` handler. */
function jsonBody(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: { "content-type": "application/json" },
  });
}

const inquiry = {
  id: "i1",
  name: "Ada Lovelace",
  email: "ada@example.com",
  company: "Analytical Engines",
  projectType: "web-app",
  status: "New",
  createdAt: "2026-07-15T00:00:00Z",
};

const twoComments = [
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

/** Answer the detail + comments reads with a loaded inquiry and its thread. */
function seedLoaded(comments: unknown = twoComments, extraRoutes: Record<string, unknown> = {}) {
  routeHarness(
    harness,
    {
      "GET /v1/inquiries/i1": inquiry,
      "GET /v1/inquiries/i1/comments": comments,
      ...extraRoutes,
    },
    { fallback: [] },
  );
}

/**
 * Wait for the detail read to paint before driving a control: the reads go over
 * the wire rather than out of a pre-seeded cache, so the screen's controls do
 * not exist at first paint.
 */
async function awaitLoaded(): Promise<void> {
  await expect.element(page.getByTestId("inquiry-detail-heading")).toBeInTheDocument();
}

describe("InquiryDetail — inquiry fields", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the inquiry heading, back link, email, and current status when it loads", async () => {
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });

    await expect
      .element(page.getByTestId("inquiry-detail-heading"))
      .toHaveTextContent("Ada Lovelace");
    const backLink = page.getByTestId("inquiry-detail-back-link");
    await expect.element(backLink).toBeInTheDocument();
    expect(backLink.element().getAttribute("href")).toBe("/dashboard/inquiries");
    expect(backLink.element().textContent?.trim()).toBe("Back to inquiries");
    await expect
      .element(page.getByTestId("inquiry-detail-email"))
      .toHaveTextContent("ada@example.com");
    await expect.element(page.getByTestId("inquiry-detail-status")).toHaveTextContent("New");
  });

  it("surfaces the RFC 7807 ProblemDetails detail when the detail query errors", async () => {
    // Only the detail query errors; the comments query still resolves to an
    // empty array so its list render never sees a non-array body.
    harness.respond((call) =>
      call.path.endsWith("/comments")
        ? jsonBody([])
        : jsonBody({ status: 404, code: "Inquiries.NotFound", detail: "Inquiry not found." }, 404),
    );

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });

    await expect
      .element(page.getByTestId("inquiry-detail-error"))
      .toHaveTextContent("Inquiry not found.");
  });
});

describe("InquiryDetail — status change", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("changes status: selects a new status and PATCHes the inquiry status endpoint", async () => {
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await awaitLoaded();

    await chooseOption("inquiry-status-select", "Reviewed");
    await userEvent.click(page.getByTestId("inquiry-status-submit"));

    await vi.waitFor(() => {
      const statusCall = harness.calls.find(
        (c) => c.method === "PATCH" && c.path === "/api/v1/inquiries/i1/status",
      );
      expect(statusCall).toBeDefined();
      expect(statusCall?.body).toEqual({ newStatus: "Reviewed" });
    });
  });

  it("sweeps the detail query after a successful status change", async () => {
    seedLoaded();

    const { queryClient } = renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await awaitLoaded();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    await chooseOption("inquiry-status-select", "Reviewed");
    await userEvent.click(page.getByTestId("inquiry-status-submit"));

    await expectSwept(invalidateSpy, inquiriesGetByIdQueryKey({ path: { id: "i1" } }));
  });

  it("leaves a rejected status change to the toast rather than an inline banner", async () => {
    // The domain only allows sequential transitions
    // (New -> Reviewed -> Contacted -> Closed) and rejects the rest with a 422.
    // The control offers all four statuses unconditionally, so a user viewing a
    // "New" inquiry can pick "Closed". The rejection is the toast's to show: the
    // page renders no surface of its own for it.
    seedLoaded(twoComments, {
      "PATCH /v1/inquiries/i1/status": failsWith(
        {
          status: 422,
          code: "Inquiries.InvalidTransition",
          detail: "Cannot transition from New to Closed.",
        },
        422,
      ),
    });
    const unhandled: UnhandledFailure[] = [];

    renderWithWallow(<InquiryDetail inquiryId="i1" />, {
      harness,
      onUnhandledFailure: (failure) => {
        unhandled.push(failure);
      },
    });
    await awaitLoaded();

    await chooseOption("inquiry-status-select", "Closed");
    await userEvent.click(page.getByTestId("inquiry-status-submit"));

    await expect.poll(() => unhandled.length).toBe(1);
    expect(unhandled[0]?.kind).toBe("mutation");
    expect(isApiFailure(unhandled[0]?.error) && unhandled[0].error.status).toBe(422);
    expect(page.getByTestId("inquiry-status-error").elements()).toHaveLength(0);
  });
});

describe("InquiryDetail — comment thread", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders each seeded comment as an inquiry-comment-item inside the comments table", async () => {
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });

    await expect.element(page.getByTestId("inquiry-comment-item").first()).toBeInTheDocument();
    expect(page.getByTestId("inquiry-comment-item").elements()).toHaveLength(2);
    await expect.element(page.getByTestId("inquiry-comments-table")).toBeInTheDocument();
    await expect.element(page.getByText("First contact made.")).toBeInTheDocument();
    await expect.element(page.getByText("Internal note.")).toBeInTheDocument();
    expect(
      page
        .getByTestId("inquiry-comment-author")
        .elements()
        .map((el) => el.textContent),
    ).toEqual(["Grace", "Alan"]);
    // Only the internal comment is marked, and `-internal-flag` is that marker —
    // `inquiry-comment-internal` is the add-form's checkbox.
    const flags = page.getByTestId("inquiry-comment-internal-flag").elements();
    expect(flags).toHaveLength(1);
    expect(flags[0]?.textContent).toBe("(internal)");
  });

  it("renders the empty state and no rows when there are no comments", async () => {
    seedLoaded([]);

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });

    await expect.element(page.getByTestId("inquiry-comments-empty")).toBeInTheDocument();
    expect(page.getByTestId("inquiry-comments-empty").element().textContent).toBe(
      "No comments yet.",
    );
    expect(page.getByTestId("inquiry-comment-item").elements()).toHaveLength(0);
  });

  it("shows a loading indicator while the comments query is pending", async () => {
    // Only the COMMENTS read hangs: the detail has to resolve for the thread to
    // render at all, so suspending every request would assert nothing.
    seedLoaded(undefined, { "GET /v1/inquiries/i1/comments": neverSettles() });

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });

    await expect.element(page.getByTestId("inquiry-comments-loading")).toBeInTheDocument();
  });
});

describe("InquiryDetail — add comment", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("adds a public comment: POSTs the content to the inquiry comments endpoint", async () => {
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("inquiry-comment-content"), "Following up");
    await userEvent.click(page.getByTestId("inquiry-comment-submit"));

    await vi.waitFor(() => {
      const addCall = harness.calls.find(
        (c) => c.method === "POST" && c.path === "/api/v1/inquiries/i1/comments",
      );
      expect(addCall).toBeDefined();
      expect(addCall?.body).toEqual({ content: "Following up", isInternal: false });
    });
  });

  it("adds an internal comment when the internal checkbox is checked", async () => {
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("inquiry-comment-content"), "Private note");
    await userEvent.click(page.getByTestId("inquiry-comment-internal"));
    await userEvent.click(page.getByTestId("inquiry-comment-submit"));

    await vi.waitFor(() => {
      const addCall = harness.calls.find(
        (c) => c.method === "POST" && c.path === "/api/v1/inquiries/i1/comments",
      );
      expect(addCall).toBeDefined();
      expect(addCall?.body).toEqual({ content: "Private note", isInternal: true });
    });
  });

  it("sweeps the comments query after a successful add", async () => {
    seedLoaded();

    const { queryClient } = renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await awaitLoaded();
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    await userEvent.type(page.getByTestId("inquiry-comment-content"), "Following up");
    await userEvent.click(page.getByTestId("inquiry-comment-submit"));

    await expectSwept(invalidateSpy, inquiriesGetCommentsQueryKey({ path: { id: "i1" } }));
  });

  it("surfaces the RFC 7807 ProblemDetails detail when add-comment fails", async () => {
    seedLoaded(twoComments, {
      "POST /v1/inquiries/i1/comments": failsWith(
        { status: 400, code: "Inquiries.CommentEmpty", detail: "Comment must not be empty." },
        400,
      ),
    });

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("inquiry-comment-content"), "x");
    await userEvent.click(page.getByTestId("inquiry-comment-submit"));

    await expect
      .element(page.getByTestId("inquiry-comment-error"))
      .toHaveTextContent("Comment must not be empty.");
  });
});
