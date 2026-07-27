import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { chooseOption } from "../../../test/catalog-select";
import { installSdkClientMock, type SdkClientMock } from "../../../test/sdk-client-mock";
import { InquiryDetail } from "./InquiryDetail";

/**
 * Component spec for the inquiry-detail page body (Wallow-8w1h.7.4). Data flows
 * through the SDK query layer (`inquiriesQueries.detail()`/`comments()` +
 * `setStatusMutation`/`addCommentMutation`), so the network seam is the shared
 * SDK client's `fetch`, overridden per test via `installSdkClientMock`
 * (Wallow-evd5.2.6 — the retired `getWallowSdk()` facade is no longer in the
 * path). The detail + comment states are driven by seeding the
 * `['inquiries', id]` and `['inquiries', id, 'comments']` cache; mutations are
 * asserted via the recorded outgoing request (`sdk.calls`) and the live client's
 * `invalidateQueries`; error branches are driven with `sdk.rejectJson` /
 * `sdk.respond`.
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

let sdk: SdkClientMock;

function newClient(): QueryClient {
  // `staleTime: Infinity` keeps seeded cache entries fresh so the component reads
  // exactly the state each test plants. Without it, staleTime:0 triggers a
  // background refetch through the SDK client mock, which (on the default `{}`
  // responder) resolves a non-array/empty body and flips the query — hiding the
  // seeded not-found/comment content. jsdom's sync render masked this race; a
  // real browser lets the refetch win. Tests that exercise a real fetch (the
  // error case) seed nothing, so the initial fetch still fires regardless.
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

/** JSON `Response` body for a path-aware `sdk.respond` handler. */
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

/** Seed a loaded inquiry with its comment thread already in cache. */
function seedLoaded(client: QueryClient, comments: unknown = twoComments) {
  client.setQueryData(["inquiries", "i1"], inquiry);
  client.setQueryData(["inquiries", "i1", "comments"], comments);
}

describe("InquiryDetail — inquiry fields", () => {
  beforeEach(() => {
    sdk = installSdkClientMock();
  });

  it("renders the inquiry heading, back link, email, and current status when it loads", async () => {
    const client = newClient();
    seedLoaded(client);

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await expect
      .element(page.getByTestId("inquiry-detail-heading"))
      .toHaveTextContent("Ada Lovelace");
    await expect.element(page.getByTestId("inquiry-detail-back-link")).toBeInTheDocument();
    await expect.element(page.getByText("ada@example.com")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-detail-status")).toHaveTextContent("New");
  });

  it("renders the not-found state when the inquiry detail is null", async () => {
    const client = newClient();
    client.setQueryData(["inquiries", "i1"], null);
    client.setQueryData(["inquiries", "i1", "comments"], []);

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await expect.element(page.getByTestId("inquiry-detail-not-found")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-detail-heading")).not.toBeInTheDocument();
  });

  it("surfaces the RFC 7807 ProblemDetails detail when the detail query errors", async () => {
    const client = newClient();
    // Detail query errors (404); the comments query still resolves to an empty
    // array so its list render never sees a non-array body.
    sdk.respond((call) =>
      call.path.endsWith("/comments")
        ? jsonBody([])
        : jsonBody({ status: 404, detail: "Inquiry not found." }, 404),
    );

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await expect
      .element(page.getByTestId("inquiry-detail-error"))
      .toHaveTextContent("Inquiry not found.");
  });
});

describe("InquiryDetail — status change", () => {
  beforeEach(() => {
    sdk = installSdkClientMock();
  });

  it("changes status: selects a new status and PATCHes the inquiry status endpoint", async () => {
    const client = newClient();
    seedLoaded(client);
    // The post-success invalidation sweeps the detail subtree (detail +
    // comments); keep those refetches array-safe.
    sdk.resolveJson([]);

    await renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await chooseOption("inquiry-status-select", "Reviewed");
    await userEvent.click(page.getByTestId("inquiry-status-submit"));

    await vi.waitFor(() => {
      const statusCall = sdk.calls.find(
        (c) => c.method === "PATCH" && c.path === "/api/v1/inquiries/i1/status",
      );
      expect(statusCall).toBeDefined();
      expect(statusCall?.body).toEqual({ newStatus: "Reviewed" });
    });
  });

  it("invalidates the detail query after a successful status change", async () => {
    const client = newClient();
    seedLoaded(client);
    sdk.resolveJson([]);
    const invalidateSpy = vi.spyOn(client, "invalidateQueries");

    await renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await chooseOption("inquiry-status-select", "Reviewed");
    await userEvent.click(page.getByTestId("inquiry-status-submit"));

    await vi.waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["inquiries", "i1"] });
    });
  });

  it("surfaces the RFC 7807 ProblemDetails detail when a rejected status change fails", async () => {
    // The domain only allows sequential transitions
    // (New -> Reviewed -> Contacted -> Closed); Inquiry.cs `IsValidTransition`
    // rejects everything else with a 422 RFC 7807 ProblemDetails. The status
    // offers all four statuses unconditionally, so a user viewing a "New"
    // inquiry can pick "Closed" directly and MUST see the rejection surfaced —
    // mirroring the inquiry-comment-error / inquiry-detail-error pattern.
    const client = newClient();
    seedLoaded(client);
    sdk.rejectJson({ status: 422, detail: "Cannot transition from New to Closed." }, 422);

    await renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await chooseOption("inquiry-status-select", "Closed");
    await userEvent.click(page.getByTestId("inquiry-status-submit"));

    await expect
      .element(page.getByTestId("inquiry-status-error"))
      .toHaveTextContent("Cannot transition from New to Closed.");
  });
});

describe("InquiryDetail — comment thread", () => {
  beforeEach(() => {
    sdk = installSdkClientMock();
  });

  it("renders each seeded comment as an inquiry-comment-row inside the comments table", async () => {
    const client = newClient();
    seedLoaded(client);

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await expect.element(page.getByTestId("inquiry-comment-row").first()).toBeInTheDocument();
    expect(page.getByTestId("inquiry-comment-row").elements()).toHaveLength(2);
    await expect.element(page.getByTestId("inquiry-comments-table")).toBeInTheDocument();
    await expect.element(page.getByText("First contact made.")).toBeInTheDocument();
    await expect.element(page.getByText("Internal note.")).toBeInTheDocument();
  });

  it("renders the empty state and no rows when there are no comments", async () => {
    const client = newClient();
    seedLoaded(client, []);

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await expect.element(page.getByTestId("inquiry-comments-empty")).toBeInTheDocument();
    expect(page.getByTestId("inquiry-comment-row").elements()).toHaveLength(0);
  });

  it("shows a loading indicator while the comments query is pending", async () => {
    const client = newClient();
    client.setQueryData(["inquiries", "i1"], inquiry);
    // Only the comments query fires (detail is seeded); leave it never-settling.
    sdk.pending();

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await expect.element(page.getByTestId("inquiry-comments-loading")).toBeInTheDocument();
  });
});

describe("InquiryDetail — add comment", () => {
  beforeEach(() => {
    sdk = installSdkClientMock();
  });

  it("adds a public comment: POSTs the content to the inquiry comments endpoint", async () => {
    const client = newClient();
    seedLoaded(client);
    // The post-success invalidation refetches the comments thread; keep it array-safe.
    sdk.resolveJson([]);

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await userEvent.type(page.getByTestId("inquiry-comment-content"), "Following up");
    await userEvent.click(page.getByTestId("inquiry-comment-submit"));

    await vi.waitFor(() => {
      const addCall = sdk.calls.find(
        (c) => c.method === "POST" && c.path === "/api/v1/inquiries/i1/comments",
      );
      expect(addCall).toBeDefined();
      expect(addCall?.body).toEqual({ content: "Following up", isInternal: false });
    });
  });

  it("adds an internal comment when the internal checkbox is checked", async () => {
    const client = newClient();
    seedLoaded(client);
    sdk.resolveJson([]);

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await userEvent.type(page.getByTestId("inquiry-comment-content"), "Private note");
    await userEvent.click(page.getByTestId("inquiry-comment-internal"));
    await userEvent.click(page.getByTestId("inquiry-comment-submit"));

    await vi.waitFor(() => {
      const addCall = sdk.calls.find(
        (c) => c.method === "POST" && c.path === "/api/v1/inquiries/i1/comments",
      );
      expect(addCall).toBeDefined();
      expect(addCall?.body).toEqual({ content: "Private note", isInternal: true });
    });
  });

  it("invalidates the comments query after a successful add", async () => {
    const client = newClient();
    seedLoaded(client);
    sdk.resolveJson([]);
    const invalidateSpy = vi.spyOn(client, "invalidateQueries");

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await userEvent.type(page.getByTestId("inquiry-comment-content"), "Following up");
    await userEvent.click(page.getByTestId("inquiry-comment-submit"));

    await vi.waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: ["inquiries", "i1", "comments"],
      });
    });
  });

  it("surfaces the RFC 7807 ProblemDetails detail when add-comment fails", async () => {
    const client = newClient();
    seedLoaded(client);
    sdk.rejectJson({ status: 400, detail: "Comment must not be empty." }, 400);

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await userEvent.type(page.getByTestId("inquiry-comment-content"), "x");
    await userEvent.click(page.getByTestId("inquiry-comment-submit"));

    await expect
      .element(page.getByTestId("inquiry-comment-error"))
      .toHaveTextContent("Comment must not be empty.");
  });
});
