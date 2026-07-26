import { queryKeys } from "@bc-solutions-coder/sdk/query";
import { QueryClient, useQueryClient } from "@tanstack/react-query";
import type { AnyRouter } from "@tanstack/react-router";
import { renderToString } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";

import { createRouter } from "./router";

/**
 * React Query SSR dehydration across the server -> client boundary
 * (Wallow-evd5.2.7).
 *
 * `createRouter()` mints one `QueryClient` per router (per SSR request), and
 * since Wallow-evd5.2.4 that single instance is both the router-context client
 * loaders prefetch into and the `Wrap` provider client components read. But the
 * SSR pass and the browser pass each call `createRouter()` separately, so the
 * client's instance starts EMPTY: every route whose loader prefetched on the
 * server (e.g. `/dashboard/organizations` via
 * `ensureQueryData(userQueries.currentUser())`) fetches a second time the moment
 * it hydrates.
 *
 * TanStack Router already carries arbitrary per-request state across that
 * boundary: `router.options.dehydrate()` runs during the SSR dehydration pass
 * and its return value is serialized into the document as
 * `DehydratedRouter.dehydratedData`; `router.options.hydrate(dehydratedData)`
 * runs on the client before `RouterClient` hands off to `RouterProvider`
 * (`@tanstack/router-core/ssr/{ssr-server,ssr-client}`). Wiring React Query's
 * own `dehydrate`/`hydrate` through those two hooks is what makes the SSR cache
 * survive — and it keeps `ssr.tsx`/`client.tsx` untouched, because both entries
 * already drive that pipeline.
 *
 * These specs assert behaviour, not payload shape: the wrapper key the router
 * stores the React Query state under is the implementation's choice, so every
 * assertion goes through the hooks themselves or through JSON serialization.
 */

/** The key the real SSR prefetch writes — `userQueries.currentUser()`'s key. */
const currentUserKey: readonly unknown[] = queryKeys.auth.currentUser();

/** Stand-in for the payload a server-side `currentUser()` prefetch caches. */
const ssrUser = { id: "1cf1a3a0-0000-4000-8000-000000000001", email: "admin@wallow.dev" };

function queryClientOf(router: AnyRouter): QueryClient {
  const client: unknown = router.options.context?.queryClient;
  if (!(client instanceof QueryClient)) {
    throw new Error("createRouter() exposes no QueryClient on the router context");
  }
  return client;
}

async function dehydrateRouter(router: AnyRouter): Promise<unknown> {
  const dehydrate: unknown = router.options.dehydrate;
  if (typeof dehydrate !== "function") {
    throw new TypeError(
      "createRouter() registers no `dehydrate` hook, so the React Query cache never reaches the document",
    );
  }
  return await (dehydrate as () => unknown | Promise<unknown>)();
}

async function hydrateRouter(router: AnyRouter, dehydrated: unknown): Promise<void> {
  const hydrate: unknown = router.options.hydrate;
  if (typeof hydrate !== "function") {
    throw new TypeError(
      "createRouter() registers no `hydrate` hook, so the client never adopts the SSR React Query cache",
    );
  }
  await (hydrate as (state: unknown) => unknown | Promise<unknown>)(dehydrated);
}

/** Server pass: prefetch into the router-context client, then dehydrate + serialize. */
async function serializeServerPass(): Promise<{ payload: unknown; dataUpdatedAt: number }> {
  const server = createRouter();
  const serverClient = queryClientOf(server);
  serverClient.setQueryData(currentUserKey, ssrUser);

  // Deliberately a JSON round-trip, not `structuredClone`: the payload crosses
  // the boundary as text embedded in the SSR document, so the client only ever
  // sees what survives serialization.
  const serialized: string = JSON.stringify(await dehydrateRouter(server));
  const payload: unknown = JSON.parse(serialized);
  const state = serverClient.getQueryState(currentUserKey);
  if (state === undefined) {
    throw new Error("the server-side prefetch did not land in the router-context client");
  }
  return { payload, dataUpdatedAt: state.dataUpdatedAt };
}

describe("createRouter (React Query SSR dehydration hooks)", () => {
  it("registers a dehydrate hook so the SSR pass can embed the query cache", () => {
    expect(typeof createRouter().options.dehydrate).toBe("function");
  });

  it("registers a hydrate hook so the browser pass can adopt the embedded cache", () => {
    expect(typeof createRouter().options.hydrate).toBe("function");
  });
});

describe("createRouter (dehydrated payload)", () => {
  it("carries the router-context client's prefetched queries", async () => {
    const router = createRouter();
    queryClientOf(router).setQueryData(currentUserKey, ssrUser);

    const serialized = JSON.stringify(await dehydrateRouter(router));

    // Key-name agnostic: whatever wrapper the router stores it under, the
    // dehydrated payload must contain the query's key AND its data.
    expect(serialized).toContain("current-user");
    expect(serialized).toContain(ssrUser.email);
  });

  it("stays JSON-serializable, since it is embedded into the SSR document", async () => {
    const router = createRouter();
    queryClientOf(router).setQueryData(currentUserKey, ssrUser);

    const payload: unknown = await dehydrateRouter(router);

    expect(() => JSON.stringify(payload)).not.toThrow();
  });
});

describe("createRouter (client-side hydration)", () => {
  it("restores SSR-prefetched data into the fresh browser-pass client", async () => {
    const { payload } = await serializeServerPass();

    const client = createRouter();
    expect(queryClientOf(client).getQueryData(currentUserKey)).toBeUndefined();

    await hydrateRouter(client, payload);

    expect(queryClientOf(client).getQueryData(currentUserKey)).toEqual(ssrUser);
  });

  it("preserves the server's freshness timestamp instead of restamping it", async () => {
    const { payload, dataUpdatedAt } = await serializeServerPass();

    const client = createRouter();
    await hydrateRouter(client, payload);

    // A restamped `dataUpdatedAt` would look "just fetched" locally but still
    // discards the server's staleness accounting; React Query carries it through
    // dehydration precisely so the client can honour it.
    expect(queryClientOf(client).getQueryState(currentUserKey)?.dataUpdatedAt).toBe(dataUpdatedAt);
  });

  it("does not refetch a hydrated query on hydration", async () => {
    const { payload } = await serializeServerPass();

    const client = createRouter();
    await hydrateRouter(client, payload);

    // Mirrors `userQueries.currentUser()` (staleTime 30_000): the loader re-runs
    // on the client after hydration, and must be served from the SSR cache.
    const queryFn = vi.fn(() => Promise.resolve(ssrUser));
    const data: unknown = await queryClientOf(client).ensureQueryData({
      queryKey: currentUserKey,
      queryFn,
      staleTime: 30_000,
    });

    expect(queryFn).not.toHaveBeenCalled();
    expect(data).toEqual(ssrUser);
  });

  it("hydrates the same client the Wrap provider hands to components", async () => {
    const { payload } = await serializeServerPass();

    const client = createRouter();
    await hydrateRouter(client, payload);

    const Wrap = client.options.Wrap;
    if (Wrap === undefined) {
      throw new Error("router exposes no Wrap, so no QueryClientProvider spans the app");
    }

    // The whole point of Wallow-evd5.2.4 + this task: ONE instance. Reading the
    // hydrated cache through the provider proves hydration did not land in a
    // second client that components never see.
    let providedData: unknown;
    function Probe(): null {
      providedData = useQueryClient().getQueryData(currentUserKey);
      return null;
    }

    renderToString(
      <Wrap>
        <Probe />
      </Wrap>,
    );

    expect(providedData).toEqual(ssrUser);
  });
});
