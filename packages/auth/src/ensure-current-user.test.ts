/**
 * Spec for `ensureCurrentUser` — the `beforeLoad` half of the current-user
 * contract (Wallow-x4qn.3).
 *
 * A route gate and the components under it must read ONE answer about who is
 * signed in, so the gate primes the request's query cache rather than fetching
 * privately. Two properties make that true, and both are asserted against a real
 * `QueryClient` and a real SDK instance over a counting transport:
 *
 *   1. it writes under the GENERATED key, so the `useCurrentUser` a component
 *      calls next is a cache hit on the very entry the gate resolved — not a
 *      second request for the same user;
 *   2. it is `ensureQueryData`, not `fetchQuery`. Paired with the query's
 *      30-second `staleTime` that is what makes a gate running on EVERY
 *      navigation cost one request instead of one per route change. A
 *      `fetchQuery` would satisfy every other assertion here and still refetch on
 *      each navigation, so the request count is the only thing that separates
 *      them.
 */

import { createQueryClient, type QueryClient } from "@bc-solutions-coder/query";
import { createWallowSdk, type CurrentUserResponse, type WallowSdk } from "@bc-solutions-coder/sdk";
import { usersGetCurrentUserQueryKey } from "@bc-solutions-coder/sdk/query";
import { describe, expect, it } from "vitest";

import { ensureCurrentUser } from "./ensure-current-user";

const BASE_URL: string = "https://api.test";

const SIGNED_IN_USER: CurrentUserResponse = {
  id: "3f1c4b0e-0000-4000-8000-000000000001",
  email: "admin@wallow.dev",
  roles: ["Admin"],
};

/** A real SDK instance over a transport that counts the requests it answers. */
interface CountingSdk {
  readonly sdk: WallowSdk;
  readonly count: () => number;
}

function sdkAnswering(status: number, body: unknown): CountingSdk {
  let calls: number = 0;
  const transport: typeof globalThis.fetch = (): Promise<Response> => {
    calls += 1;

    return Promise.resolve(
      new Response(JSON.stringify(body), {
        status,
        headers: { "content-type": "application/json" },
      }),
    );
  };

  return {
    sdk: createWallowSdk({ baseUrl: BASE_URL, fetch: transport }),
    count: (): number => calls,
  };
}

/** The RFC 7807 body the API answers an anonymous caller with. */
function unauthorizedProblem(): unknown {
  return { type: "about:blank", title: "Unauthorized", status: 401 };
}

describe("ensureCurrentUser", () => {
  it("resolves the signed-in user, sub included", async () => {
    const { sdk } = sdkAnswering(200, SIGNED_IN_USER);
    const queryClient: QueryClient = createQueryClient();

    await expect(ensureCurrentUser({ queryClient, client: sdk.client })).resolves.toEqual({
      ...SIGNED_IN_USER,
      sub: SIGNED_IN_USER.id,
    });
  });

  it("primes the cache under the generated key, so the next read is a cache hit", async () => {
    const { sdk } = sdkAnswering(200, SIGNED_IN_USER);
    const queryClient: QueryClient = createQueryClient();

    const user = await ensureCurrentUser({ queryClient, client: sdk.client });

    expect(queryClient.getQueryData(usersGetCurrentUserQueryKey({ client: sdk.client }))).toEqual(
      user,
    );
  });

  it("serves a second gate from the cache instead of re-asking the API", async () => {
    const { sdk, count } = sdkAnswering(200, SIGNED_IN_USER);
    const queryClient: QueryClient = createQueryClient();

    await ensureCurrentUser({ queryClient, client: sdk.client });
    await ensureCurrentUser({ queryClient, client: sdk.client });

    expect(count()).toBe(1);
  });

  it("resolves null for an anonymous visitor, so the gate can redirect", async () => {
    const { sdk } = sdkAnswering(401, unauthorizedProblem());
    const queryClient: QueryClient = createQueryClient();

    await expect(ensureCurrentUser({ queryClient, client: sdk.client })).resolves.toBeNull();
  });

  it("rejects on a backend failure rather than reporting the visitor as signed out", async () => {
    const { sdk } = sdkAnswering(500, { title: "Internal Server Error", status: 500 });
    const queryClient: QueryClient = createQueryClient();

    await expect(ensureCurrentUser({ queryClient, client: sdk.client })).rejects.toMatchObject({
      status: 500,
    });
  });
});
