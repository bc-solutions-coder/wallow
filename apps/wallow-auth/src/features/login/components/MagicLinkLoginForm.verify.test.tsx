import type { QueryClient } from "@bc-solutions-coder/query";
import type { SdkCall, SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import {
  createTestQueryClient,
  renderWithWallow,
} from "@bc-solutions-coder/testing/render-with-wallow";
import { type ReactNode, useState } from "react";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, type Mock, vi } from "vitest";

import { createAuthHarness } from "../../../test/harness";
import { accountVerifyMagicLinkQueryKey } from "../api";
import { MAGIC_LINK_EXPIRED_MESSAGE, MAGIC_LINK_VERIFY_FAILED_MESSAGE } from "../magic-link-result";
import { MagicLinkLoginForm } from "./MagicLinkLoginForm";

/**
 * The magic-link tab's VERIFY half, driven at the PANEL rather than through the
 * login shell (Wallow-x4qn.9.3).
 *
 * `MagicLinkLoginForm.test.tsx` beside this file already covers verify end to end
 * through `LoginScreen` — the copy each 401 token earns, the ticket exchange a
 * success triggers, the wire method and query string. Nothing here restates that.
 * This file exists because verify is the ONE site in this bead that cannot become a
 * generated mutation at all, and the replacement it does get is a different
 * mechanism with observable consequences the shell-level spec cannot see.
 *
 * ── WHY THIS SITE IS DIFFERENT ───────────────────────────────────────────────
 *
 * `GET /v1/identity/auth/passwordless/magic-link/verify` is a READ to the code
 * generator, so `@bc-solutions-coder/sdk/query` emits `accountVerifyMagicLinkOptions`
 * and NO `accountVerifyMagicLinkMutation` (pinned in `src/generated-mutations.test.ts`).
 * Its eight siblings swap a hand-rolled `mutationFn` for a `{op}Mutation()` factory;
 * this one stops being a mutation and is redeemed through the QUERY CLIENT
 * (`queryClient.fetchQuery`) instead.
 *
 * That moves the redemption from mutation state into the query CACHE, keyed by the
 * generated key — which is the difference this file pins, and which is also the risk:
 *
 *  • A cache is REPLAYABLE. A one-time-use token whose response is served from cache
 *    on the next mount would hand a user a `signInTicket` for a redemption that never
 *    happened, and hide a spent link behind a stale success. So "a fresh mount
 *    re-redeems" is asserted, not assumed — it is what rules out a `staleTime` that
 *    turns this cache entry into a replay.
 *  • The EFFECT is rewritten. Losing the `useRef` latch is the classic regression
 *    here: the `queryClient` handle is stable across renders in a way the old
 *    `verifyMutation` object was not, which makes the effect LOOK dep-safe — but
 *    `onAuthResult` and `onError` are still fresh identities every render, and a
 *    magic-link token is deleted from Redis on redemption (`PasswordlessService.cs:117`).
 *    A re-fired effect paints "this link has expired" over a sign-in that just
 *    succeeded. {@link VerifyHarness} re-renders the panel with NEW callback
 *    identities on demand so that is a test rather than a hope, per bd memory
 *    `exactly-once-server-mutations-in-react-need-a-ref-not-just-deps`.
 *  • Rejection changes shape. `mutate()` routes failure to an `onError` callback;
 *    `fetchQuery` REJECTS. A conversion that forgets to catch leaves the banner
 *    empty and the user staring at a dead form.
 *
 * ── SEAM ─────────────────────────────────────────────────────────────────────
 *
 * The shared SDK harness (`@bc-solutions-coder/testing/sdk-harness`): the SDK is REAL
 * and only its `fetch` is faked, so the whole pipeline — request-scoped SDK ->
 * generated artifact -> serialization -> error shaping -> React Query — runs here.
 * The panel is mounted BARE, with `onAuthResult`/`onError` spies standing in for the
 * shell, exactly as `ExternalProviders.test.tsx` mounts its component: the shell owns
 * the navigation, so a bare panel needs no Navigation-API arming and a leaked
 * redirect cannot tear the runner down.
 *
 * Browser project — it mounts a component in real Chromium.
 */

/** `AccountController.VerifyMagicLink` (:838) — a GET; its token rides the query. */
const VERIFY_ENDPOINT = "/v1/identity/auth/passwordless/magic-link/verify";

/**
 * A token shaped like the one the service really mints: base64 of an HMAC, so it
 * carries `+`, `/` and `=` and can never survive being pasted into a URL unencoded.
 */
const TOKEN = "sGV+bG8/d29ybGQxMjM=";

/** `PasswordlessService.ValidateMagicLinkAsync`'s success body (AccountController.cs:848). */
const VERIFIED_BODY = {
  succeeded: true,
  email: "user@example.com",
  signInTicket: "ticket-abc",
} as const;

/** The 401 status every verify failure arrives with. */
const UNAUTHORIZED_STATUS = 401;

/** A spent-link token (PasswordlessService.cs:112) — maps to the expired copy. */
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
 * than spelled as a literal.
 *
 * A literal is a trap this app's specs have already fallen into once: the generated
 * key carries the client's `baseUrl`, so it is not knowable without the harness —
 * and a drifted literal makes `getQueryData` return `undefined` rather than fail,
 * which turns the assertion into a no-op that every implementation passes.
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
    // THE CONVERSION, stated as an outcome rather than as a grep for `fetchQuery`.
    // A hand-rolled mutation resolves the same body to the same callback and writes
    // NOTHING to the cache, so this is the one assertion that separates the two
    // mechanisms — and it also pins that the key came from the generated factory,
    // which the frontend state boundary requires of every key in the app.
    const queryClient: QueryClient = createTestQueryClient();

    await renderPanel(queryClient);

    await vi.waitFor(() => {
      expect(onAuthResult).toHaveBeenCalledTimes(1);
    });

    expect(queryClient.getQueryData(verifyQueryKey())).toEqual(VERIFIED_BODY);
  });

  it("records a refused redemption in the cache as an error rather than as data", async () => {
    // The other half of the same fact: a 401 must leave the cache entry in `error`,
    // never populated with a half-body a later read could mistake for a sign-in.
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
    // The shell's `authDispositionOf` narrows THIS body. Handing it a wrapper
    // (`{ data, error, response }`) instead would make every branch fall through to
    // the generic failure — with the sign-in already completed on the server.
    await renderPanel(createTestQueryClient());

    await vi.waitFor(() => {
      expect(onAuthResult).toHaveBeenCalledTimes(1);
    });

    expect(onAuthResult).toHaveBeenCalledWith(VERIFIED_BODY);
  });

  it("sends the token as a GET query parameter with no request body", async () => {
    // The shape of the generated artifact's argument, read off the wire: the token
    // belongs in `query`, and `body` on a GET means the argument was assembled as if
    // this were still one of the eight POSTs.
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
    // The oracle's `_errorMessage = null` at the top of `HandleVerifyMagicLink`. An
    // "expired link" banner hanging over an in-flight redemption is a lie about the
    // current attempt, and the clear must survive the effect being rewritten.
    await renderPanel(createTestQueryClient());

    await vi.waitFor(() => {
      expect(onError).toHaveBeenCalled();
    });

    expect(onError.mock.calls[0]).toEqual([null]);
  });

  it("reports the generic copy for an unrecognised token on the same 401", async () => {
    // Binds the code map against a blanket `401 -> expired` rule: this endpoint's
    // 401 carries three tokens with two meanings, so a status-keyed shortcut would
    // tell a user with a tampered link to request a new one that will fail the same
    // way. The rejection path is also where `fetchQuery` differs most from
    // `mutate()` — it throws rather than calling a callback.
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
    // The latch, bound at the OUTCOME. `queryClient` is stable across renders where
    // the old `verifyMutation` object was not, which makes the rewritten effect look
    // dep-safe — but the shell's callbacks are new identities every render, and a
    // second redemption of a deleted Redis key can only fail.
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
    // A cache is replayable and a magic-link token is not. Serving the second mount
    // from cache would hand back a `signInTicket` for a redemption that never
    // happened — so the entry must not be treated as fresh. This is what a
    // `staleTime` (or an `initialData`) on the verify options would break, and the
    // shared `queryClient` across the two mounts is what makes it observable.
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
