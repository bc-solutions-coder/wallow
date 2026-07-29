/**
 * Specs for the shared `renderWithWallow` seam (Wallow-pu6a.5.1).
 *
 * BROWSER project — this is the one part of `@bc-solutions-coder/testing` that
 * mounts a real component tree, so it runs in headless Chromium like every other
 * `*.test.tsx` in the repo (`.claude/rules/TESTING.md`: never jsdom).
 *
 * Nothing here mocks anything. The SDK is the real one, built by the harness
 * over a fake transport; the router and query client are real. Assertions are
 * kept off the `{ data, error }` shape of a generated call so Wallow-pu6a.5.2's
 * `throwOnError` flip cannot break this seam's contract.
 */
import { QueryClient, useQuery } from "@tanstack/react-query";
import { createRootRoute, createRoute, useRouter } from "@tanstack/react-router";
import { useEffect } from "react";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import {
  createTestQueryClient,
  DEFAULT_RENDER_PATH,
  renderWithWallow,
  type WallowTestRouterContext,
} from "./render-with-wallow";
import { createSdkHarness, type SdkHarness } from "./sdk-harness";

function Hello(): React.ReactElement {
  return <p data-testid="hello">hello</p>;
}

/** Reads the router context the seam is supposed to supply and renders it. */
function ContextProbe(): React.ReactElement {
  const router = useRouter();
  const context = router.options.context as WallowTestRouterContext | undefined;
  return (
    <div>
      <span data-testid="probe-has-sdk">{String(context?.sdk !== undefined)}</span>
      <span data-testid="probe-has-query-client">{String(context?.queryClient !== undefined)}</span>
      <span data-testid="probe-location">{router.state.location.pathname}</span>
    </div>
  );
}

/** Proves a QueryClientProvider is in scope without touching the network. */
function QueryProbe(): React.ReactElement {
  const query = useQuery({ queryKey: ["render-with-wallow", "probe"], queryFn: () => "cached" });
  return <span data-testid="query-probe">{query.data ?? "pending"}</span>;
}

/** Proves the context SDK is wired to the harness transport. */
function SdkProbe(): React.ReactElement {
  const router = useRouter();
  const context = router.options.context as WallowTestRouterContext;
  useEffect(() => {
    void context.sdk.client.get({ url: "/v1/identity/users/me" });
  }, [context.sdk]);
  return <span data-testid="sdk-probe">sent</span>;
}

describe("createTestQueryClient", () => {
  it("returns a QueryClient with retries disabled", () => {
    const client = createTestQueryClient();

    expect(client).toBeInstanceOf(QueryClient);
    // Retries turn a deliberate `rejectJson` into seconds of backoff before the
    // error state a spec is waiting on ever renders.
    expect(client.getDefaultOptions().queries?.retry).toBe(false);
    expect(client.getDefaultOptions().mutations?.retry).toBe(false);
  });

  it("returns a fresh client per call so caches never leak between specs", () => {
    expect(createTestQueryClient()).not.toBe(createTestQueryClient());
  });
});

describe("renderWithWallow", () => {
  let harness: SdkHarness;

  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson({});
  });

  it("renders the given element", async () => {
    renderWithWallow(<Hello />, { harness });

    await expect.element(page.getByTestId("hello")).toBeInTheDocument();
  });

  it("supplies a QueryClientProvider", async () => {
    renderWithWallow(<QueryProbe />, { harness });

    await expect.element(page.getByTestId("query-probe")).toHaveTextContent("cached");
  });

  it("puts the SDK and the query client on the router context", async () => {
    renderWithWallow(<ContextProbe />, { harness });

    await expect.element(page.getByTestId("probe-has-sdk")).toHaveTextContent("true");
    await expect.element(page.getByTestId("probe-has-query-client")).toHaveTextContent("true");
  });

  it("routes context-SDK calls through the harness transport", async () => {
    renderWithWallow(<SdkProbe />, { harness });

    await expect.element(page.getByTestId("sdk-probe")).toBeInTheDocument();
    await expect.poll(() => harness.last?.path).toBe("/api/v1/identity/users/me");
  });

  it("starts the memory history at the default path", async () => {
    renderWithWallow(<ContextProbe />, { harness });

    await expect.element(page.getByTestId("probe-location")).toHaveTextContent(DEFAULT_RENDER_PATH);
  });

  it("starts the memory history at an explicit path, query string included", async () => {
    const { router } = renderWithWallow(<ContextProbe />, {
      harness,
      path: "/login?returnUrl=%2Fdashboard",
    });

    await expect.element(page.getByTestId("probe-location")).toHaveTextContent("/login");
    expect(router.state.location.search).toEqual(
      expect.objectContaining({ returnUrl: "/dashboard" }),
    );
  });

  it("returns the harness, query client and router it rendered with", () => {
    const queryClient = createTestQueryClient();

    const result = renderWithWallow(<Hello />, { harness, queryClient });

    expect(result.harness).toBe(harness);
    expect(result.queryClient).toBe(queryClient);
    expect(result.router).toBeDefined();
  });

  it("creates its own harness when none is supplied", async () => {
    const result = renderWithWallow(<Hello />, {});

    await expect.element(page.getByTestId("hello")).toBeInTheDocument();
    expect(result.harness.sdk).toBeDefined();
    expect(result.harness).not.toBe(harness);
  });

  it("mounts caller-supplied routes under its throwaway root", async () => {
    // The route-level case: a spec that needs the real route's `validateSearch`/
    // `beforeLoad` rather than a bare component. A real app route is already
    // parented to its app's `__root`, so the seam re-parents whatever it is
    // handed onto its own throwaway root — mirroring the local `renderRouteAt`
    // helper this replaces. `otherRoot` stands in for that foreign parent.
    const otherRoot = createRootRoute();
    const route = createRoute({
      getParentRoute: () => otherRoot,
      path: "/thing",
      component: () => <p data-testid="thing">thing</p>,
    });

    renderWithWallow(null, { harness, routes: [route], path: "/thing" });

    await expect.element(page.getByTestId("thing")).toBeInTheDocument();
  });
});
