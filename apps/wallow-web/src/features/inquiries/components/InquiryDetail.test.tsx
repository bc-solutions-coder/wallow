/** @vitest-environment jsdom */
import * as matchers from "@testing-library/jest-dom/matchers";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { InquiryDetail } from "./InquiryDetail";

// No global `expect` (vitest `globals` is off) — register jest-dom matchers.
expect.extend(matchers);

/**
 * Component spec for the inquiry-detail page body (Wallow-8w1h.7.4). The
 * `getWallowSdk()` facade is mocked so detail/comments queries are inert; the
 * detail + comment states are driven by the `['inquiries', id]` and
 * `['inquiries', id, 'comments']` cache, and add-comment/status-change delegate
 * through the api.ts mutation factories to the mocked facade slice.
 *
 * Testids follow `{page}-{element}` kebab-case. Per the scout's CRITICAL 7.4
 * reconciliation there is NO Blazor/C# `InquiryPage` oracle for the
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
 */

const mocks = vi.hoisted(() => ({
  get: vi.fn(),
  comments: vi.fn(),
  addComment: vi.fn(),
  setStatus: vi.fn(),
}));

// InquiryDetail lives at src/features/inquiries/components/, so the facade module
// (`../../lib/wallow-sdk` from api.ts) is `../../../lib/wallow-sdk` from here.
vi.mock("../../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    inquiries: {
      get: mocks.get,
      comments: mocks.comments,
      addComment: mocks.addComment,
      setStatus: mocks.setStatus,
    },
  }),
}));

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
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
    vi.clearAllMocks();
  });

  it("renders the inquiry heading, back link, email, and current status when it loads", async () => {
    const client = newClient();
    seedLoaded(client);

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    const heading = await screen.findByTestId("inquiry-detail-heading");
    expect(heading).toHaveTextContent("Ada Lovelace");
    expect(screen.getByTestId("inquiry-detail-back-link")).toBeInTheDocument();
    expect(screen.getByText("ada@example.com")).toBeInTheDocument();
    expect(screen.getByTestId("inquiry-detail-status")).toHaveTextContent("New");
  });

  it("renders the not-found state when the inquiry detail is null", async () => {
    const client = newClient();
    client.setQueryData(["inquiries", "i1"], null);
    client.setQueryData(["inquiries", "i1", "comments"], []);

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    expect(await screen.findByTestId("inquiry-detail-not-found")).toBeInTheDocument();
    expect(screen.queryByTestId("inquiry-detail-heading")).not.toBeInTheDocument();
  });

  it("surfaces the RFC 7807 ProblemDetails detail when the detail query errors", async () => {
    const client = newClient();
    mocks.get.mockRejectedValue({ status: 404, detail: "Inquiry not found." });
    mocks.comments.mockResolvedValue([]);

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    const error = await screen.findByTestId("inquiry-detail-error");
    expect(error).toHaveTextContent("Inquiry not found.");
  });
});

describe("InquiryDetail — status change", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("changes status: selects a new status and delegates to inquiries.setStatus", async () => {
    const user = userEvent.setup();
    const client = newClient();
    seedLoaded(client);
    mocks.setStatus.mockResolvedValue({ ...inquiry, status: "Reviewed" });

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await user.selectOptions(await screen.findByTestId("inquiry-status-select"), "Reviewed");
    await user.click(screen.getByTestId("inquiry-status-submit"));

    await waitFor(() => {
      expect(mocks.setStatus).toHaveBeenCalledWith("i1", "Reviewed");
    });
  });

  it("invalidates the detail query after a successful status change", async () => {
    const user = userEvent.setup();
    const client = newClient();
    seedLoaded(client);
    const invalidateSpy = vi.spyOn(client, "invalidateQueries");
    mocks.setStatus.mockResolvedValue({ ...inquiry, status: "Reviewed" });

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await user.selectOptions(await screen.findByTestId("inquiry-status-select"), "Reviewed");
    await user.click(screen.getByTestId("inquiry-status-submit"));

    await waitFor(() => {
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
    const user = userEvent.setup();
    const client = newClient();
    seedLoaded(client);
    mocks.setStatus.mockRejectedValue({
      status: 422,
      detail: "Cannot transition from New to Closed.",
    });

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await user.selectOptions(await screen.findByTestId("inquiry-status-select"), "Closed");
    await user.click(screen.getByTestId("inquiry-status-submit"));

    const error = await screen.findByTestId("inquiry-status-error");
    expect(error).toHaveTextContent("Cannot transition from New to Closed.");
  });
});

describe("InquiryDetail — comment thread", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders each seeded comment as an inquiry-comment-row inside the comments table", async () => {
    const client = newClient();
    seedLoaded(client);

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    const rows = await screen.findAllByTestId("inquiry-comment-row");
    expect(rows).toHaveLength(2);
    expect(screen.getByTestId("inquiry-comments-table")).toBeInTheDocument();
    expect(screen.getByText("First contact made.")).toBeInTheDocument();
    expect(screen.getByText("Internal note.")).toBeInTheDocument();
  });

  it("renders the empty state and no rows when there are no comments", async () => {
    const client = newClient();
    seedLoaded(client, []);

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    expect(await screen.findByTestId("inquiry-comments-empty")).toBeInTheDocument();
    expect(screen.queryAllByTestId("inquiry-comment-row")).toHaveLength(0);
  });

  it("shows a loading indicator while the comments query is pending", async () => {
    const client = newClient();
    client.setQueryData(["inquiries", "i1"], inquiry);
    mocks.comments.mockReturnValue(new Promise<never>(() => {}));

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    expect(await screen.findByTestId("inquiry-comments-loading")).toBeInTheDocument();
  });
});

describe("InquiryDetail — add comment", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("adds a public comment: delegates the content to inquiries.addComment", async () => {
    const user = userEvent.setup();
    const client = newClient();
    seedLoaded(client);
    mocks.addComment.mockResolvedValue(undefined);

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await user.type(await screen.findByTestId("inquiry-comment-content"), "Following up");
    await user.click(screen.getByTestId("inquiry-comment-submit"));

    await waitFor(() => {
      expect(mocks.addComment).toHaveBeenCalledWith("i1", {
        content: "Following up",
        isInternal: false,
      });
    });
  });

  it("adds an internal comment when the internal checkbox is checked", async () => {
    const user = userEvent.setup();
    const client = newClient();
    seedLoaded(client);
    mocks.addComment.mockResolvedValue(undefined);

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await user.type(await screen.findByTestId("inquiry-comment-content"), "Private note");
    await user.click(screen.getByTestId("inquiry-comment-internal"));
    await user.click(screen.getByTestId("inquiry-comment-submit"));

    await waitFor(() => {
      expect(mocks.addComment).toHaveBeenCalledWith("i1", {
        content: "Private note",
        isInternal: true,
      });
    });
  });

  it("invalidates the comments query after a successful add", async () => {
    const user = userEvent.setup();
    const client = newClient();
    seedLoaded(client);
    const invalidateSpy = vi.spyOn(client, "invalidateQueries");
    mocks.addComment.mockResolvedValue(undefined);

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await user.type(await screen.findByTestId("inquiry-comment-content"), "Following up");
    await user.click(screen.getByTestId("inquiry-comment-submit"));

    await waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: ["inquiries", "i1", "comments"],
      });
    });
  });

  it("surfaces the RFC 7807 ProblemDetails detail when add-comment fails", async () => {
    const user = userEvent.setup();
    const client = newClient();
    seedLoaded(client);
    mocks.addComment.mockRejectedValue({ status: 400, detail: "Comment must not be empty." });

    renderWithClient(client, <InquiryDetail inquiryId="i1" />);

    await user.type(await screen.findByTestId("inquiry-comment-content"), "x");
    await user.click(screen.getByTestId("inquiry-comment-submit"));

    const error = await screen.findByTestId("inquiry-comment-error");
    expect(error).toHaveTextContent("Comment must not be empty.");
  });
});
