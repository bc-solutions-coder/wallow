import { useMutation } from "@bc-solutions-coder/query";
import { useRouteContext } from "@tanstack/react-router";
import { useEffect, useRef, useState } from "react";

import { mfaEnrollTotpMutation, mfaExchangeEnrollmentToken } from "../api";
import { exchangeFailureMessage, startFailureMessage } from "../enroll-result";

export interface EnrollmentStart {
  /** The TOTP secret `enroll/totp` minted, or `null` before it has. */
  readonly secret: string | null;
  /** The `otpauth://` URI for the QR. `null` degrades the screen to manual entry. */
  readonly qrUri: string | null;
  /** True while either hop is open — the exchange or the start. */
  readonly loading: boolean;
  /** Start-side failure copy. Confirm-side failures are the screen's own. */
  readonly error: string | null;
  /** The intro branch's retry. Fires `enroll/totp` again, nothing else. */
  readonly beginSetup: () => void;
}

/**
 * Runs MFA enrollment's opening sequence: exchange the settings hand-off token
 * if there is one, then start enrollment.
 *
 * ── FOUR THINGS HERE ARE LOAD-BEARING ────────────────────────────────────────
 *
 * 1. ORDER. The exchange is what mints the `Identity.MfaPartial` cookie, and an
 *    `enroll/totp` fired first has no session to resolve and 401s — reporting a
 *    session problem when the real fault is the link.
 *
 * 2. FIRE-ONCE. `startedRef` guards the sequence, because a second `enroll/totp`
 *    mints a SECOND secret and silently invalidates the QR the user has already
 *    scanned. A ref rather than state, so the effect does not re-run when it
 *    flips. (This is the oracle's `TryTakeFromJson` suppression, which existed to
 *    stop its interactive circuit re-calling the prerender's start.)
 *
 * 3. `exchanging` SEEDED FROM THE TOKEN. The very first paint of a token flow
 *    must already read as busy: the intro branch's "Begin setup" is a retry, and
 *    offering it before the cookie exists invites an `enroll/totp` that can only
 *    401.
 *
 * 4. NOTHING AWAITED between `startEnroll({})` and `setExchanging(false)`. React
 *    batches two updates in the same continuation into one render, so the busy
 *    state passes straight from the exchange to the start; put a yield between
 *    them and a frame appears in which the intro branch offers a live retry
 *    button mid-sequence.
 *
 * The secret lives in MUTATION state and never in a query: it is minted once, is
 * not refetchable, and a refetch would mint a second one.
 *
 * `blocked` suspends the sequence — the screen passes its refused-returnUrl
 * verdict. A refused destination never enrolls: do not make a user set up a
 * second factor for somewhere already decided against.
 */
export function useEnrollmentStart(
  enrollToken: string | undefined,
  blocked: boolean,
): EnrollmentStart {
  const { sdk } = useRouteContext({ from: "__root__" });
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // See (3) above.
  const [exchanging, setExchanging] = useState(enrollToken !== undefined && enrollToken !== "");

  // See (2) above.
  const startedRef = useRef(false);

  const startEnrollment = useMutation({
    // SPREAD, never passed straight through: the generated factory carries only a
    // `mutationFn`, so handing it over as the whole options object would drop the
    // `onMutate`/`onError` arms below.
    ...mfaEnrollTotpMutation({ client: sdk.client }),
    // The oracle's `_errorMessage = null;`. Cleared EAGERLY at mutate time rather
    // than derived from `error`, which would leave the dead message standing
    // above an in-flight retry for the whole request.
    onMutate: () => {
      setErrorMessage(null);
    },
    // `Error`, not `unknown`: the generated factory declares react-query's
    // `DefaultError`, and annotating wider than that makes the spread above and
    // this arm disagree about the mutation's error type. It is also what actually
    // arrives — the SDK's error interceptor normalises every rejection into a
    // `WallowError` — and `startFailureMessage` reads it as `unknown` regardless.
    onError: (cause: Error) => {
      setErrorMessage(startFailureMessage(cause));
    },
  });

  /** Stable across renders (TanStack Query binds it), so the mount effect may depend on it. */
  const startEnroll = startEnrollment.mutate;

  useEffect(() => {
    if (blocked) {
      return;
    }

    if (startedRef.current) {
      return;
    }
    startedRef.current = true;

    void (async () => {
      if (enrollToken !== undefined && enrollToken !== "") {
        try {
          await mfaExchangeEnrollmentToken({ client: sdk.client, query: { token: enrollToken } });
        } catch (error: unknown) {
          // Enrolling anyway would just 401 and report a session problem when the
          // real fault is the expired link.
          setErrorMessage(exchangeFailureMessage(error));
          setExchanging(false);
          return;
        }
      }

      // Opened with no yield before the exchange flag drops — see (4) above.
      //
      // `{}` and not `()`: the generated artifact's variables are its REQUEST
      // object, which is required even when — as here — this endpoint takes no
      // body, path or query of its own.
      startEnroll({});
      setExchanging(false);
    })();
  }, [blocked, enrollToken, startEnroll, sdk]);

  return {
    secret: startEnrollment.data?.secret ?? null,
    qrUri: startEnrollment.data?.qrUri ?? null,
    loading: exchanging || startEnrollment.isPending,
    error: errorMessage,
    beginSetup: () => {
      startEnroll({});
    },
  };
}
