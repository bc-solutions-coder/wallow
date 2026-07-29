import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { failsWith, neverSettles, routeHarness } from "../../../test/harness-routes";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { chooseOption } from "../../../test/catalog-select";
import { expectSwept } from "../../../test/invalidation";
import { inquiriesGetByIdQueryKey, inquiriesGetCommentsQueryKey } from "../api";
import { InquiryDetail } from "./InquiryDetail";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Component spec for the inquiry-detail page body (Wallow-8w1h.7.4). Data flows
 * through the GENERATED query surface (`inquiriesGetByIdOptions`/
 * `inquiriesGetCommentsOptions` + `inquiriesUpdateStatusMutation`/
 * `inquiriesAddCommentMutation`), so the network seam is the SDK instance the
 * render puts on the router context, backed by `createSdkHarness()`
 * (Wallow-pu6a.5.5). The detail + comment states are driven by ANSWERING those
 * two requests (`routeHarness`) rather than seeding a cache key; mutations are
 * asserted via the recorded outgoing request (`harness.calls`) and, for the
 * post-success sweep, by running the filter handed to `invalidateQueries`
 * against the real generated key (`expectSwept`). Error and pending branches are
 * scoped to ONE operation (`failsWith` / `neverSettles`), because the reads
 * behind the failing control still have to succeed for it to be on screen.
 *
 * Testids follow `{page}-{element}` kebab-case. Per the scout's CRITICAL 7.4
 * reconciliation there is NO C# `InquiryPage` oracle for the
 * detail/comments/status flow (the page object only covers the public submit
 * form: inquiry-name/email/phone/company/project-type/budget-range/timeline/
 * message/submit/success/error), so these testids are invented following the
 * Organizations `OrganizationDetail`/`MemberList` convention:
 * `inquiry-detail-heading`, `inquiry-detail-back-link`, `inquiry-detail-not-found`,
 * `inquiry-detail-error`, `inquiry-detail-status`, `inquiry-status-select` +
 * `inquiry-status-submit` + `inquiry-status-error`,
 * `inquiry-comments-table` + `inquiry-comment-row`,
 * `inquiry-comments-loading` / `inquiry-comments-empty`, `inquiry-comment-content` +
 * `inquiry-comment-internal` + `inquiry-comment-submit`, `inquiry-comment-error`.
 *
 * The status control is the catalog `Select` (Wallow-m5aq.5.3), not a native
 * `<select>`, so picking a status goes through `chooseOption` — open the
 * combobox trigger, click the named option out of the portalled listbox —
 * rather than `userEvent.selectOptions`, which only drives an
 * `HTMLSelectElement`. The testid `inquiry-status-select` is unchanged; it now
 * names the trigger.
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
 * Wait for the detail read to paint before driving a control. The screen's
 * controls no longer exist at first paint: the reads go over the wire rather
 * than out of a pre-seeded cache, so every interaction spec has to settle the
 * detail query first.
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
    await expect.element(page.getByTestId("inquiry-detail-back-link")).toBeInTheDocument();
    await expect.element(page.getByText("ada@example.com")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-detail-status")).toHaveTextContent("New");
  });

  it("renders the not-found state when the inquiry detail is null", async () => {
    routeHarness(
      harness,
      {
        "GET /v1/inquiries/i1": null,
        "GET /v1/inquiries/i1/comments": [],
      },
      { fallback: [] },
    );

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });

    await expect.element(page.getByTestId("inquiry-detail-not-found")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-detail-heading")).not.toBeInTheDocument();
  });

  it("surfaces the RFC 7807 ProblemDetails detail when the detail query errors", async () => {
    // Detail query errors (404); the comments query still resolves to an empty
    // array so its list render never sees a non-array body.
    harness.respond((call) =>
      call.path.endsWith("/comments")
        ? jsonBody([])
        : jsonBody({ status: 404, detail: "Inquiry not found." }, 404),
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

  it("surfaces the RFC 7807 ProblemDetails detail when a rejected status change fails", async () => {
    // The domain only allows sequential transitions
    // (New -> Reviewed -> Contacted -> Closed); Inquiry.cs `IsValidTransition`
    // rejects everything else with a 422 RFC 7807 ProblemDetails. The status
    // offers all four statuses unconditionally, so a user viewing a "New"
    // inquiry can pick "Closed" directly and MUST see the rejection surfaced —
    // mirroring the inquiry-comment-error / inquiry-detail-error pattern.
    seedLoaded(twoComments, {
      "PATCH /v1/inquiries/i1/status": failsWith(
        { status: 422, detail: "Cannot transition from New to Closed." },
        422,
      ),
    });

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });
    await awaitLoaded();

    await chooseOption("inquiry-status-select", "Closed");
    await userEvent.click(page.getByTestId("inquiry-status-submit"));

    await expect
      .element(page.getByTestId("inquiry-status-error"))
      .toHaveTextContent("Cannot transition from New to Closed.");
  });
});

describe("InquiryDetail — comment thread", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders each seeded comment as an inquiry-comment-row inside the comments table", async () => {
    seedLoaded();

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });

    await expect.element(page.getByTestId("inquiry-comment-row").first()).toBeInTheDocument();
    expect(page.getByTestId("inquiry-comment-row").elements()).toHaveLength(2);
    await expect.element(page.getByTestId("inquiry-comments-table")).toBeInTheDocument();
    await expect.element(page.getByText("First contact made.")).toBeInTheDocument();
    await expect.element(page.getByText("Internal note.")).toBeInTheDocument();
  });

  it("renders the empty state and no rows when there are no comments", async () => {
    seedLoaded([]);

    renderWithWallow(<InquiryDetail inquiryId="i1" />, { harness });

    await expect.element(page.getByTestId("inquiry-comments-empty")).toBeInTheDocument();
    expect(page.getByTestId("inquiry-comment-row").elements()).toHaveLength(0);
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
        { status: 400, detail: "Comment must not be empty." },
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
