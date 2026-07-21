import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { InquiryList } from "./InquiryList";

/**
 * Component spec for the inquiries list page (Wallow-8w1h.7.2), mirroring
 * OrganizationList.test.tsx. The `getWallowSdk()` facade is mocked so the list
 * query is inert; list/empty states are driven by seeding the `['inquiries']`
 * cache with `setQueryData`, and the loading state by leaving the query to hit a
 * never-resolving facade call.
 *
 * DIVERGENCE reconciliation (see bead 7.2 note): task 7 said to "copy the C# E2E
 * InquiryPage page object's testids", but InquiryPage.cs only carries the public
 * SUBMIT-FORM testids — there is NO Blazor admin list UI or list-row testid to
 * mirror. So this list follows the Organizations `{page}-{element}` convention
 * per the bead's own acceptance: page root `dashboard-inquiries`, per-row
 * `inquiry-item`, plus `inquiry-item-status` for the acceptance's "showing status
 * per inquiry" requirement, `inquiries-empty-state`, and `inquiries-loading`.
 */

// Hoisted so the vi.mock factory and the test bodies share the same spies. Mocks
// the full inquiries facade slice, though only `list` is exercised here.
const mocks = vi.hoisted(() => ({
  list: vi.fn(),
  create: vi.fn(),
  get: vi.fn(),
  comments: vi.fn(),
  addComment: vi.fn(),
  setStatus: vi.fn(),
}));

// Mock the facade module the feature's api.ts imports (`../../lib/wallow-sdk`
// from features/inquiries; `../../../lib/wallow-sdk` from here).
vi.mock("../../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    inquiries: {
      list: mocks.list,
      create: mocks.create,
      get: mocks.get,
      comments: mocks.comments,
      addComment: mocks.addComment,
      setStatus: mocks.setStatus,
    },
  }),
}));

function newClient(): QueryClient {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("InquiryList", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders each seeded inquiry as an inquiry-item element", async () => {
    const client = newClient();
    client.setQueryData(
      ["inquiries"],
      [
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
      ],
    );

    renderWithClient(client, <InquiryList />);

    await expect.element(page.getByTestId("inquiry-item").first()).toBeInTheDocument();
    expect(page.getByTestId("inquiry-item").elements()).toHaveLength(2);
    await expect.element(page.getByText("Ada Lovelace")).toBeInTheDocument();
    await expect.element(page.getByText("Grace Hopper")).toBeInTheDocument();
  });

  it("shows the status for each inquiry", async () => {
    const client = newClient();
    client.setQueryData(
      ["inquiries"],
      [
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
      ],
    );

    renderWithClient(client, <InquiryList />);

    await expect.element(page.getByTestId("inquiry-item-status").first()).toBeInTheDocument();
    const statuses = page.getByTestId("inquiry-item-status").elements();
    expect(statuses).toHaveLength(2);
    expect(statuses.map((el) => el.textContent)).toEqual(["New", "Contacted"]);
  });

  it("renders the empty state and no rows when the inquiry list is empty", async () => {
    const client = newClient();
    client.setQueryData(["inquiries"], []);

    renderWithClient(client, <InquiryList />);

    await expect.element(page.getByTestId("inquiries-empty-state")).toBeInTheDocument();
    expect(page.getByTestId("inquiry-item").elements()).toHaveLength(0);
  });

  it("renders a loading indicator while the list query is pending", async () => {
    const client = newClient();
    // No cached data -> the query fires; the facade never resolves, so the
    // component stays in its loading state.
    mocks.list.mockReturnValue(new Promise<never>(() => {}));

    renderWithClient(client, <InquiryList />);

    await expect.element(page.getByTestId("inquiries-loading")).toBeInTheDocument();
    expect(page.getByTestId("inquiry-item").elements()).toHaveLength(0);
  });
});
