import type { QueryClient } from "@bc-solutions-coder/query";
import type { SdkCall, SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import {
  createTestQueryClient,
  renderWithWallow,
} from "@bc-solutions-coder/testing/render-with-wallow";
import { type ReactNode, useState } from "react";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, type Mock, vi } from "vitest";

import { createAuthHarness } from "@shared/testing/harness";
import { accountVerifyMagicLinkQueryKey } from "../api";
import { MAGIC_LINK_EXPIRED_MESSAGE, MAGIC_LINK_VERIFY_FAILED_MESSAGE } from "../magic-link-result";
import { MagicLinkLoginForm } from "./MagicLinkLoginForm";

/**
 * The magic-link verify half, driven at the PANEL with spy callbacks for the
 * shell. The end-to-end copy and exchange live in `MagicLinkLoginForm.test.tsx`.
 *
 * Verify is a GET, so it is redeemed through `queryClient.fetchQuery` rather than
 * a mutation: the response lands in the query CACHE, and a one-time token served
 * from cache would hand back a ticket for a redemption that never happened.
 * Freshness and exactly-once are therefore asserted, not assumed.
 */

/** A GET, so its token rides the query string, not a body. */
const VERIFY_ENDPOINT = "/v1/identity/auth/passwordless/magic-link/verify";

/**
 * A token shaped like the one the service really mints: base64 of an HMAC, so it
 * carries `+`, `/` and `=` and can never survive being pasted into a URL unencoded.
 */
const TOKEN = "sGV+bG8/d29ybGQxMjM=";

/** The success body a redemption answers with. */
const VERIFIED_BODY = {
  succeeded: true,
  email: "user@example.com",
  signInTicket: "ticket-abc",
} as const;

/** The 401 status every verify failure arrives with. */
const UNAUTHORIZED_STATUS = 401;

/** A spent-link token — maps to the expired copy. */
const SPENT_TOKEN = "Token expired or already used.";

/** A token this screen's map does not know — the generic tail on the same 401. */
const UNKNOWN_TOKEN = "some_new_token";

let harness: SdkHarness;
let onAuthResult: Mock<(body: unknown) => void>;
let onError: Mock<(message: string | null) => void>;

beforeEach(() => {
  harness = createAuthHarness();
  onAuthResult = vi.fn<(body: unknown) => void>();
  onError = vi.fn<(message: string | null) => void>();
  harness.resolveJson(VERIFIED_BODY);
});

/** Answer every subsequent request with the bare `{ succeeded, error }` 401 body. */
function rejectVerify(token: string): void {
  harness.rejectJson({ succeeded: false, error: token }, UNAUTHORIZED_STATUS);
}

/** Every recorded request to the verify endpoint, in order. */
function verifyCalls(): readonly SdkCall[] {
  return harness.calls.filter((call): boolean => call.path === VERIFY_ENDPOINT);
}

/**
 * The key the redemption must land under, asked of the GENERATED factory rather
 * than spelled as a literal: the key carries the client's `baseUrl`, so a drifted
 * literal makes `getQueryData` return `undefined` rather than fail — a no-op
 * assertion every implementation passes.
 */
function verifyQueryKey() {
  return accountVerifyMagicLinkQueryKey({ client: harness.client, query: { token: TOKEN } });
}

/**
 * A parent that re-renders the panel with FRESH `onAuthResult`/`onError` identities
 * on every click — the shape the real shell has, where a failed verify sets the
 * banner and re-renders the panel below it.
 */
function VerifyHarness(props: { readonly token: string }): ReactNode {
  const [renders, setRenders] = useState(0);

  return (
    <div>
      <button
        type="button"
        data-testid="verify-rerender"
        onClick={() => {
          setRenders(renders + 1);
        }}
      >
        {renders}
      </button>
      <MagicLinkLoginForm
        token={props.token}
        onAuthResult={(body: unknown) => {
          onAuthResult(body);
        }}
        onError={(message: string | null) => {
          onError(message);
        }}
      />
    </div>
  );
}

/**
 * Mount the panel on the shared harness against the caller's cache.
 *
 * The cache is a PARAMETER rather than something this helper makes: every
 * assertion below reads it, and one test mounts twice against a single client.
 */
async function renderPanel(queryClient: QueryClient) {
  return await renderWithWallow(<VerifyHarness token={TOKEN} />, { harness, queryClient });
}

describe("the magic-link redemption reaches the API through the query cache", () => {
  it("stores the redeemed body under the generated verify key", async () => {
    // A hand-rolled mutation resolves the same body to the same callback and writes
    // NOTHING to the cache, so this is the one assertion that separates the two
    // mechanisms.
    const queryClient: QueryClient = createTestQueryClient();

    await renderPanel(queryClient);

    await vi.waitFor(() => {
      expect(onAuthResult).toHaveBeenCalledTimes(1);
    });

    expect(queryClient.getQueryData(verifyQueryKey())).toEqual(VERIFIED_BODY);
  });

  it("records a refused redemption in the cache as an error rather than as data", async () => {
    // A 401 must leave the cache entry in `error`, never populated with a half-body
    // a later read could mistake for a sign-in.
    rejectVerify(SPENT_TOKEN);

    const queryClient: QueryClient = createTestQueryClient();

    await renderPanel(queryClient);

    await vi.waitFor(() => {
      expect(onError).toHaveBeenCalledWith(MAGIC_LINK_EXPIRED_MESSAGE);
    });

    expect(queryClient.getQueryState(verifyQueryKey())?.status).toBe("error");
    expect(queryClient.getQueryData(verifyQueryKey())).toBeUndefined();
  });

  it("hands the raw response body up to the shell, not the query result wrapper", async () => {
    // The shell's `authDispositionOf` narrows THIS body. A wrapper
    // (`{ data, error, response }`) makes every branch fall through to the generic
    // failure — with the sign-in already completed on the server.
    await renderPanel(createTestQueryClient());

    await vi.waitFor(() => {
      expect(onAuthResult).toHaveBeenCalledTimes(1);
    });

    expect(onAuthResult).toHaveBeenCalledWith(VERIFIED_BODY);
  });

  it("sends the token as a GET query parameter with no request body", async () => {
    // Read off the wire: the token belongs in `query`, and a `body` on a GET means
    // the argument was assembled as if this were a POST.
    await renderPanel(createTestQueryClient());

    await vi.waitFor(() => {
      expect(verifyCalls()).toHaveLength(1);
    });

    const call: SdkCall = verifyCalls()[0] as SdkCall;

    expect(call.method).toBe("GET");
    expect(call.body).toBeUndefined();
    expect(new URL(call.url).searchParams.get("token")).toBe(TOKEN);
  });

  it("clears a stale banner before redeeming", async () => {
    // An "expired link" banner hanging over an in-flight redemption is a lie about
    // the current attempt.
    await renderPanel(createTestQueryClient());

    await vi.waitFor(() => {
      expect(onError).toHaveBeenCalled();
    });

    expect(onError.mock.calls[0]).toEqual([null]);
  });

  it("reports the generic copy for an unrecognised token on the same 401", async () => {
    // Binds the code map against a blanket `401 -> expired` rule: this endpoint's
    // 401 carries three tokens with two meanings. The rejection path is also where
    // `fetchQuery` differs most from `mutate()` — it throws rather than calling a
    // callback.
    rejectVerify(UNKNOWN_TOKEN);

    await renderPanel(createTestQueryClient());

    await vi.waitFor(() => {
      expect(onError).toHaveBeenCalledWith(MAGIC_LINK_VERIFY_FAILED_MESSAGE);
    });

    expect(onAuthResult).not.toHaveBeenCalled();
  });
});

describe("the magic-link token is redeemed exactly once", () => {
  it("does not redeem again when the panel re-renders with fresh callbacks", async () => {
    // `queryClient` is stable across renders, which makes the effect LOOK dep-safe,
    // but the shell's callbacks are new identities every render — and a second
    // redemption of a spent token can only fail.
    await renderPanel(createTestQueryClient());

    await vi.waitFor(() => {
      expect(verifyCalls()).toHaveLength(1);
    });

    const user = userEvent.setup();

    await user.click(page.getByTestId("verify-rerender"));
    await user.click(page.getByTestId("verify-rerender"));

    expect(verifyCalls()).toHaveLength(1);
    expect(onAuthResult).toHaveBeenCalledTimes(1);
  });

  it("redeems again on a fresh mount instead of replaying the cached response", async () => {
    // A cache is replayable and a magic-link token is not, so the entry must not be
    // treated as fresh. A `staleTime` (or `initialData`) on the verify options
    // would break this; the shared `queryClient` is what makes it observable.
    const queryClient: QueryClient = createTestQueryClient();

    const first = await renderPanel(queryClient);

    await vi.waitFor(() => {
      expect(verifyCalls()).toHaveLength(1);
    });

    await first.unmount();

    await renderPanel(queryClient);

    await vi.waitFor(() => {
      expect(verifyCalls()).toHaveLength(2);
    });
  });
});
