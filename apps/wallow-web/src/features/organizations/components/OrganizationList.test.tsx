import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { OrganizationList } from "./OrganizationList";

/**
 * Component spec for the CANONICAL list-page (Wallow-8w1h.4.2). The
 * `getWallowSdk()` facade is mocked so the list query is inert; list/empty
 * states are driven by seeding the `['orgs']` cache with `setQueryData`, and the
 * loading state by leaving the query to hit a never-resolving facade call.
 *
 * Runs under the browser-mode project (real Chromium via `vitest-browser-react`;
 * Wallow-xzha.3.2), so there is no jsdom pragma and no `@testing-library/*`.
 * Testids follow `{page}-{element}` kebab-case; per-row testid is
 * `organization-item` (the bead spec deliberately uses `organization-item`, not
 * `organizations-row`).
 */

// Hoisted so the vi.mock factory and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  list: vi.fn(),
  get: vi.fn(),
  create: vi.fn(),
}));

// Mock the facade module the feature's api.ts imports (`../../lib/wallow-sdk`
// from features/organizations; `../../../lib/wallow-sdk` from here).
vi.mock("../../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    organizations: { list: mocks.list, get: mocks.get, create: mocks.create },
  }),
}));

function newClient(): QueryClient {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("OrganizationList", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders each seeded org as an organization-item element", async () => {
    const client = newClient();
    client.setQueryData(
      ["orgs"],
      [
        { id: "o1", name: "Acme", domain: null, memberCount: "3" },
        { id: "o2", name: "Globex", domain: "globex.io", memberCount: "1" },
      ],
    );

    renderWithClient(client, <OrganizationList />);

    await expect.element(page.getByTestId("organization-item").first()).toBeInTheDocument();
    expect(page.getByTestId("organization-item").elements()).toHaveLength(2);
    // Exact match: `getByText` is substring-by-default in the browser provider,
    // so "Globex" would otherwise also match the "globex.io" domain cell.
    await expect.element(page.getByText("Acme", { exact: true })).toBeInTheDocument();
    await expect.element(page.getByText("Globex", { exact: true })).toBeInTheDocument();
  });

  it("renders the empty state and no rows when the org list is empty", async () => {
    const client = newClient();
    client.setQueryData(["orgs"], []);

    renderWithClient(client, <OrganizationList />);

    await expect.element(page.getByTestId("organizations-empty-state")).toBeInTheDocument();
    expect(page.getByTestId("organization-item").elements()).toHaveLength(0);
  });

  it("renders a loading indicator while the list query is pending", async () => {
    const client = newClient();
    // No cached data -> the query fires; the facade never resolves, so the
    // component stays in its loading state.
    mocks.list.mockReturnValue(new Promise<never>(() => {}));

    renderWithClient(client, <OrganizationList />);

    await expect.element(page.getByTestId("organizations-loading")).toBeInTheDocument();
    expect(page.getByTestId("organization-item").elements()).toHaveLength(0);
  });
});
