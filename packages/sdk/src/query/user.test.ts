import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { setSsrRequestContextResolver } from "../ssr";
import { resetQueryBootstrapForTests } from "./bootstrap";
import { queryKeys } from "./keys";
import { userQueries } from "./user";

describe("userQueries", () => {
  it("keys the current-user query off the shared auth factory", () => {
    expect(userQueries.currentUser().queryKey).toEqual(queryKeys.auth.currentUser());
  });

  it("holds the current user for 30s so beforeLoad stops refetching per navigation", () => {
    expect(userQueries.currentUser().staleTime).toBe(30_000);
  });
});

/**
 * SSR self-fetch origin for the current-user query (Wallow-spb5).
 *
 * `beforeLoad` resolves this query during SSR, so it is the FIRST self-fetch of a
 * dashboard render — before the generated `/api/**` client runs. It builds its
 * target from the SSR request context, so it has the same hazard
 * `configureSsrClient` does: a container published on 5053 while listening on 3000
 * cannot fetch `http://localhost:5053/bff/user`, and the resulting "fetch failed"
 * becomes a 500 error boundary that never stamps `data-app-ready`.
 */
describe("userQueries.currentUser SSR fetch target", () => {
  const PUBLISHED_ORIGIN = "http://localhost:5053";
  const INTERNAL_ORIGIN = "http://localhost:3000";

  const fetchMock = vi.fn(
    async (_target: string, _init?: RequestInit) =>
      new Response(JSON.stringify({ id: "u1", email: "admin@wallow.dev" }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
  );

  beforeEach(() => {
    fetchMock.mockClear();
    vi.stubGlobal("fetch", fetchMock);
    resetQueryBootstrapForTests();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    setSsrRequestContextResolver(() => undefined);
    resetQueryBootstrapForTests();
  });

  async function runCurrentUserQueryFn(): Promise<void> {
    const options = userQueries.currentUser();
    await options.queryFn!({} as never);
  }

  it("fetches /bff/user on the internal origin when the request origin is not self-reachable", async () => {
    setSsrRequestContextResolver(() => ({
      origin: PUBLISHED_ORIGIN,
      cookie: "wallow_bff=sess",
      internalOrigin: INTERNAL_ORIGIN,
    }));

    await runCurrentUserQueryFn();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0]![0]).toBe(`${INTERNAL_ORIGIN}/bff/user`);
  });

  it("still forwards the session cookie when targeting the internal origin", async () => {
    setSsrRequestContextResolver(() => ({
      origin: PUBLISHED_ORIGIN,
      cookie: "wallow_bff=sess",
      internalOrigin: INTERNAL_ORIGIN,
    }));

    await runCurrentUserQueryFn();

    const init: RequestInit | undefined = fetchMock.mock.calls[0]![1];
    expect(init?.headers).toEqual({ cookie: "wallow_bff=sess" });
  });

  it("keeps using the request origin when no internal origin is set", async () => {
    setSsrRequestContextResolver(() => ({ origin: PUBLISHED_ORIGIN, cookie: undefined }));

    await runCurrentUserQueryFn();

    expect(fetchMock.mock.calls[0]![0]).toBe(`${PUBLISHED_ORIGIN}/bff/user`);
  });

  it("stays on the browser's relative /bff/user when there is no SSR request context", async () => {
    setSsrRequestContextResolver(() => undefined);

    await runCurrentUserQueryFn();

    expect(fetchMock.mock.calls[0]![0]).toBe("/bff/user");
  });
});
