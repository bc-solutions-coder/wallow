import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { AppList } from "./AppList";

/**
 * Component spec for the Apps list page (Wallow-8w1h.5.2), mirroring
 * OrganizationList.test.tsx. The `getWallowSdk()` facade is mocked so the list
 * query is inert; the list/empty states are driven by seeding the `['apps']`
 * cache with `setQueryData`, and the loading state by leaving the query to hit a
 * never-resolving facade call.
 *
 * Testids follow `{page}-{element}` kebab-case: per-row
 * `app-item` (deliberately `app-item`, not `apps-row`), empty state
 * `apps-empty-state`, loading `apps-loading`.
 */

// Hoisted so the vi.mock factory and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  list: vi.fn(),
  get: vi.fn(),
  register: vi.fn(),
}));

// Mock the facade module the feature's api.ts imports (`../../lib/wallow-sdk`
// from features/apps; `../../../lib/wallow-sdk` from here).
vi.mock("../../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    apps: { list: mocks.list, get: mocks.get, register: mocks.register },
  }),
}));

function newClient(): QueryClient {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("AppList", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders each seeded app as an app-item element", async () => {
    const client = newClient();
    client.setQueryData(
      ["apps"],
      [
        {
          clientId: "c1",
          displayName: "Acme App",
          clientType: "public",
          redirectUris: [],
          createdAt: null,
        },
        {
          clientId: "c2",
          displayName: "Globex App",
          clientType: "confidential",
          redirectUris: ["https://globex.io/cb"],
          createdAt: "2026-07-01T00:00:00Z",
        },
      ],
    );

    renderWithClient(client, <AppList />);

    await expect.element(page.getByTestId("app-item").first()).toBeInTheDocument();
    expect(page.getByTestId("app-item").elements()).toHaveLength(2);
    await expect.element(page.getByText("Acme App")).toBeInTheDocument();
    await expect.element(page.getByText("Globex App")).toBeInTheDocument();
  });

  it("renders the empty state and no rows when the app list is empty", async () => {
    const client = newClient();
    client.setQueryData(["apps"], []);

    renderWithClient(client, <AppList />);

    await expect.element(page.getByTestId("apps-empty-state")).toBeInTheDocument();
    expect(page.getByTestId("app-item").elements()).toHaveLength(0);
  });

  it("renders a loading indicator while the list query is pending", async () => {
    const client = newClient();
    // No cached data -> the query fires; the facade never resolves, so the
    // component stays in its loading state.
    mocks.list.mockReturnValue(new Promise<never>(() => {}));

    renderWithClient(client, <AppList />);

    await expect.element(page.getByTestId("apps-loading")).toBeInTheDocument();
    expect(page.getByTestId("app-item").elements()).toHaveLength(0);
  });
});
