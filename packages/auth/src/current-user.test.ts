/**
 * Verify current-user query keys, a 30-second staleTime, profile mapping, and 401-only anonymous
 * results through a real SDK client with a stub transport.
 */

import { createQueryClient, type QueryClient } from "@bc-solutions-coder/query";
import { createWallowSdk, type CurrentUserResponse, type WallowSdk } from "@bc-solutions-coder/sdk";
import { usersGetCurrentUserQueryKey } from "@bc-solutions-coder/sdk/query";
import { describe, expect, it } from "vitest";

import { currentUserQuery } from "./current-user";

/** An absolute origin, so the stub transport sees the URL an SSR render would build. */
const BASE_URL: string = "https://api.test";

/** The path `UsersController.GetCurrentUser` is generated at. */
const CURRENT_USER_URL: string = `${BASE_URL}/v1/identity/users/me`;

/** Freshness window for current-user query observers. */
const EXPECTED_STALE_TIME_MS: number = 30_000;

const SIGNED_IN_USER: CurrentUserResponse = {
  id: "3f1c4b0e-0000-4000-8000-000000000001",
  email: "admin@wallow.dev",
  firstName: "Ada",
  lastName: "Admin",
  roles: ["Admin"],
  permissions: ["users.read"],
};

/** A real SDK instance whose transport answers once with `status` and `body`. */
interface StubbedSdk {
  readonly sdk: WallowSdk;
  /** URLs the client actually sent, in order — proof the call rode THIS instance. */
  readonly urls: string[];
}

function sdkAnswering(status: number, body: unknown): StubbedSdk {
  const urls: string[] = [];
  const transport: typeof globalThis.fetch = (input: RequestInfo | URL): Promise<Response> => {
    const request: Request = input instanceof Request ? input : new Request(input);
    urls.push(request.url);

    return Promise.resolve(
      new Response(JSON.stringify(body), {
        status,
        headers: { "content-type": "application/json" },
      }),
    );
  };

  return { sdk: createWallowSdk({ baseUrl: BASE_URL, fetch: transport }), urls };
}

/** The RFC 7807 body the API answers an anonymous caller with. */
function unauthorizedProblem(): unknown {
  return { type: "about:blank", title: "Unauthorized", status: 401 };
}

describe("currentUserQuery options", () => {
  it("uses the GENERATED query key, so any invalidation raised for the operation reaches it", () => {
    const { sdk } = sdkAnswering(200, SIGNED_IN_USER);

    expect(currentUserQuery(sdk.client).queryKey).toEqual(
      usersGetCurrentUserQueryKey({ client: sdk.client }),
    );
  });

  it("holds a resolved user for 30 seconds, so a gate on every navigation is a cache read", () => {
    const { sdk } = sdkAnswering(200, SIGNED_IN_USER);

    expect(currentUserQuery(sdk.client).staleTime).toBe(EXPECTED_STALE_TIME_MS);
  });
});

describe("currentUserQuery resolution", () => {
  it("resolves the signed-in user with sub renamed from the API's id", async () => {
    const { sdk } = sdkAnswering(200, SIGNED_IN_USER);
    const queryClient: QueryClient = createQueryClient();

    await expect(queryClient.fetchQuery(currentUserQuery(sdk.client))).resolves.toEqual({
      ...SIGNED_IN_USER,
      sub: SIGNED_IN_USER.id,
    });
  });

  it("keeps every field the API answered with alongside sub", async () => {
    // `sub` is an ADDITION, not a projection: a screen reading `email` or
    // `roles` off the resolved user must still find them.
    const { sdk } = sdkAnswering(200, SIGNED_IN_USER);
    const queryClient: QueryClient = createQueryClient();

    const user = await queryClient.fetchQuery(currentUserQuery(sdk.client));

    expect(user).toMatchObject({
      email: SIGNED_IN_USER.email,
      roles: SIGNED_IN_USER.roles,
      permissions: SIGNED_IN_USER.permissions,
    });
  });

  it("falls back to an empty sub when the API answers without an id", async () => {
    // `WallowUser.sub` is non-optional, so there is no "leave it off" option; an
    // empty string is the honest stand-in and keeps the claim helpers total.
    const { sdk } = sdkAnswering(200, { email: "nobody@wallow.dev" });
    const queryClient: QueryClient = createQueryClient();

    await expect(queryClient.fetchQuery(currentUserQuery(sdk.client))).resolves.toEqual({
      email: "nobody@wallow.dev",
      sub: "",
    });
  });

  it("resolves null when the API answers 401 — anonymous is an answer, not a failure", async () => {
    const { sdk } = sdkAnswering(401, unauthorizedProblem());
    const queryClient: QueryClient = createQueryClient();

    await expect(queryClient.fetchQuery(currentUserQuery(sdk.client))).resolves.toBeNull();
  });

  it("rejects on a 500 rather than reporting the user as signed out", async () => {
    // Softening anything beyond 401 would sign every real user out during an
    // outage. The error boundary is the correct destination for this one.
    const { sdk } = sdkAnswering(500, { title: "Internal Server Error", status: 500 });
    const queryClient: QueryClient = createQueryClient();

    await expect(queryClient.fetchQuery(currentUserQuery(sdk.client))).rejects.toMatchObject({
      status: 500,
    });
  });

  it("routes the request through the caller's own client, never a module-global one", async () => {
    // The request-scoped instance is what carries the session cookie and the
    // internal origin an SSR render needs; a call that leaked onto the generated
    // singleton would resolve against its baked "/api" instead and never reach
    // this transport.
    const { sdk, urls } = sdkAnswering(200, SIGNED_IN_USER);
    const queryClient: QueryClient = createQueryClient();

    await queryClient.fetchQuery(currentUserQuery(sdk.client));

    expect(urls).toEqual([CURRENT_USER_URL]);
  });
});
